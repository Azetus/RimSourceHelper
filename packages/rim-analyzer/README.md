# rim-analyzer

RimWorld DLL/XML analysis tool. Builds structured knowledge databases from game assemblies and Defs using Mono.Cecil.

## Prerequisites

- **.NET 10 Runtime** (or later) — required to run the analyzer. RimWorld mod developers typically already have the .NET SDK installed.

## Build

```bash
cd packages/rim-analyzer
dotnet publish -c Release    # Output: release/rim-analyzer/
```

Run via: `dotnet release/rim-analyzer/rim-analyzer.dll <command> [options]`

## Typical Workflow

```bash
dotnet rim-analyzer.dll build --game-path "D:\Steam\steamapps\common\RimWorld" --output "./rimworld.db"
dotnet rim-analyzer.dll add-mod --mod-path "D:\Steam\steamapps\common\RimWorld\Mods\VanillaFireModes" --db "./rimworld.db" --game-path "D:\Steam\steamapps\common\RimWorld"
dotnet rim-analyzer.dll decompile --target "Verse.Pawn" --db "./rimworld.db"
dotnet rim-analyzer.dll harmony --mod-path "D:\Steam\steamapps\common\RimWorld\Mods\VanillaFireModes" --game-path "D:\Steam\steamapps\common\RimWorld"
```

## Commands

### `build`

Analyze Core + DLCs → full rebuild of knowledge database.

```bash
rim-analyzer build --game-path <path> --output <path> [--verbose]
```

| Option | Required | Description |
|--------|----------|-------------|
| `--game-path` | ✅ | RimWorld game root directory |
| `--output` | ✅ | Output SQLite path (always overwritten) |
| `--verbose` | | Verbose stderr logging |

```bash
$ rim-analyzer build --game-path "D:\Steam\steamapps\common\RimWorld" --output "./rimworld.db"
```
```json
{"status":"success","types":16157,"methods":90777,"calls":228834,"defs":13809}
```

---

### `add-mod`

Add mod code + Defs + Harmony patches to existing database. Idempotent.

```bash
rim-analyzer add-mod --mod-path <path> --db <path> --game-path <path> [--verbose]
```

| Option | Required | Description |
|--------|----------|-------------|
| `--mod-path` | ✅ | Mod root directory |
| `--db` | ✅ | Existing database path |
| `--game-path` | ✅ | RimWorld game root (for DLL reference resolution) |
| `--verbose` | | Verbose stderr logging |

```bash
$ rim-analyzer add-mod --mod-path "E:\Code\mod\Rimworld\Vanilla-Melee-Modes" --db "./rimworld.db" --game-path "D:\Steam\steamapps\common\RimWorld"
```
```json
{"status":"success","types":51,"methods":157,"calls":141,"defs":7}
```

---

### `remove-mod`

Remove a mod's data from the database. Idempotent.

```bash
rim-analyzer remove-mod --name <name> --db <path>
```

| Option | Required | Description |
|--------|----------|-------------|
| `--name` | ✅ | Mod name (as in About.xml) |
| `--db` | ✅ | Database path |

```bash
$ rim-analyzer remove-mod --name "Vanilla Fire Modes" --db "./rimworld.db"
```
```json
{"status":"success","removed":"Vanilla Fire Modes"}
```

---

### `decompile`

On-demand decompilation. Resolves target DLL from database automatically.

```bash
rim-analyzer decompile --target <name> --db <path> [--game-path <path>]
```

| Option | Required | Description |
|--------|----------|-------------|
| `--target` | ✅ | Type FullName, method FullName, or method Signature |
| `--db` | ✅ | Database path |
| `--game-path` | | Override game root (fallback: DB metadata) |

Resolution: type FullName → full class; method FullName → all overloads; Signature → single method.

```bash
$ rim-analyzer decompile --target "Verse.Log" --db "./rimworld.db"
```
```json
{"status":"success","kind":"type","name":"Verse.Log","source":"using System;\nusing ..."}
```

```bash
$ rim-analyzer decompile --target "Verse.Log.Error" --db "./rimworld.db"
```
```json
{"status":"success","kind":"method","name":"Verse.Log.Error","overloads":1,"source":"public static void Error..."}
```

---

### `harmony`

Analyze Harmony patches in a mod. Stateless (no database needed).

```bash
rim-analyzer harmony --mod-path <path> [--game-path <path>]
```

| Option | Required | Description |
|--------|----------|-------------|
| `--mod-path` | ✅ | Mod root directory or DLL path |
| `--game-path` | | RimWorld game root (for DLL reference resolution) |

```bash
$ rim-analyzer harmony --mod-path "E:\Code\mod\Rimworld\Vanilla-Melee-Modes" --game-path "D:\Steam\steamapps\common\RimWorld"
```
```json
{"status":"success","modName":"Vanilla Melee Modes","patchCount":4,"patches":[{"targetType":"Verse.AI.Pawn_JobTracker","targetMethod":"StartJob","patchType":"Postfix",...},{"targetType":"Verse.Pawn","targetMethod":"PreApplyDamage","patchType":"Prefix",...},{"targetType":"Verse.VerbProperties","targetMethod":"AdjustedArmorPenetration","patchType":"Postfix",...},{"targetType":"Verse.VerbProperties","targetMethod":"AdjustedArmorPenetration","patchType":"Postfix",...}]}
```

> Harmony patches are also stored in DB during `add-mod` for cross-mod query support.

---

## Output Conventions

| Channel | Content |
|---------|---------|
| stdout | JSON result |
| stderr | Progress logs / warnings |
| Exit code | 0 = success, non-zero = failure |

## Recovery

Database inconsistent? Re-run `build` (deletes and recreates), then `add-mod` for each mod.
