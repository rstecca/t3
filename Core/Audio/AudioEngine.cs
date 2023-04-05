using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using ManagedBass;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharpDX.Win32;
using T3.Core.Animation;
using T3.Core.IO;
using T3.Core.Logging;
using T3.Core.Operator;
using T3.Core.Resource;

namespace T3.Core.Audio
{
    /// <summary>
    /// Controls loading, playback and discarding of audio clips.
    /// </summary>
    public static class AudioEngine
    {
        public static int clipChannels(AudioClip clip)
        {
            if (_clipPlaybacks.TryGetValue(clip.Id, out var stream))
            {
                Bass.ChannelGetInfo(stream.StreamHandle, out var info);
                return info.Channels;
            }

            return 0;
        }
        public static int clipSampleRate(AudioClip clip)
        {
            if (_clipPlaybacks.TryGetValue(clip.Id, out var stream))
            {
                Bass.ChannelGetInfo(stream.StreamHandle, out var info);
                return info.Frequency;
            }

            return 0;
        }

        public static void UseAudioClip(AudioClip clip, double time)
        {
            _updatedClipTimes[clip] = time;
        }

        public static void ReloadClip(AudioClip clip)
        {
            if (_clipPlaybacks.TryGetValue(clip.Id, out var stream))
            {
                Bass.StreamFree(stream.StreamHandle);
                _clipPlaybacks.Remove(clip.Id);
            }
            
            UseAudioClip(clip,0);
        }

        public static void prepareRecording(Playback playback)
        {
            foreach (var (audioClipId, clipStream) in _clipPlaybacks)
            {
                Bass.ChannelStop(clipStream.StreamHandle);
                Bass.ChannelPlay(clipStream.StreamHandle);
                clipStream.UpdateTimeRecord(playback);
            }

            _fifoBuffers.Clear();
        }

        public static void CompleteFrame(Playback playback,
            double frameDurationInSeconds,
            bool clearBuffers = false)
        {
            if (!_bassInitialized)
            {
                Bass.Free();
                Bass.Init();
                _bassInitialized = true;
            }
            AudioAnalysis.CompleteFrame(playback);
            
            // Create new streams
            foreach (var (audioClip, time) in _updatedClipTimes)
            {
                if (_clipPlaybacks.TryGetValue(audioClip.Id, out var clip))
                {
                    clip.TargetTime = time;
                }
                else
                {
                    var audioClipStream = AudioClipStream.LoadClip(audioClip);
                    if (audioClipStream != null)
                        _clipPlaybacks[audioClip.Id] = audioClipStream;
                }
            }
            
            List<Guid> obsoleteIds = new();
            var playbackSpeedChanged = Math.Abs(_lastPlaybackSpeed - playback.PlaybackSpeed) > 0.001f;
            _lastPlaybackSpeed = playback.PlaybackSpeed;

            var handledMainSoundtrack = false;
            foreach (var (audioClipId,clipStream) in _clipPlaybacks)
            {
                clipStream.IsInUse = _updatedClipTimes.ContainsKey(clipStream.AudioClip);
                if (!clipStream.IsInUse)
                {
                    obsoleteIds.Add(audioClipId);
                }
                else
                {
                    if (playback.IsLive && playbackSpeedChanged)
                        clipStream.UpdatePlaybackSpeed(playback.PlaybackSpeed);

                    if (!handledMainSoundtrack && clipStream.AudioClip.IsSoundtrack)
                    {
                        if (!Playback.Current.IsLive)
                        {
                            var sampleRate = Bass.ChannelGetAttribute(clipStream.StreamHandle, ChannelAttribute.Frequency);
                            var samples = (int)Math.Max(Math.Round(frameDurationInSeconds * sampleRate), 0.0);
                            var bytes = (int)Math.Max(Bass.ChannelSeconds2Bytes(clipStream.StreamHandle, frameDurationInSeconds), 0);

                            clipStream.UpdateTimeRecord(playback);

                            // create or clear buffers if necessary
                            byte[] buffer;
                            if (!_fifoBuffers.TryGetValue(clipStream.AudioClip, out buffer))
                                buffer = _fifoBuffers[clipStream.AudioClip] = new byte[0];
                            else if (clearBuffers)
                                buffer = new byte[0];

                            if (bytes > 0)
                            {
                                while (bytes >= buffer.Length &&
                                       Bass.ChannelIsActive(clipStream.StreamHandle) == PlaybackState.Playing)
                                {
                                    // update timing
                                    clipStream.UpdateTimeRecord(playback);

                                    Bass.ChannelUpdate(clipStream.StreamHandle, (int)(frameDurationInSeconds * 1000.0));
                                    var newBuffer = new byte[bytes];
                                    var newBytes = Bass.ChannelGetData(clipStream.StreamHandle, newBuffer, (int)DataFlags.Available);

                                    if (newBytes > 0)
                                    {
                                        newBuffer = new byte[newBytes];
                                        Bass.ChannelGetData(clipStream.StreamHandle, newBuffer, newBytes);
                                        buffer = buffer.Concat(newBuffer).ToArray();
                                        UpdateFftBuffer(clipStream.StreamHandle, playback);
                                    }
                                }

                                _fifoBuffers[clipStream.AudioClip] = buffer;
                            }
                        }
                        else
                        {
                            UpdateFftBuffer(clipStream.StreamHandle, playback);
                            clipStream.UpdateTimeLive(playback);
                        }

                        handledMainSoundtrack = true;
                    }
                }
            }

            foreach (var id in obsoleteIds)
            {
                _clipPlaybacks[id].Disable();
                _clipPlaybacks.Remove(id);
            }
            _updatedClipTimes.Clear();
        }

        public static void SetMute(bool configAudioMuted)
        {
            IsMuted = configAudioMuted;
            UpdateMuting();
        }
        
        public static bool IsMuted { get; private set; }
        
        private static void UpdateMuting()
        {
            foreach (var stream in _clipPlaybacks.Values)
            {
                var volume = IsMuted ? 0 : 1;
                Bass.ChannelSetAttribute(stream.StreamHandle,ChannelAttribute.Volume, volume);
            }
        }
        
        private static void UpdateFftBuffer(int soundStreamHandle, Playback playback)
        {
            int get256FftValues = (int)DataFlags.FFT512;

            // do not advance plaback if we are not in live mode
            if (!playback.IsLive)
                get256FftValues |= (int)268435456; // TODO: find BASS_DATA_NOREMOVE in ManagedBass

            if (playback.Settings != null && playback.Settings.AudioSource == PlaybackSettings.AudioSources.ProjectSoundTrack)
            {
                Bass.ChannelGetData(soundStreamHandle, AudioAnalysis.FftGainBuffer, get256FftValues);
            }
        }

        public static byte[] LastMixDownBuffer(double frameDurationInSeconds)
        {
            foreach (var (audioClipId, clipStream) in _clipPlaybacks)
            {
                if (_fifoBuffers.TryGetValue(clipStream.AudioClip, out var buffer))
                {
                    var bytes = (int)Bass.ChannelSeconds2Bytes(clipStream.StreamHandle, frameDurationInSeconds);

                    var result = buffer.SkipLast(buffer.Length - bytes).ToArray();
                    _fifoBuffers[clipStream.AudioClip] = buffer.Skip(bytes).ToArray();

                    // pad buffer with zeroes at the end if required
                    if (result.Length < bytes)
                        result = result.Concat(new byte[bytes - result.Length]).ToArray();

                    return result;
                }
            }

            // error
            return null;
        }

        private static double _lastPlaybackSpeed = 1;
        private static bool _bassInitialized;
        private static readonly Dictionary<Guid, AudioClipStream> _clipPlaybacks = new();
        private static readonly Dictionary<AudioClip, double> _updatedClipTimes = new();
        private static readonly Dictionary<AudioClip, byte[]> _fifoBuffers = new();
    }


    public class AudioClipStream
    {
        public AudioClip AudioClip;
        
        public double Duration;
        public int StreamHandle;
        public bool IsInUse;
        public bool IsNew = true;
        public float DefaultPlaybackFrequency { get; private set; }
        public double TargetTime { get; set; }

        public void UpdatePlaybackSpeed(double newSpeed)
        {
            // Stop
            if (newSpeed == 0.0)
            {
                Bass.ChannelStop(StreamHandle);
            }
            // Play backwards
            else if (newSpeed < 0.0)
            {
                Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.ReverseDirection, -1);
                Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Frequency, DefaultPlaybackFrequency * -newSpeed);
                Bass.ChannelPlay(StreamHandle);
            }
            else
            {
                Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.ReverseDirection, 1);
                Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Frequency, DefaultPlaybackFrequency * newSpeed);
                Bass.ChannelPlay(StreamHandle);
            }
        }


        public static AudioClipStream LoadClip(AudioClip clip)
        {
            if (string.IsNullOrEmpty(clip.FilePath))
                return null;
            
            Log.Debug($"Loading audioClip {clip.FilePath} ...");
            if (!File.Exists(clip.FilePath))
            {
                Log.Error($"AudioClip file '{clip.FilePath}' does not exist.");
                return null;
            }
            var streamHandle = Bass.CreateStream(clip.FilePath, 0, 0, BassFlags.Prescan | BassFlags.Float);
            Bass.ChannelGetAttribute(streamHandle, ChannelAttribute.Frequency, out var defaultPlaybackFrequency);
            Bass.ChannelSetAttribute(streamHandle, ChannelAttribute.Volume, AudioEngine.IsMuted ? 0 : 1);
            var bytes = Bass.ChannelGetLength(streamHandle);
            if (bytes < 0)
            {
                Log.Error($"Failed to initialize audio playback for {clip.FilePath}.");
            }
            var duration = (float)Bass.ChannelBytes2Seconds(streamHandle, bytes);
            
            var stream = new AudioClipStream()
                             {
                                 AudioClip = clip,
                                 StreamHandle = streamHandle,
                                 DefaultPlaybackFrequency = defaultPlaybackFrequency,
                                 Duration = duration,
                             };

            clip.LengthInSeconds = duration;
            return stream;
        }
        
        private const double AudioSyncingOffset = -2f / 60f;
        private const double AudioTriggerDelayOffset = 2f / 60f;

        /// <summary>
        /// We try to find a compromise between letting bass play the audio clip in the correct playback speed which
        /// eventually will drift away from Tooll's Playback time. If the delta between playback and audio-clip time exceeds
        /// a threshold, we resync.
        /// Frequent resync causes audio glitches.
        /// Too large of a threshold can disrupt syncing and increase latency.
        /// </summary>
        /// <param name="playback"></param>
        public void UpdateTimeLive(Playback playback)
        {
            if (Playback.Current.IsLive && Playback.Current.PlaybackSpeed == 0)
            {
                Bass.ChannelPause(StreamHandle);
                return;
            }
            
            var localTargetTimeInSecs = TargetTime - playback.SecondsFromBars(AudioClip.StartTime);
            var isOutOfBounds = localTargetTimeInSecs < 0 || localTargetTimeInSecs >= AudioClip.LengthInSeconds;
            var isPlaying = Bass.ChannelIsActive(StreamHandle) == PlaybackState.Playing;
            
            if (isOutOfBounds)
            {
                if (isPlaying)
                {
                    //Log.Debug("Pausing");
                    Bass.ChannelPause(StreamHandle);
                }
                return;
            }

            if (!isPlaying)
            {
                //Log.Debug("Restarting");
                Bass.ChannelPlay(StreamHandle);
            }

            var currentStreamPos = Bass.ChannelGetPosition(StreamHandle);
            var currentPos = Bass.ChannelBytes2Seconds(StreamHandle, currentStreamPos) - AudioSyncingOffset;
            var soundDelta = (currentPos - localTargetTimeInSecs) * playback.PlaybackSpeed;

            // we may not fall behind or skip ahead in playback
            var maxSoundDelta = ProjectSettings.Config.AudioResyncThreshold * Math.Abs(Playback.Current.PlaybackSpeed);
            if (Math.Abs(soundDelta) <= maxSoundDelta)
                return;

            // Resync
            Log.Debug($"Sound delta {soundDelta:0.000}s for {AudioClip.FilePath}");
            var resyncOffset = AudioTriggerDelayOffset * Playback.Current.PlaybackSpeed + AudioSyncingOffset;
            var newStreamPos = Bass.ChannelSeconds2Bytes(StreamHandle, localTargetTimeInSecs + resyncOffset);
            Bass.ChannelSetPosition(StreamHandle, newStreamPos, PositionFlags.Bytes);
        }

        /// <summary>
        /// Update timeline time when recoding
        /// </summary>
        /// <param name="playback"></param>
        public void UpdateTimeRecord(Playback playback)
        {
            // offset timing dependent on position in clip
            var resyncOffset = AudioSyncingOffset;
            var localTargetTimeInSecs = playback.TimeInSecs - playback.SecondsFromBars(AudioClip.StartTime) + resyncOffset;

            Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.NoRamp, 1);
            Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Volume, 1);
            Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.ReverseDirection, 1);
            Bass.ChannelSetAttribute(StreamHandle, ChannelAttribute.Frequency, DefaultPlaybackFrequency);

            var newStreamPos = Bass.ChannelSeconds2Bytes(StreamHandle, Math.Max(localTargetTimeInSecs, 0.0));
            var flags = PositionFlags.Bytes | PositionFlags.MixerNoRampIn | PositionFlags.Decode;
            Bass.ChannelSetPosition(StreamHandle, newStreamPos, flags);
        }

        public void Disable()
        {
            Bass.StreamFree(StreamHandle);
        }
    }
    
    public class AudioClip
    {
        #region serialized attributes 
        public Guid Id;
        public string FilePath;
        public double StartTime;
        public double EndTime;
        public float Bpm = 120;
        public bool DiscardAfterUse = true;
        public bool IsSoundtrack = false;
        #endregion

        /// <summary>
        /// Is initialized after loading...
        /// </summary>
        public double LengthInSeconds;  

        
        #region serialization
        public static AudioClip FromJson(JToken jToken)
        {
            var idToken = jToken[nameof(Id)];

            var idString = idToken?.Value<string>();
            if (idString == null)
                return null;

            var newAudioClip = new AudioClip
                                   {
                                       Id = Guid.Parse(idString),
                                       FilePath = jToken[nameof(FilePath)]?.Value<string>() ?? String.Empty,
                                       StartTime = jToken[nameof(StartTime)]?.Value<double>() ?? 0,
                                       EndTime = jToken[nameof(EndTime)]?.Value<double>() ?? 0,
                                       Bpm = jToken[nameof(Bpm)]?.Value<float>() ?? 0,
                                       DiscardAfterUse = jToken[nameof(DiscardAfterUse)]?.Value<bool>() ?? true,
                                       IsSoundtrack = jToken[nameof(IsSoundtrack)]?.Value<bool>() ?? true,
                                   };
            
            return newAudioClip;
        }

        public void ToJson(JsonTextWriter writer)
        {
            //writer.WritePropertyName(Id.ToString());
            writer.WriteStartObject();
            {
                writer.WriteValue(nameof(Id), Id);
                writer.WriteValue(nameof(StartTime), StartTime);
                writer.WriteValue(nameof(EndTime), EndTime);
                writer.WriteValue(nameof(Bpm), Bpm);
                writer.WriteValue(nameof(DiscardAfterUse), DiscardAfterUse);
                writer.WriteValue(nameof(IsSoundtrack), IsSoundtrack);
                writer.WriteObject(nameof(FilePath), FilePath);
            }
            writer.WriteEndObject();
        }

        #endregion        
    }
}