# Workflow and decoding of the native API

This document records how `TeklaHelper.DrawingNative` was built: how the native DLLs of Tekla Structures were
analyzed, how each finding was verified, and what to check again when upgrading Tekla.

> This document only describes the method, function addresses and memory layouts. The repository contains no decompiled Tekla code.

## 1. Goal

The Tekla Open API (`Tekla.Structures.Drawing`) does not expose:

- The **inputs** of a drawing plugin: the points or objects the user picked when creating the plugin.
- The **objects drawn by a plugin** (Dimension, Mark, Line, LeaderLine...). These objects only become visible after Explode.
- A way to **move a plugin** by modifying its inputs.

This information exists in Tekla's `Drawings.dll` but is not public. The goal was to find the right native functions,
determine their exact signatures and memory layouts, and wrap them in a safe C# API.

## 2. Tools and analysis targets

| | |
|---|---|
| Tools | [REA](https://github.com/morluto/rea) (an MCP server for reverse engineering) + Ghidra 12.1.4 |
| Tekla version | Tekla Structures 2025.0 SP7, x64 |
| `Drawings.dll` | SHA-256 `c5ab9d73f10f7540c5f82947ba094eadc4423da9501b53961490abb957d6186b`, image base `0x180000000`, 344 exported functions |
| `Tekla.Structures.Native.DbvDatabase.dll` | SHA-256 `605a3ab88d9f013919081a7d9e5a244b9a9ea7719cf5aa1fc83836cf2640dd18` |
| `DBdict.dll` | SHA-256 `9a3dcec4f6154df758ade916aa5278de4be33582be60f64674dd7d4d8749a4d1` |
| `DBvirtual.dll` | SHA-256 `54e65ccea86dc8eba2a44d03adc6dccdb5bf23cfc73847088e9b75650e7f9def` |

The first analysis of `Drawings.dll` (about 20 MB) takes a very long time, so `ghidra.startup_timeout_ms` had to be raised to
`2400000` (40 minutes).

## 3. Workflow

### Step 1 - List and verify the exported functions

- Read the export table of `Drawings.dll`. Demangle the C++ (MSVC) names to get the class, parameters and return type.
- Check every `EntryPoint` used in `[DllImport]` against the export table, so every name in `NativeMethods.cs` exists exactly as declared.
- Classes related to drawing plugins:
  - `grclaPluginInput_c`: 24 functions (plugin inputs);
  - `grclaPlugin_c`: 1 function (`IsPluginChild`);
  - `grdbRelation_c`: 3 functions (`GetIDsOfChildren`, `GetIDsOfFathers`, `AddRelation`).

### Step 2 - Decompile the layout of `grclaPluginInput_c`

The constructor, `DBSelect`, `DBInsert`, `GetPoints`… were decompiled to infer the object layout (sizeof = `0x48`):

| Offset | Field |
|---|---|
| `0x00` | `std::vector<Vector_c>` Points (Vector_c = 3 × double, `0x18` bytes) |
| `0x18` | `std::set<int>` anchored points |
| `0x38` | `int` ObjectId |
| `0x3C` | `int` Id |
| `0x40` | `int` World |
| `0x44` | `int` RelationType |

Pitfalls found while decompiling:

- The `(int, int)` constructor takes **`(world, objectId)`**, the opposite of the order one would usually guess.
- `DBSelect` dereferences the database object pointer **without a NULL check**: a nonexistent ID crashes Tekla. `GetPluginId` reads the same
  object but does check for NULL, so the library calls it first as a safe probe.
- `GetPointAnchored` asserts when the index exceeds the number of points.
- `Move(..., vectorInDrawingCoordinates = true)` uses Tekla's World stack. Called outside the render loop, it hits the assert `!m_Stack.empty()` and Tekla closes.
  The library therefore always passes `false` and does not expose this option (nor `newWorld`, which requires it).

### Step 3 - Calling conventions (MSVC x64 ABI) for P/Invoke

- Member functions: the `this` pointer is the first parameter. On x64, `CallingConvention.ThisCall` has no effect.
- Returning a class by value (`std::vector<int>`): for static functions, declaring a struct of 3 pointers (`First/Last/End`) as the return
  type is enough, because the CLR creates the hidden return buffer.
- A C++ `bool` is 1 byte, so `[MarshalAs(UnmanagedType.I1)]` is required.
- **Freeing a `std::vector` returned by native code:** `Drawings.dll` uses the Universal CRT, so memory is released with `ucrtbase.dll!free`.
  For blocks of `0x1000` bytes or more, `std::allocator` aligns to 32 bytes and stores the original pointer at `First[-8]`. See `StdVectorInt.ToListAndFree`.

### Step 4 - The complete `grdbRelation_c::Type_e`

- The validation function `FUN_1800c9740` (`grdb_relation.cpp`) lists every valid value; any other value hits the assert `"Unknown relation type"`.
- Values below 100000 match the `GR_O_*` object type codes. Their names were decoded from the string table `PTR_s_GR_O_VIEW`
  (index = code − `0xFB`) and cross-checked against the object table in a `.dg` file.
- Values 100002–100009 are input relations. For example, the number of points selects the type 100002..100005 (`FUN_18075fab0`).

The result is the `RelationType` enum.

### Step 5 - Why `GetIDsOfChildren` does not return the objects drawn by a plugin

The first experiment used `grdbRelation_c::GetIDsOfChildren(pluginId)` and returned only 2 IDs: the point set and the picked rebar.
Exploding the plugin, however, showed 11 objects. Decompiling `DBInsert` (`FUN_18075e760`) explained why:

- `grdbRelation` is a **shared relation table** (father, child, type). When an input is saved, Tekla writes a row
  with *father = plugin* and *child = input target*. So "Children" here is the child end of a relation, not an object drawn by the plugin.
- The objects drawn by a plugin live in the **object table `gr_object`** (table `0x130`), linked to the plugin through the `belongs_to` field:

| Offset in a `gr_object` row | Field |
|---|---|
| `+0x0` | `GR_O_*` type |
| `+0x4` | Object ID |
| `+0xC` | `belongs_to`: ID of the owning plugin |

Verified with real data: decompress the `.dg` drawing file (gzip) and compare its `gr_object` table with the open drawing.
For a `RebarGroupDimensionPlugin` plugin there are 14 rows owned by the plugin: 6 StraightDimension, 2 StraightDimensionSet,
3 Mark, 2 Line, 1 LeaderLine. This matches the 11 objects seen after Explode, plus the 2 DimensionSets and 1 LeaderLine.

The only exported function that reads `belongs_to` is `grclaPlugin_c::IsPluginChild(childId, -, -)`. It returns the ID of the owning plugin
and **only uses its first parameter**. However, it checks one ID at a time, so building a list means probing IDs upward from `pluginId + 1`.
That is slow and unreliable for large IDs.

### Step 6 - Enumerating exactly the way Tekla does

- Tekla's internal code lists a plugin's children with `FUN_180730f40` → `FUN_1800cdc00`. This opens a cursor on the index
  `gr_object_belongs_to_idx` with the key `belongs_to = pluginId`. Neither function is **exported**.
- The **import table** of `Drawings.dll` shows that the database engine lives in separate DLLs:

| DLL | Role |
|---|---|
| `Tekla.Structures.Native.DbvDatabase.dll` | C++ classes `dbvDatabase_c` / `dbvCursor_c` |
| `DBvirtual.dll` | C functions `dbv_*`: cursors, fetch, table read/write, undo |
| `DBdict.dll` | C functions `dbd_*`: database dictionary, tables, indexes |
| `DBfast.dll` | C functions `dbf_*`: index structures |

- Decompiling `dbvDatabase_c::OpenCursor` and `dbvCursor_c::Fetch/Close` showed that these C++ classes are only **thin wrappers** around
  the C API. The C API is exported under plain (unmangled) names, so calling it through P/Invoke is straightforward:

```c
int dbd_getvirtualdatabasebyname(const char* name, void** ctx);                         // DBdict.dll
int dbd_getvirtualindexpointer_ctx(void* ctx, int table, const char* index, void** idx); // DBdict.dll
int dbd_getvirtualtablesizeof_ctx(void* ctx, int table, int* size);                     // DBdict.dll
int dbv_opencursor_ctx(void* idx, void* keyFrom, void* keyTo, void** cursor);           // DBvirtual.dll
int dbv_fetch_ctx(void* cursor, void* row, int* status);  // a row is returned when the result is 0 and status == 0
int dbv_closecursor_ctx(void* cursor);
```

- **The drawing database name:** the strings of `Drawings.dll` contain `"drawings_editor_db"`, next to `Model`, `Library` and
  `ModelImportDb`. The library tries this name first and falls back to the current database (`dbd_getvirtualdatabase()`). It
  only accepts a database that has the index `gr_object_belongs_to_idx`.
- **The cursor key** is an all-zero `gr_object` row with only `belongs_to` (`+0xC`) set to `pluginId`, and the same key is used for both
  `keyFrom` and `keyTo`, just like Tekla's internal code.

The result is `DrawingNativePlugin.GetChildIds(pluginId)` (`Database/DrawingObjectTable.cs`).

### Step 7 - Verification inside Tekla

Each step was verified with drawing macros running inside Tekla:

- Listing inputs: the points and ObjectIds match what was picked when the plugin was created.
- Listing children with the cursor: returns exactly 14 objects. Each ID was cross-checked with `IsPluginChild` (result `OK`) and its type read through the Open API.
- Moving a plugin: `NativePluginInput.Move` + `Update`, then `drawing.CommitChanges()`. Tekla recalculates the plugin from its new
  inputs on commit, so `plugin.Modify()` is not needed (it even returned `false` on its first call after Tekla started, and without a
  commit nothing is redrawn). The plugin is redrawn at the new position,
  and **the IDs of its child objects change** every time the plugin is recalculated.

### Step 8 - Runtime limitation

Running from a console application raised an `AccessViolationException`. The reason:

- The Open API talks to Tekla through an inter-process channel, so it works from outside.
- P/Invoke, however, loads a **fresh, empty** copy of `Drawings.dll` / `DBdict.dll` into the console process. The global database list
  of `DBdict.dll` is NULL, and the first call dereferences that NULL pointer. On .NET Framework, `try/catch` cannot catch this error.

That is why the library checks `GetModuleHandle("Drawings.dll")` before every native call (`Common/NativeHost.cs`). If Tekla has not
already loaded this DLL into the current process, the library throws `InvalidOperationException` instead of letting the process die.

### Step 9 - Saved drawing file (.dg)

The objects drawn by a plugin can also be read from the saved drawing file `<model>\drawings\D<guid>.dg`, without
Tekla. The format was taken from `dbv_readtable` → `FUN_18003ee20` and `dbv_writetable` → `FUN_180043fa0` in `DBvirtual.dll`,
then checked by parsing a real file to its last byte.

- The file is **gzip**. Decompressed, it starts with the 12-byte header `"Xsteel %5.2f"` (for example `Xsteel  9.54`).
- Next comes a **table directory**: a table whose 4-byte rows are the table numbers (`0x144`, `0x143` … `0xFD`).
- Then one table per directory entry, in **reverse directory order** (`0xFD`, `0xFE`, `0x100` …). Every table is:

| Field | Size |
|---|---|
| Row count | `uint32` |
| Row length | `uint32` |
| Per row: state byte, then the row bytes | `1 + row length` |

- The state byte is 1..10. `dbv_readtable` rejects 11 and 12, and skips rows in state 6, 8 or 10 (bit mask `0x1d40`),
  so the library reports them as deleted.
- The row lengths match the tables in memory: `gr_object` (`0x130`) `0x18`, dimension (`0x100`) `0x5D8`, mark (`0x108`)
  `0x248`, graphic object (`0x116`) `0x128`.
- Dimension table (`0x100`): `+0x4` dimension ID, `+0x8` World, `+0xC` ID of the **dimension set** that contains it.
  In memory the same field is the key of the index `gr_dim_pid_idx`. This matches `DimensionSetBase.GetObjects()`
  of the Open API.

**Finding the file of a drawing.** Every save writes a new `D<random guid>.dg` and keeps the older versions (16 versions
of one drawing were found in the test model), so the file name is not the drawing GUID. Each version has an XML file
`D<random guid>.dg.metadata` (`<DrawingVersionMetadata>`) with `DrawingGuid` (= `Drawing.GetIdentifier().GUID`),
`Name`, `Mark`, `DrawingType` and `ModifyDate`. `ModifyDate` counts seconds since 1970 in local wall-clock time, not UTC:
it matches the local write time of the .dg file. The newest version is the one with the largest `ModifyDate`.

The result is `DrawingFile/DgFile.cs` and `DrawingFile/DgMetadata.cs`.

## 4. Library architecture

```
NativeFile.cs                  Native DLL names: DRAWINGS, DBDICT, DBVIRTUAL
Common/
  NativeHost.cs                Checks that the code runs inside the Tekla process
  NativeVector3.cs             Vector_c (3 doubles)
  RelationType.cs              The complete grdbRelation_c::Type_e
  StdVectorInt.cs              std::vector<int> returned by value + freeing its memory
Database/
  DatabaseNativeMethods.cs     P/Invoke for the C API of DBdict.dll / DBvirtual.dll
  DrawingObjectTable.cs        Cursor on gr_object_belongs_to_idx
Plugin/
  NativeMethods.cs             P/Invoke for Drawings.dll (29 functions)
  NativePluginInput.cs         Wrapper for grclaPluginInput_c (IDisposable)
  DrawingNativePlugin.cs       Public API
DrawingFile/
  DgFile.cs                    Reader for saved drawing files (.dg), works without Tekla
  DgMetadata.cs                .dg.metadata: drawing GUID of a file version, newest version of a drawing
  DgTable.cs / DgRow.cs        Raw tables and rows of the file
  DgDrawingObject.cs           One row of the gr_object table
Examples/                      Sample macros: GetPluginChildrenFromNative, GetPluginChildrenFromFile, MovePlugin
```

## 5. When upgrading Tekla Structures

The library relies on a non-public API, so it may break between versions. Checklist:

1. Compare the SHA-256 of the four DLLs in section 2. If they are identical, nothing needs to be done.
2. Check that every `EntryPoint` in `Plugin/NativeMethods.cs` is still in the export table of `Drawings.dll`.
3. Decompile the constructor and `DBSelect` of `grclaPluginInput_c` again to confirm sizeof `0x48` and the offsets.
4. Check the `Type_e` validation function and update `RelationType` if there are new values.
5. Check the string `drawings_editor_db`, the index `gr_object_belongs_to_idx`, the table number `0x130` and the `+0x4`/`+0xC` offsets of a `gr_object` row.
6. Run the two macros in `Examples/` on a drawing that contains a plugin and compare the results with Explode.
