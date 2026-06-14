# rim-analyzer CLI Reference

RimWorld DLL analysis tool — builds structured knowledge databases from game assemblies and Defs.

## Commands

### `build`

Analyze RimWorld Core + DLCs and build a knowledge database from scratch.

```bash
rim-analyzer build --game-path <path> --output <path> [--verbose]
```

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--game-path` | path | ✅ | RimWorld game root directory |
| `--output` | path | ✅ | Output SQLite database path |
| `--verbose` | flag | | Enable verbose logging |

> `build` always performs a full rebuild. If the output file exists, it is deleted first.

**What it does:**
1. Loads `Assembly-CSharp.dll` from `{game-path}/RimWorldWin64_Data/Managed/`
2. Extracts all types, methods, fields, properties (metadata)
3. Builds inheritance graph
4. Analyzes IL call graph (call/callvirt/newobj/ldftn)
5. Parses Defs XML from `{game-path}/Data/Core/Defs/` and all detected DLCs
6. Detects Def-to-Def references
7. Writes everything to SQLite with source tracking

**Output (stdout):**
```json
{"status":"success","types":16157,"methods":79712,"calls":153596,"defs":13809}
```

**Example:**
```bash
rim-analyzer build \
  --game-path "D:\Steam\steamapps\common\RimWorld" \
  --output "./rimworld.db"
```

---

### `add-mod`

Add a mod's code and Defs to an existing database. Idempotent — re-running replaces previous data for the same mod.

```bash
rim-analyzer add-mod --mod-path <path> --db <path> --game-path <path> [--verbose]
```

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--mod-path` | path | ✅ | Mod root directory |
| `--db` | path | ✅ | Existing database path |
| `--game-path` | path | ✅ | RimWorld game root (for reference DLLs) |
| `--verbose` | flag | | Enable verbose logging |

**What it does:**
1. Reads `About/About.xml` to get mod name and packageId
2. Recursively finds all `.dll` files in the mod directory
3. Recursively finds all `.xml` files (filters to `<Defs>` root only)
4. Loads DLLs with game's Managed directory as reference
5. Extracts metadata + call graph (including cross-mod calls to Core)
6. Parses Defs and detects references
7. Writes to existing database with mod-specific source tracking

**Output (stdout):**
```json
{"status":"success","types":1258,"methods":5413,"calls":4253,"defs":8545}
```

**Example:**
```bash
rim-analyzer add-mod \
  --mod-path "D:\Steam\steamapps\workshop\content\294100\2890901044" \
  --db "./rimworld.db" \
  --game-path "D:\Steam\steamapps\common\RimWorld"
```

---

### `remove-mod`

Remove all data associated with a mod from the database.

```bash
rim-analyzer remove-mod --name <name> --db <path>
```

| Option | Type | Required | Description |
|--------|------|----------|-------------|
| `--name` | string | ✅ | Name of the mod to remove (as shown in About.xml) |
| `--db` | path | ✅ | Database path |

**Output (stdout):**
```json
{"status":"success","removed":"Combat Extended"}
```

**Example:**
```bash
rim-analyzer remove-mod --name "Combat Extended" --db "./rimworld.db"
```

---

## Planned Commands (Not Yet Implemented)

### `decompile`

On-demand decompilation of a type or method using ICSharpCode.Decompiler.

```bash
rim-analyzer decompile --assembly <path> --type <name> [--method <name>] [--references <path>...]
```

### `harmony`

Analyze Harmony patches declared in a mod's DLL.

```bash
rim-analyzer harmony --mod-path <path> [--references <path>...]
```

---

## Output Conventions

- **stdout**: JSON result (machine-readable)
- **stderr**: Progress logs and warnings (human-readable)
- **Exit code**: 0 = success, non-zero = failure

## Database Sources

Each entry in the database is tagged with a source:

| Source Type | Example Name | Created By |
|-------------|-------------|------------|
| `core` | RimWorld Core | `build` |
| `dlc` | Royalty, Biotech, ... | `build` |
| `mod` | Combat Extended | `add-mod` |
