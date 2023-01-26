﻿using System;
using ImGuiNET;
using T3.Core.Animation;
using T3.Core.Audio;
using T3.Core.Logging;
using T3.Editor.Gui.Graph;
using T3.Editor.Gui.Interaction;
using T3.Editor.Gui.Interaction.Timing;
using T3.Editor.Gui.Styling;
using T3.Editor.Gui.UiHelpers;
using Icon = T3.Editor.Gui.Styling.Icon;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace T3.Editor.Gui.Windows.TimeLine
{
    internal static class TimeControls
    {
        internal static void DrawTimeControls(TimeLineCanvas timeLineCanvas)
        {
            var playback = Playback.Current; // TODO, this should be non-static eventually

            // Settings
            if (CustomComponents.IconButton(Icon.Settings, PlaybackSettingsPopup.PlaybackSettingsPopupId, ControlSize))
            {
                //playback.TimeInBars = playback.LoopRange.Start;
                ImGui.OpenPopup(PlaybackSettingsPopup.PlaybackSettingsPopupId);
            }
            
            PlaybackSettingsPopup.DrawPlaybackSettings(ref playback);


            CustomComponents.TooltipForLastItem("Timeline Settings",
                                                "Switch between soundtrack and VJ modes. Control BPM and other inputs.");

            ImGui.SameLine();

            // Current Time
            var delta = 0.0;
            string formattedTime = "";
            switch (UserSettings.Config.TimeDisplayMode)
            {
                case TimeFormat.TimeDisplayModes.Bars:
                    formattedTime = TimeFormat.FormatTimeInBars(playback.TimeInBars, 0);
                    break;

                case TimeFormat.TimeDisplayModes.Secs:
                    formattedTime = TimeSpan.FromSeconds(playback.TimeInSecs).ToString(@"hh\:mm\:ss\:ff");
                    break;

                case TimeFormat.TimeDisplayModes.F30:
                    var frames = playback.TimeInSecs * 30;
                    formattedTime = $"{frames:0}f ";
                    break;
                case TimeFormat.TimeDisplayModes.F60:
                    var frames60 = playback.TimeInSecs * 60;
                    formattedTime = $"{frames60:0}f ";
                    break;
            }

            if (CustomComponents.JogDial(formattedTime, ref delta, new Vector2(100, ControlSize.Y)))
            {
                playback.PlaybackSpeed = 0;
                playback.TimeInBars += delta;
                if (UserSettings.Config.TimeDisplayMode == TimeFormat.TimeDisplayModes.F30)
                {
                    playback.TimeInSecs = Math.Floor(playback.TimeInSecs * 30) / 30;
                }
            }

            ImGui.SameLine();

            // Time Mode with context menu
            if (ImGui.Button(UserSettings.Config.TimeDisplayMode.ToString(), ControlSize))
            {
                UserSettings.Config.TimeDisplayMode =
                    (TimeFormat.TimeDisplayModes)(((int)UserSettings.Config.TimeDisplayMode + 1) % Enum.GetNames(typeof(TimeFormat.TimeDisplayModes)).Length);
            }

            CustomComponents.TooltipForLastItem("Timeline format",
                                                "Click to toggle through BPM, Frames and Normal time modes");

            ImGui.SameLine();

            // Continue Beat indicator
            {
                ImGui.PushStyleColor(ImGuiCol.Text, UserSettings.Config.EnableIdleMotion
                                                        ? new Vector4(1, 1, 1, 0.5f)
                                                        : new Vector4(0, 0, 0, 0.5f));

                if (CustomComponents.IconButton(Icon.BeatGrid, "##continueBeat", ControlSize))
                {
                    UserSettings.Config.EnableIdleMotion = !UserSettings.Config.EnableIdleMotion;
                }

                CustomComponents.TooltipForLastItem("Idle Motion - Keeps beat time running",
                                                    "This will keep updating the output [Time]\nwhich is useful for procedural animation and syncing.");

                if (UserSettings.Config.EnableIdleMotion)
                {
                    var center = (ImGui.GetItemRectMin() + ImGui.GetItemRectMax()) / 2;
                    var beat = (int)(playback.FxTimeInBars * 4) % 4;
                    var bar = (int)(playback.FxTimeInBars) % 4;
                    const int gridSize = 4;
                    var drawList = ImGui.GetWindowDrawList();
                    var min = center - new Vector2(7, 7) + new Vector2(beat * gridSize, bar * gridSize);
                    drawList.AddRectFilled(min, min + new Vector2(gridSize - 1, gridSize - 1), Color.Orange);
                }

                ImGui.PopStyleColor();
                ImGui.SameLine();
            }

            var hideTimeControls = playback is BeatTimingPlayback;

            if (hideTimeControls)
            {
                if (ImGui.Button($"{BeatTiming.Bpm:0.0} BPM?"))
                {
                    var newBpm = T3Ui._bpmDetection.ComputeBpmRate();
                    if (newBpm > 0)
                    {
                        Log.Debug("Setting bpm to" + newBpm);
                        BeatTiming.SetBpmRate(newBpm);
                    }
                    // T3Ui.BeatTiming.SetBpmFromSystemAudio();
                    // if (newBpm > 0)
                    //     playback.Bpm = newBpm;
                }

                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                var bar = (float)Math.Pow(1 - BeatTiming.BeatTime % 1, 4);
                var height = 1;

                //var volume = BeatTiming.SyncPrecision;
                ImGui.GetWindowDrawList().AddRectFilled(new Vector2(min.X, max.Y), new Vector2(min.X + 3, max.Y - height * (max.Y - min.Y)),
                                                        Color.Orange.Fade(bar));

                ImGui.SameLine();

                ImGui.Button("Sync");
                if (ImGui.IsItemActivated())
                {
                    BeatTiming.TriggerSyncTap();
                }
                else if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                {
                    Log.Debug("Resync!");
                    BeatTiming.TriggerResyncMeasure();
                }

                CustomComponents.TooltipForLastItem("Click on beat to sync. Tap later once to refine. Click right to sync measure.",
                                                    KeyboardBinding.ListKeyboardShortcuts(UserActions.PlaybackJumpToStartTime));

                ImGui.SameLine();

                ImGui.PushButtonRepeat(true);
                {
                    if (ImGui.ArrowButton("##left", ImGuiDir.Left))
                    {
                        BeatTiming.TriggerDelaySync();
                    }

                    ImGui.SameLine();

                    if (ImGui.ArrowButton("##right", ImGuiDir.Right))
                    {
                        BeatTiming.TriggerAdvanceSync();
                    }

                    ImGui.SameLine();
                }
                ImGui.PopButtonRepeat();
            }
            else
            {
                // Jump to start
                var isSelected = playback.TimeInBars != playback.LoopRange.Start;
                if (CustomComponents.ToggleIconButton(Icon.JumpToRangeStart,
                                                      label: "##jumpToBeginning",
                                                      isSelected: ref isSelected,
                                                      ControlSize)
                    || KeyboardBinding.Triggered(UserActions.PlaybackJumpToStartTime)
                    )
                {
                    playback.TimeInBars = playback.LoopRange.Start;
                }

                CustomComponents.TooltipForLastItem("Jump to beginning",
                                                    KeyboardBinding.ListKeyboardShortcuts(UserActions.PlaybackJumpToStartTime));

                ImGui.SameLine();

                // Prev Keyframe
                ImGui.PushStyleColor(ImGuiCol.Text, FrameStats.Last.HasKeyframesBeforeCurrentTime ? T3Style.Colors.Text.Rgba : Color.Black);
                if (CustomComponents.IconButton(Icon.JumpToPreviousKeyframe, "##prevKeyframe", ControlSize)
                    || KeyboardBinding.Triggered(UserActions.PlaybackJumpToPreviousKeyframe))
                {
                    UserActionRegistry.DeferredActions.Add(UserActions.PlaybackJumpToPreviousKeyframe);
                }
                ImGui.PopStyleColor();

                CustomComponents.TooltipForLastItem("Jump to previous keyframe",
                                                    KeyboardBinding.ListKeyboardShortcuts(UserActions.PlaybackJumpToPreviousKeyframe));

                ImGui.SameLine();

                // Play backwards
                var isPlayingBackwards = playback.PlaybackSpeed < 0;
                if (CustomComponents.ToggleIconButton(Icon.PlayBackwards,
                                                      label: isPlayingBackwards ? $"{(int)playback.PlaybackSpeed}x##backwards" : "##backwards",
                                                      isSelected: ref isPlayingBackwards,
                                                      ControlSize))
                {
                    if (playback.PlaybackSpeed != 0)
                    {
                        playback.PlaybackSpeed = 0;
                    }
                    else if (playback.PlaybackSpeed == 0)
                    {
                        playback.PlaybackSpeed = -1;
                    }
                }

                CustomComponents.TooltipForLastItem("Play backwards",
                                                    "Play backwards (and faster): " +
                                                    KeyboardBinding.ListKeyboardShortcuts(UserActions.PlaybackBackwards, false) +
                                                    "\n Previous frame:" + KeyboardBinding.ListKeyboardShortcuts(UserActions.PlaybackPreviousFrame, false));

                ImGui.SameLine();

                // Play forward
                var isPlaying = playback.PlaybackSpeed > 0;
                if (CustomComponents.ToggleIconButton(Icon.PlayForwards,
                                                      label: isPlaying ? $"{(int)playback.PlaybackSpeed}x##forward" : "##forward",
                                                      ref isPlaying,
                                                      ControlSize))
                {
                    if (Math.Abs(playback.PlaybackSpeed) > 0.001f)
                    {
                        playback.PlaybackSpeed = 0;
                    }
                    else if (Math.Abs(playback.PlaybackSpeed) < 0.001f)
                    {
                        playback.PlaybackSpeed = 1;
                    }
                }

                CustomComponents.TooltipForLastItem("Start playback",
                                                    "Play forward (and faster): " +
                                                    KeyboardBinding.ListKeyboardShortcuts(UserActions.PlaybackForward, false) +
                                                    "\n Play half speed (and slower): " +
                                                    KeyboardBinding.ListKeyboardShortcuts(UserActions.PlaybackForwardHalfSpeed, false) +
                                                    "\n Next frame:" + KeyboardBinding.ListKeyboardShortcuts(UserActions.PlaybackNextFrame, false));

                const float editFrameRate = 30;
                const float frameDuration = 1 / editFrameRate;

                // Step to previous frame
                if (KeyboardBinding.Triggered(UserActions.PlaybackPreviousFrame))
                {
                    var rounded = Math.Round(playback.TimeInBars * editFrameRate) / editFrameRate;
                    playback.TimeInBars = rounded - frameDuration;
                }

                // Step to previous frame
                if (KeyboardBinding.Triggered(UserActions.PlaybackJumpBack))
                {
                    //var rounded = Math.Round(playback.TimeInBars * editFrameRate) / editFrameRate;
                    playback.TimeInBars -= 1;
                }

                // Step to next frame
                if (KeyboardBinding.Triggered(UserActions.PlaybackNextFrame))
                {
                    var rounded = Math.Round(playback.TimeInBars * editFrameRate) / editFrameRate;
                    playback.TimeInBars = rounded + frameDuration;
                }

                // Play backwards with increasing speed
                if (KeyboardBinding.Triggered(UserActions.PlaybackBackwards))
                {
                    Log.Debug("Backwards triggered with speed " + playback.PlaybackSpeed);
                    if (playback.PlaybackSpeed >= 0)
                    {
                        playback.PlaybackSpeed = -1;
                    }
                    else if (playback.PlaybackSpeed > -16)
                    {
                        playback.PlaybackSpeed *= 2;
                    }
                }

                // Play forward with increasing speed
                if (KeyboardBinding.Triggered(UserActions.PlaybackForward))
                {
                    if (playback.PlaybackSpeed <= 0)
                    {
                        playback.PlaybackSpeed = 1;
                    }
                    else if (playback.PlaybackSpeed < 16) // Bass can't play much faster anyways
                    {
                        playback.PlaybackSpeed *= 2;
                    }
                }

                if (KeyboardBinding.Triggered(UserActions.PlaybackForwardHalfSpeed))
                {
                    if (playback.PlaybackSpeed > 0 && playback.PlaybackSpeed < 1f)
                        playback.PlaybackSpeed *= 0.5f;
                    else
                        playback.PlaybackSpeed = 0.5f;
                }

                // Stop as separate keyboard action 
                if (KeyboardBinding.Triggered(UserActions.PlaybackStop))
                {
                    playback.PlaybackSpeed = 0;
                }

                if (KeyboardBinding.Triggered(UserActions.PlaybackToggle))
                {
                    playback.PlaybackSpeed = playback.PlaybackSpeed == 0 ? 1 : 0;
                }

                ImGui.SameLine();

                // Next Keyframe
                ImGui.PushStyleColor(ImGuiCol.Text, FrameStats.Last.HasKeyframesAfterCurrentTime ? T3Style.Colors.Text.Rgba : Color.Black);
                if (CustomComponents.IconButton(Icon.JumpToNextKeyframe, "##nextKeyframe", ControlSize)
                    || KeyboardBinding.Triggered(UserActions.PlaybackJumpToNextKeyframe))
                {
                    UserActionRegistry.DeferredActions.Add(UserActions.PlaybackJumpToNextKeyframe);
                }
                ImGui.PopStyleColor();

                CustomComponents.TooltipForLastItem("Jump to next keyframe",
                                                    KeyboardBinding.ListKeyboardShortcuts(UserActions.PlaybackJumpToNextKeyframe));
                ImGui.SameLine();

                // // End
                // Loop
                if (CustomComponents.ToggleIconButton(Icon.Loop, "##loop", ref playback.IsLooping, ControlSize))
                {
                    var loopRangeMatchesTime = playback.LoopRange.IsValid && playback.LoopRange.Contains(playback.TimeInBars);
                    if (playback.IsLooping && !loopRangeMatchesTime)
                    {
                        playback.LoopRange.Start = (float)(playback.TimeInBars - playback.TimeInBars % 4);
                        playback.LoopRange.Duration = 4;
                    }
                }

                CustomComponents.TooltipForLastItem("Loop playback", "This will initialize one bar around current time.");

                ImGui.SameLine();
            }

            // Curve Mode
            if (ImGui.Button(timeLineCanvas.Mode.ToString(), ControlSize))
            {
                timeLineCanvas.Mode = (TimeLineCanvas.Modes)(((int)timeLineCanvas.Mode + 1) % Enum.GetNames(typeof(TimeLineCanvas.Modes)).Length);
            }

            CustomComponents.TooltipForLastItem("Toggle keyframe view between Dope sheet and Curve mode.");

            ImGui.SameLine();

            // ToggleAudio
            if (CustomComponents.IconButton(UserSettings.Config.AudioMuted ? Icon.ToggleAudioOff : Icon.ToggleAudioOn, "##audioToggle", ControlSize))
            {
                UserSettings.Config.AudioMuted = !UserSettings.Config.AudioMuted;
                AudioEngine.SetMute(UserSettings.Config.AudioMuted);
            }

            ImGui.SameLine();

            // ToggleHover
            Icon icon;
            string tooltip;
            string additionalTooltip = null;
            switch (UserSettings.Config.HoverMode)
            {
                case GraphCanvas.HoverModes.Disabled:
                    icon = Icon.HoverPreviewDisabled;
                    tooltip = "No preview images on hover";
                    break;
                case GraphCanvas.HoverModes.Live:
                    icon = Icon.HoverPreviewPlay;
                    tooltip = "Live Hover Preview - Render explicit thumbnail image.";
                    additionalTooltip = "This can interfere with the rendering of the current output.";
                    break;
                default:
                    icon = Icon.HoverPreviewSmall;
                    tooltip = "Last - Show the current state of the operator.";
                    additionalTooltip = "This can be outdated if operator is not require for current output.";
                    break;
            }

            if (CustomComponents.IconButton(icon, "##hoverPreview", ControlSize))
            {
                UserSettings.Config.HoverMode =
                    (GraphCanvas.HoverModes)(((int)UserSettings.Config.HoverMode + 1) % Enum.GetNames(typeof(GraphCanvas.HoverModes)).Length);
            }

            CustomComponents.TooltipForLastItem(tooltip, additionalTooltip);

            ImGui.SameLine();
        }




        public static Vector2 ControlSize => new Vector2(45, 30) * T3Ui.UiScaleFactor;
    }
}