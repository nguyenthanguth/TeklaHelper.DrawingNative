# TeklaHelper.DrawingNative

An OOP wrapper for the **native drawing Plugin API** of Tekla Structures (`Drawings.dll`, `DBdict.dll`, `DBvirtual.dll`).
It covers what the Tekla Open API cannot do:

- Read the **inputs** of a drawing plugin (picked points, picked objects) and modify them.
- List the **objects drawn by a plugin** (Dimension, Mark, Line, LeaderLine...) — objects that are otherwise only visible after Explode.
- **Move a plugin** by moving its point inputs.
- Read and write the drawing's parent/child relation table (`grdbRelation`).

## Requirements

| | |
|---|---|
| Platform | .NET Framework 4.8, **x64** |
| Runtime | **Inside the Tekla process** (macros, plugins) with a drawing open |

> The library calls Tekla's native DLLs directly. In an external process (console app, WinForms...) those DLLs hold
> no drawing data, so every method throws `InvalidOperationException`.

## Installation

```
dotnet add package TeklaHelper.DrawingNative
```

**Using it in a Tekla macro:** copy `TeklaHelper.DrawingNative.dll` to `<model>\macros\drawings` and reference it with
a relative path:

```csharp
#pragma reference "TeklaHelper.DrawingNative.dll"
```

After replacing the DLL with a new version, restart Tekla (Tekla keeps the loaded DLL in memory).

## Main API

Namespace `TeklaHelper.DrawingNative.Plugin`:

| Member | Purpose |
|---|---|
| `DrawingNativePlugin.GetChildIds(pluginId)` | IDs of the objects drawn by the plugin |
| `DrawingNativePlugin.GetInputIds(pluginId)` / `GetInputs(pluginId)` | IDs / `NativePluginInput` objects of the plugin inputs |
| `DrawingNativePlugin.GetParentPluginId(childId)` / `IsChildOf(childId, pluginId)` | The plugin that drew an object |
| `DrawingNativePlugin.GetPluginIdsUsingObject(objectId)` | Plugins that use a model object as an input |
| `DrawingNativePlugin.MoveInputsToPlugin(oldId, newId)` | Move inputs to another plugin |
| `DrawingNativePlugin.GetRelationChildIds` / `GetRelationFatherIds` / `AddRelation` | The `grdbRelation` relation table |
| `NativePluginInput` | A single input: `Points`, `ObjectId`, `World`, `RelationType`, `Move`, `Update`, `Insert`, `Delete`... (must be disposed) |
| `NativePluginInput.SelectById` / `FromId` / `ForObject(world, objectId)` / `ForPoints(world, points)` | Load an existing input / create a new one |

Namespace `TeklaHelper.DrawingNative`: `RelationType` (the complete `grdbRelation_c::Type_e`), `NativeVector3`.

> `GetChildIds` returns the objects drawn by the plugin. `GetRelationChildIds` reads the relation table — for a plugin
> that table only holds the targets of its inputs, not its dimensions or marks.
>
> `GetChildIds` returns every object, including the dimensions inside a DimensionSet drawn by the same plugin. To keep
> only the top-level objects, remove the IDs returned by `DimensionSetBase.GetObjects()` (Open API), as
> `examples/GetPluginChildren.cs` does.

## Examples

The package ships two complete macros in the `examples/` folder: `GetPluginChildren.cs` and `MovePlugin.cs`.

### Read the inputs and child objects of a plugin

```csharp
using Tekla.Structures;
using Tekla.Structures.Drawing;
using Tekla.Structures.DrawingInternal;
using TeklaHelper.DrawingNative.Plugin;

int pluginId = plugin.GetIdentifier().ID;

foreach (NativePluginInput input in DrawingNativePlugin.GetInputs(pluginId))
{
    using (input)
    {
        Console.WriteLine(input);   // NativePluginInput(Id=..., ObjectId=..., World=..., RelationType=..., Points=...)
    }
}

foreach (int childId in DrawingNativePlugin.GetChildIds(pluginId))
{
    DrawingObject child = Operation.GetDrawingObject(new Identifier(childId), drawing.GetIdentifier());
    Console.WriteLine(childId + " " + child.GetType().Name);   // StraightDimension, Mark, Line...
}
```

### Move a plugin

```csharp
foreach (NativePluginInput input in DrawingNativePlugin.GetInputs(pluginId))
{
    using (input)
    {
        if (!input.IsPointInput) continue;              // object inputs have no points
        if (input.Move(500.0, 50.0, 0.0)) input.Update();
    }
}

// Tekla recalculates the plugin from its new inputs on commit; plugin.Modify() is not needed.
drawing.CommitChanges();

// Child IDs change when the plugin is recalculated, so read them again if needed.
List<int> newChildIds = DrawingNativePlugin.GetChildIds(pluginId);
```

## Notes

- Child object IDs **change every time the plugin is recalculated** (`drawing.CommitChanges()` after moving its inputs, `plugin.Modify()`...); do not keep old IDs.
- `NativePluginInput.Move` moves the points in their own coordinates. The native option to move in drawing
  coordinates or change the World closes Tekla outside its render loop, so it is not exposed; use `SetWorld` instead.
- The library relies on Tekla's non-public native API, reverse engineered from Tekla's native DLLs; the native layout
  may differ between Tekla versions. After upgrading Tekla, run the examples again to check it before use.
