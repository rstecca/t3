﻿using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using T3.Core.Logging;
using T3.Core.Utils;
using T3.Editor.Gui.Styling;

namespace T3.Editor.Gui.Interaction
{
    /// <summary>
    /// Draws a circular dial to manipulate values with various speeds
    /// </summary>
    public static class FreeDialOverlay
    {
        public static bool Draw(ref double roundedValue, bool restarted, Vector2 center, double min = double.NegativeInfinity,
                                double max = double.PositiveInfinity,
                                float scale = 0.1f, bool clamp = false)
        {
            var modified = false;
            _drawList = ImGui.GetForegroundDrawList();
            _io = ImGui.GetIO();
            
            if (restarted)
            {
                _baseLog10Speed = (int)(Math.Log10(scale)+3.5f);
                _value = roundedValue;
                _mousePositions.Clear();
                _center = _io.MousePos;
                _dampedRadius = 50;
                _dampedAngleVelocity = 0;
                _dampedModifierScaleFactor = 1;
                _lastValueAngle = 0;
                _framesSinceLastMove = 120;
                _originalValue = roundedValue;
            }

            _mousePositions.Add(_io.MousePos);

            if (_mousePositions.Count > 100)
            {
                _mousePositions.RemoveAt(0);
            }
            

            if (_mousePositions.Count > 1)
            {
                // Terminology
                // range - normalized angle from -0.5 ... 0.5 with 0 at current value
                // valueRange - delta value for complete revolution of current dial
                // tickInterval = Log10 delta vale between ticks.
                
                var p1 = _mousePositions[^1];
                var mousePosRadius = Vector2.Distance(_center, p1);
                
                // Update angle...
                var dir = p1 - _center;
                var valueAngle = MathF.Atan2(dir.X, dir.Y);
                var deltaAngle = DeltaAngle(valueAngle, _lastValueAngle);
                _lastValueAngle = valueAngle;

                var hasMoved = Math.Abs(deltaAngle) > 0.015f;
                if (hasMoved)
                {
                    
                    _framesSinceLastMove = 0;
                }
                else
                {
                    if(Math.Abs(mousePosRadius - _dampedRadius) > 40)
                        _framesSinceLastMove++;
                }
                
                _dampedAngleVelocity = MathUtils.Lerp(_dampedAngleVelocity, (float)deltaAngle, 0.06f);
                
                // Update radius and value range

                const float maxRadius = 2500;
                var angleDamping = MathF.Pow(MathUtils.SmootherStep(1, 15, _framesSinceLastMove),2) * 0.1f;
                _dampedRadius = MathUtils.Lerp(_dampedRadius, mousePosRadius.Clamp(40f,maxRadius), angleDamping);
                
                _drawList.AddCircle(_center, _dampedRadius+25,  UiColors.BackgroundFull.Fade(0.1f), 128, 50);

                _dampedModifierScaleFactor = MathUtils.Lerp(_dampedModifierScaleFactor, GetKeyboardScaleFactor(), 0.1f);
                
                var normalizedClampedRadius = ( _dampedRadius/1000).Clamp(0.07f, 1);
                var valueRange = (Math.Pow(4 * (normalizedClampedRadius ), 3)) * 50 * scale * _dampedModifierScaleFactor;
                
                var tickInterval =  Math.Pow(10, (int)Math.Log10(valueRange * 250 / _dampedRadius) - 2) ;
                
                
                // Update value...
                _value += deltaAngle / (Math.PI * 2) * valueRange;
                roundedValue = _io.KeyCtrl ? _value : Math.Round(_value / (tickInterval)) * tickInterval;
                
                var numberOfTicks = valueRange / tickInterval;
                var anglePerTick = 2*Math.PI / numberOfTicks;
                
                var valueTickOffsetFactor =  MathUtils.Fmod(_value, tickInterval) / tickInterval;
                var tickRatioAlignmentAngle = anglePerTick * valueTickOffsetFactor ;
                
                
                for (int tickIndex = -(int)numberOfTicks/2; tickIndex < numberOfTicks/2; tickIndex++)
                {
                    var f = MathF.Pow(MathF.Abs(tickIndex / ((float)numberOfTicks/2)), 0.5f);
                    var negF = 1 - f;
                    var tickAngle = tickIndex * anglePerTick - valueAngle - tickRatioAlignmentAngle ;
                    var direction = new Vector2(MathF.Sin(-(float)tickAngle), MathF.Cos(-(float)tickAngle));
                    var valueAtTick = _value + (tickIndex * anglePerTick - tickRatioAlignmentAngle) / (2 * Math.PI) * valueRange;
                    var isPrimary =   Math.Abs(MathUtils.Fmod(valueAtTick + tickInterval * 5, tickInterval * 10) - tickInterval * 5) < tickInterval / 10;
                    var isPrimary2 =   Math.Abs(MathUtils.Fmod(valueAtTick + tickInterval * 50, tickInterval * 100) - tickInterval * 50) < tickInterval / 100;
                    
                    _drawList.AddLine(direction * _dampedRadius + _center,
                    direction * (_dampedRadius + (isPrimary ? 10 : 5f)) + _center,
                        UiColors.ForegroundFull.Fade(negF * (isPrimary ? 1 : 0.5f)),
                        1
                    );
                                        
                    if (isPrimary)
                    {
                        var font = isPrimary2 ? Fonts.FontBold : Fonts.FontSmall;
                        var v = Math.Abs(valueAtTick) < 0.0001 ? 0 : valueAtTick;
                        var label = $"{v:G5}";
                        
                        ImGui.PushFont(font);
                        var size = ImGui.CalcTextSize(label);
                        ImGui.PopFont();
                        
                        _drawList.AddText(font, 
                                          font.FontSize, 
                                          direction * (_dampedRadius + 30) + _center - size/2, 
                                          UiColors.ForegroundFull.Fade(negF * (isPrimary2 ? 1 : 0.5f)), 
                                          label);
                    }
                }

                // Current value at mouse
                {
                    var dialFade = MathUtils.SmootherStep(60, 160, _dampedRadius);
                    var dialAngle= (float)( (_value - roundedValue) * (2 * Math.PI) / valueRange + valueAngle);
                    _dampedDialValueAngle = MathUtils.LerpAngle(_dampedDialValueAngle, dialAngle, 0.4f);
                    var direction = new Vector2(MathF.Sin(_dampedDialValueAngle), MathF.Cos(_dampedDialValueAngle));
                    _drawList.AddLine(direction * _dampedRadius + _center,
                                      direction * (_dampedRadius + 30) + _center,
                                      UiColors.ForegroundFull.Fade(0.7f * dialFade),
                                      2
                                     );
                    
                    var labelFade = MathUtils.SmootherStep(200, 300, _dampedRadius);
                    _drawList.AddText(Fonts.FontBold,
                                      Fonts.FontBold.FontSize,
                                      direction * (_dampedRadius - 40) + _center +  new Vector2(-15,-Fonts.FontSmall.FontSize/2), 
                                      Color.White.Fade(labelFade * dialFade), 
                                      $"{roundedValue:0.00}\n" 
                                      );
                }
                
                // Draw previous value
                {
                    var visible = Math.Abs(_value - _originalValue) < valueRange / 3;
                    if (visible)
                    {
                        var originalValueAngle= (float)( (_value - _originalValue) * (2 * Math.PI) / valueRange + valueAngle);
                        var direction = new Vector2(MathF.Sin(originalValueAngle), MathF.Cos(originalValueAngle));
                        _drawList.AddLine(direction * _dampedRadius + _center,
                                          direction * (_dampedRadius - 10) + _center,
                                          UiColors.StatusActivated.Fade(0.8f),
                                          2
                                         );
                    }
                }
            }

            return true;
        }

        private static float _dampedRadius = 0;
        private static Vector2 _center = Vector2.Zero;
        private static float _dampedAngleVelocity;
        private static double _lastValueAngle;
        private static double _dampedModifierScaleFactor;
        private static float _dampedDialValueAngle;
        private static int _framesSinceLastMove;
        private static double _originalValue;

        private static double DeltaAngle(double angle1, double angle2)
        {
            angle1 = (angle1 + Math.PI) % (2 * Math.PI) - Math.PI;
            angle2 = (angle2 + Math.PI) % (2 * Math.PI) - Math.PI;

            var delta = angle2 - angle1;
            return delta switch
                       {
                           > Math.PI  => delta - 2 * Math.PI,
                           < -Math.PI => delta + 2 * Math.PI,
                           _          => delta
                       };
        }
        
        /** The precise value before rounding. This used for all internal calculations. */
        private static double _value; 

        private static bool CalculateIntersection(Vector2 p1A, Vector2 p1B, Vector2 p2A, Vector2 p2B, out Vector2 intersection)
        {
            double a1 = p1B.Y - p1A.Y;
            double b1 = p1A.X - p1B.X;
            double c1 = a1 * p1A.X + b1 * p1A.Y;

            double a2 = p2B.Y - p2A.Y;
            double b2 = p2A.X - p2B.X;
            double c2 = a2 * p2A.X + b2 * p2A.Y;

            double determinant = a1 * b2 - a2 * b1;

            if (Math.Abs(determinant) < 1e-6) // Lines are parallel
            {
                intersection =Vector2.Zero;
                return false;
            }

            var x = (b2 * c1 - b1 * c2) / determinant;
            var y = (a1 * c2 - a2 * c1) / determinant;
            intersection = new Vector2((float)x, (float)y);
            return true;
        }
        
        private static readonly List<Vector2> _mousePositions = new(100);
        
        private static float _baseLog10Speed = 1;

        private static double GetKeyboardScaleFactor()
        {
            if (_io.KeyAlt)
            {
                return 10;
            }

            if (_io.KeyShift)
            {
                return 0.1;
            }

            return 1;
        }
        

        private static ImDrawListPtr _drawList;
        private static ImGuiIOPtr _io;
    }
}