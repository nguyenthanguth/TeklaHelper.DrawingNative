#pragma warning disable 1633 // Unrecognized #pragma directive
#pragma reference "Tekla.Macros.Akit"
#pragma reference "Tekla.Macros.Wpf.Runtime"
#pragma reference "Tekla.Macros.Runtime"
#pragma reference "Tekla.Structures"
#pragma reference "Tekla.Structures.Drawing"
#pragma reference "System.Windows.Forms"
#pragma reference "TeklaHelper.DrawingNative.dll"
#pragma warning restore 1633 // Unrecognized #pragma directive

// =====================================================================================
// TeklaHelper.DrawingNative example - MovePlugin
// Moves the selected plugins by (Dx, Dy):
//   1. Moves every point input with NativePluginInput.Move and saves it with Update.
//   2. drawing.CommitChanges() makes Tekla recalculate the plugins from their new inputs (plugin.Modify() is not needed).
//   Object inputs (for example a picked rebar) have no points and are left unchanged.
//
// Usage: copy this file and TeklaHelper.DrawingNative.dll to <model>\macros\drawings,
// open a drawing, select one or more plugins and run the macro.
// =====================================================================================

using System;
using System.Text;
using System.Windows.Forms;
using Tekla.Structures.Drawing;
using Tekla.Structures.DrawingInternal;
using TeklaHelper.DrawingNative.Plugin;

namespace UserMacros
{
    public sealed class Macro
    {
        private const string MacroName = "MovePlugin";
        private const double Dx = 500.0;
        private const double Dy = 50.0;

        [Tekla.Macros.Runtime.MacroEntryPointAttribute()]
        public static void Run(Tekla.Macros.Runtime.IMacroRuntime runtime)
        {
            try
            {
                DrawingHandler drawingHandler = new DrawingHandler();
                Drawing drawing = drawingHandler.GetActiveDrawing();
                if (drawing == null)
                {
                    MessageBox.Show("No drawing is open.", MacroName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                StringBuilder report = new StringBuilder();
                DrawingObjectEnumerator selected = drawingHandler.GetDrawingObjectSelector().GetSelected();
                while (selected.MoveNext())
                {
                    Plugin plugin = selected.Current as Plugin;
                    if (plugin == null) continue;

                    int pluginId = plugin.GetIdentifier().ID;
                    report.AppendLine(string.Format("Plugin '{0}' (ID {1}): moved {2} point input(s)",
                        plugin.Name, pluginId, MovePointInputs(pluginId, Dx, Dy)));
                }

                if (report.Length == 0)
                {
                    MessageBox.Show("No plugin is selected.", MacroName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                drawing.CommitChanges();
                MessageBox.Show(report.ToString(), MacroName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex, MacroName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Moves every point input of the plugin and saves it to the drawing database; returns the number of inputs saved.
        /// </summary>
        private static int MovePointInputs(int pluginId, double dx, double dy)
        {
            int saved = 0;
            foreach (NativePluginInput input in DrawingNativePlugin.GetInputs(pluginId))
            {
                using (input)
                {
                    if (input.IsPointInput && input.Move(dx, dy, 0.0) && input.Update()) saved++;
                }
            }
            return saved;
        }
    }
}
