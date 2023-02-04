﻿using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using T3.Editor.Gui.Styling;
using T3.Editor.Gui.UiHelpers;

namespace T3.Editor.Gui.Windows
{
    public class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            Config.Title = "Settings";
        }

        protected override void DrawContent()
        {
            var changed = false;
            ImGui.NewLine();
            if (ImGui.TreeNode("User Interface"))
            {
                FormInputs.AddVerticalSpace();
                FormInputs.SetIndent(20);
                changed |= FormInputs.AddCheckBox("Warn before Lib modifications",
                                                       ref UserSettings.Config.WarnBeforeLibEdit,
                                                       "This warning pops up when you attempt to enter an Operator that ships with the application.\n" +
                                                       "If unsure, this is best left checked.",
                                                   UserSettings.Defaults.WarnBeforeLibEdit);
                
                changed |= FormInputs.AddCheckBox("Use arc connections",
                                                                  ref UserSettings.Config.UseArcConnections,
                                                                  "Affects the shape of the connections between your Operators",
                                                                  UserSettings.Defaults.UseArcConnections);
                

                changed |= FormInputs.AddCheckBox("Use Jog Dial Control",
                                                                  ref UserSettings.Config.UseJogDialControl,
                                                                  "Affects the shape of the connections between your Operators",
                                                                  UserSettings.Defaults.UseJogDialControl);

                changed |= FormInputs.AddCheckBox("Show Graph thumbnails",
                                                                  ref UserSettings.Config.ShowThumbnails,
                                                   null,
                                                                  UserSettings.Defaults.ShowThumbnails);

                changed |= FormInputs.AddCheckBox("Drag snapped nodes",
                                                                  ref UserSettings.Config.SmartGroupDragging, 
                                                                  "An experimental features that will drag neighbouring snapped operators.",
                                                                  UserSettings.Defaults.SmartGroupDragging);
                
                changed |= FormInputs.AddCheckBox("Fullscreen Window Swap",
                                                                  ref UserSettings.Config.SwapMainAnd2ndWindowsWhenFullscreen,
                                                                  "Swap main and second windows when fullscreen",
                                                                  UserSettings.Defaults.SwapMainAnd2ndWindowsWhenFullscreen);

                FormInputs.ResetIndent();
                FormInputs.AddVerticalSpace();
                //ImGui.Dummy(new Vector2(20,20));
                

                changed |= FormInputs.AddFloat("UI Scale",
                                                               ref UserSettings.Config.UiScaleFactor,
                                                               0.1f, 5f, 0.01f, true, 
                                                               "The global scale of all rendered UI in the application",
                                                               UserSettings.Defaults.UiScaleFactor);
                

                changed |= FormInputs.AddFloat("Scroll smoothing",
                                                               ref UserSettings.Config.ScrollSmoothing,
                                                               0.0f, 0.2f, 0.01f, true,
                                                     null,
                                                     UserSettings.Defaults.ScrollSmoothing);

                changed |= FormInputs.AddFloat("Snap strength",
                                                    ref UserSettings.Config.SnapStrength,
                                                    0.0f, 0.2f, 0.01f, true,
                                                    "Controls the distance until items like keyframes snap in the timeline.",
                                                    UserSettings.Defaults.SnapStrength);
                
                changed |= FormInputs.AddFloat("Click threshold",
                                                               ref UserSettings.Config.ClickThreshold,
                                                               0.0f, 10f, 0.1f, true, 
                                                               "The threshold in pixels until a click becomes a drag. Adjusting this might be useful for stylus input.",
                                                               UserSettings.Defaults.ClickThreshold);
                
                changed |= FormInputs.AddFloat("Timeline Raster Density",
                                                               ref UserSettings.Config.TimeRasterDensity,
                                                               0.0f, 10f, 0.01f, true, 
                                                               "The threshold in pixels until a click becomes a drag. Adjusting this might be useful for stylus input.",
                                                               UserSettings.Defaults.TimeRasterDensity);
                ImGui.Dummy(new Vector2(20,20));
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Space Mouse"))
            {
                CustomComponents.HelpText("These settings only apply with a connected space mouse controller");
                
                changed |= FormInputs.AddFloat("Smoothing",
                                                               ref UserSettings.Config.SpaceMouseDamping,
                                                               0.0f, 10f, 0.01f, true);                

                changed |= FormInputs.AddFloat("Move Speed",
                                                               ref UserSettings.Config.SpaceMouseMoveSpeedFactor,
                                                               0.0f, 10f, 0.01f, true);                

                changed |= FormInputs.AddFloat("Rotation Speed",
                                                               ref UserSettings.Config.SpaceMouseRotationSpeedFactor,
                                                               0.0f, 10f, 0.01f, true);                
                
                ImGui.Dummy(new Vector2(20,20));
                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Additional Settings"))
            {
                changed |= FormInputs.AddFloat("Gizmo size",
                                                               ref UserSettings.Config.GizmoSize,
                                                               0.0f, 10f, 0.01f, true);                        

                changed |= FormInputs.AddFloat("Tooltip delay in Seconds",
                                                               ref UserSettings.Config.TooltipDelay,
                                                               0.0f, 30f, 0.01f, true);        
                
                ImGui.TreePop();
            }
            if (changed)
                UserSettings.Save();
        }

        public override List<Window> GetInstances()
        {
            return new List<Window>();
        }
    }
}