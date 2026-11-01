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
// TeklaHelper.DrawingNative example - GetPluginChildrenFromNative
// Lists the inputs and the objects drawn by each selected plugin (dimensions, marks, lines...).
// GetChildIds returns every object, including the dimensions inside a DimensionSet; the macro uses the Open API
// (DimensionSetBase.GetObjects) to show only the top-level objects, with each set's dimensions listed under it.
//
// Usage: copy this file and TeklaHelper.DrawingNative.dll to <model>\macros\drawings,
// open a drawing, select one or more plugins and run the macro.
// =====================================================================================

using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using Tekla.Structures;
using Tekla.Structures.Drawing;
using Tekla.Structures.DrawingInternal;
using TeklaHelper.DrawingNative.Plugin;

namespace UserMacros
{
    public sealed class Macro
    {
        private const string MacroName = "GetPluginChildrenFromNative";

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
                    report.AppendLine(string.Format("Plugin '{0}' (ID {1})", plugin.Name, pluginId));

                    // 1. Inputs: points or objects picked when the plugin was created.
                    foreach (NativePluginInput input in DrawingNativePlugin.GetInputs(pluginId))
                    {
                        using (input)
                        {
                            report.AppendLine("  [INPUT] " + input);
                        }
                    }

                    // 2. Objects drawn by the plugin. IDs change every time the plugin is recalculated.
                    //    GetChildIds also returns the dimensions inside a DimensionSet; list them under their set.
                    List<int> childIds = DrawingNativePlugin.GetChildIds(pluginId);
                    Dictionary<int, DrawingObject> children = new Dictionary<int, DrawingObject>();
                    foreach (int childId in childIds)
                    {
                        children[childId] = Operation.GetDrawingObject(new Identifier(childId), drawing.GetIdentifier());
                    }

                    Dictionary<int, List<int>> setMembers;
                    List<int> topLevelIds = GroupChildren(childIds, children, out setMembers);
                    report.AppendLine(string.Format("  [CHILD] {0} top-level object(s), {1} in total:", topLevelIds.Count, childIds.Count));
                    foreach (int childId in topLevelIds)
                    {
                        report.AppendLine(string.Format("    - {0}  {1}", childId, TypeName(children[childId])));

                        List<int> members;
                        if (!setMembers.TryGetValue(childId, out members)) continue;
                        foreach (int dimensionId in members)
                        {
                            DrawingObject dimension;
                            children.TryGetValue(dimensionId, out dimension);
                            report.AppendLine(string.Format("        - {0}  {1}", dimensionId, TypeName(dimension)));
                        }
                    }
                    report.AppendLine();
                }

                if (report.Length == 0)
                {
                    MessageBox.Show("No plugin is selected.", MacroName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                MessageBox.Show(report.ToString(), MacroName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex, MacroName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Groups the objects drawn by a plugin into parent/child: GetChildIds also returns the dimensions inside a
        /// DimensionSet; the set -> dimension relation comes from the Open API (DimensionSetBase.GetObjects).
        /// </summary>
        /// <param name="childIds">Objects drawn by the plugin (DrawingNativePlugin.GetChildIds).</param>
        /// <param name="children">The same objects, loaded with Operation.GetDrawingObject.</param>
        /// <param name="setMembers">For every DimensionSet in <paramref name="childIds"/>: its dimensions.</param>
        /// <returns>The top-level objects: <paramref name="childIds"/> without the dimensions that belong to a set.</returns>
        private static List<int> GroupChildren(List<int> childIds, Dictionary<int, DrawingObject> children,
                                               out Dictionary<int, List<int>> setMembers)
        {
            setMembers = new Dictionary<int, List<int>>();
            HashSet<int> nested = new HashSet<int>();
            foreach (KeyValuePair<int, DrawingObject> child in children)
            {
                DimensionSetBase set = child.Value as DimensionSetBase;
                if (set == null) continue;

                List<int> members = new List<int>();
                DrawingObjectEnumerator dimensions = set.GetObjects();
                while (dimensions.MoveNext())
                {
                    int dimensionId = dimensions.Current.GetIdentifier().ID;
                    members.Add(dimensionId);
                    nested.Add(dimensionId);
                }
                setMembers[child.Key] = members;
            }
            return childIds.FindAll(id => !nested.Contains(id));
        }

        private static string TypeName(DrawingObject drawingObject)
        {
            return drawingObject != null ? drawingObject.GetType().Name : "-";
        }
    }
}
