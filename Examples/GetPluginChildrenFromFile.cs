#pragma warning disable 1633 // Unrecognized #pragma directive
#pragma reference "Tekla.Macros.Akit"
#pragma reference "Tekla.Macros.Wpf.Runtime"
#pragma reference "Tekla.Macros.Runtime"
#pragma reference "Tekla.Structures"
#pragma reference "Tekla.Structures.Drawing"
#pragma reference "Tekla.Structures.Model"
#pragma reference "System.Windows.Forms"
#pragma reference "TeklaHelper.DrawingNative.dll"
#pragma warning restore 1633 // Unrecognized #pragma directive

// =====================================================================================
// TeklaHelper.DrawingNative example - GetPluginChildrenFromFile
// Lists the objects drawn by each selected plugin from the saved drawing file (.dg) instead of Tekla's memory:
//   1. DgMetadata.FindLatest finds the newest saved version of the open drawing (<model>\drawings\D<guid>.dg).
//   2. DgFile.GetChildIds lists the objects drawn by the plugin, as they were when the drawing was last saved.
//   3. DgFile.GetDimensionIds lists the dimensions of each DimensionSet, so they are shown under their set.
// Save the drawing first: the file does not contain unsaved changes.
//
// Usage: copy this file and TeklaHelper.DrawingNative.dll to <model>\macros\drawings,
// open a drawing, select one or more plugins and run the macro.
// =====================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Tekla.Structures;
using Tekla.Structures.Drawing;
using Tekla.Structures.DrawingInternal;
using TeklaHelper.DrawingNative.DrawingFile;

namespace UserMacros
{
    public sealed class Macro
    {
        private const string MacroName = "GetPluginChildrenFromFile";

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

                // 1. Newest saved version of the open drawing.
                string drawingsDirectory = Path.Combine(new Tekla.Structures.Model.Model().GetInfo().ModelPath, "drawings");
                DgMetadata latest = DgMetadata.FindLatest(drawingsDirectory, drawing.GetIdentifier().GUID);
                if (latest == null)
                {
                    MessageBox.Show("The drawing has no saved file yet. Save it and run the macro again.",
                        MacroName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                DgFile file = DgFile.Load(latest.DrawingFilePath);
                Dictionary<int, DgDrawingObject> objects = new Dictionary<int, DgDrawingObject>();
                foreach (DgDrawingObject drawingObject in file.GetObjects()) objects[drawingObject.Id] = drawingObject;

                StringBuilder report = new StringBuilder();
                DrawingObjectEnumerator selected = drawingHandler.GetDrawingObjectSelector().GetSelected();
                while (selected.MoveNext())
                {
                    Plugin plugin = selected.Current as Plugin;
                    if (plugin == null) continue;

                    // 2. Objects drawn by the plugin, as saved.
                    int pluginId = plugin.GetIdentifier().ID;
                    List<int> childIds = file.GetChildIds(pluginId);

                    // 3. Dimensions are listed under their DimensionSet.
                    Dictionary<int, List<int>> setMembers;
                    List<int> topLevelIds = GroupChildren(file, childIds, out setMembers);

                    report.AppendLine(string.Format("Plugin '{0}' (ID {1}): {2} top-level object(s), {3} in total",
                        plugin.Name, pluginId, topLevelIds.Count, childIds.Count));
                    foreach (int childId in topLevelIds)
                    {
                        report.AppendLine("    - " + Describe(childId, objects, drawing.GetIdentifier()));

                        List<int> members;
                        if (!setMembers.TryGetValue(childId, out members)) continue;
                        foreach (int dimensionId in members)
                        {
                            report.AppendLine("        - " + Describe(dimensionId, objects, drawing.GetIdentifier()));
                        }
                    }
                    report.AppendLine();
                }

                if (report.Length == 0)
                {
                    MessageBox.Show("No plugin is selected.", MacroName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                report.Insert(0, "File: " + Path.GetFileName(latest.DrawingFilePath) + ", saved " +
                                 latest.ModifyTime.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + Environment.NewLine);
                MessageBox.Show(report.ToString(), MacroName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:\n" + ex, MacroName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Groups the objects drawn by a plugin into parent/child: GetChildIds also returns the dimensions inside a
        /// DimensionSet; the set -> dimension relation comes from the file (DgFile.GetDimensionIds).
        /// </summary>
        /// <param name="file">The loaded drawing file.</param>
        /// <param name="childIds">Objects drawn by the plugin (DgFile.GetChildIds).</param>
        /// <param name="setMembers">For every DimensionSet in <paramref name="childIds"/>: its dimensions.</param>
        /// <returns>The top-level objects: <paramref name="childIds"/> without the dimensions that belong to a set.</returns>
        private static List<int> GroupChildren(DgFile file, List<int> childIds, out Dictionary<int, List<int>> setMembers)
        {
            setMembers = new Dictionary<int, List<int>>();
            HashSet<int> nested = new HashSet<int>();
            foreach (int childId in childIds)
            {
                List<int> members = file.GetDimensionIds(childId);
                if (members.Count == 0) continue;
                setMembers[childId] = members;
                nested.UnionWith(members);
            }
            return childIds.FindAll(id => !nested.Contains(id));
        }

        /// <summary>
        /// "ID  TypeName (0xType)": the type name comes from the Open API ("-" if the object is no longer in the open
        /// drawing), the type code from the file.
        /// </summary>
        private static string Describe(int id, Dictionary<int, DgDrawingObject> objects, Identifier drawingId)
        {
            DrawingObject drawingObject = Operation.GetDrawingObject(new Identifier(id), drawingId);
            DgDrawingObject saved;
            objects.TryGetValue(id, out saved);

            return string.Format("{0}  {1} (0x{2:X})", id, drawingObject != null ? drawingObject.GetType().Name : "-",
                saved != null ? saved.Type : 0);
        }
    }
}
