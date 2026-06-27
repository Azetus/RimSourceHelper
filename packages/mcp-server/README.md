# mcp-server

RimWorld knowledge base MCP server. Provides 19 tools for AI coding assistants to query types, methods, call graphs, Defs, Harmony patches, XML patches, and decompiled source code.

## Integration

Add to your `opencode.json`:
```json
{
  "mcp": {
    "RimSourceHelper": {
      "type": "local",
      "command": ["node", "<absolute-path-to>/release/index.js"],
      "enabled": true
    }
  }
}
```

## Tools

### `find_target`

Search types, methods, fields, or properties by name. Supports kind filter and relevance ranking.

```
find_target(query: "isFighter", kind: "field", limit: 3)
```
```
## Fields (1)
- `Verse.PawnKindDef.isFighter` : System.Boolean — RimWorld Core
```

```
find_target(query: "Pawn", kind: "type", limit: 3)
```
```
## Types (3)
- `Verse.Pawn` — RimWorld Core
- `RimWorld.PawnActivityWorker` — RimWorld Core
- `RimWorld.PawnAddictionHediffsGenerator` — RimWorld Core
```

---

### `get_target_info`

Full metadata for a type or method: inheritance, callers/callees, Harmony patches.

```
get_target_info(target: "Verse.Pawn")
```
```
# Verse.Pawn
- Modifiers: public
- Base: `Verse.ThingWithComps`
- Implements: `Verse.IStrippable`, `RimWorld.IBillGiver`, `Verse.IVerbOwner`, ...
- Source: RimWorld Core (core)
- Members: 136 methods, 97 fields, 118 properties

## Harmony Patches (1)
- **Prefix** on `PreApplyDamage` by `VMM_VanillaMeleeModes.Patches.Patch_Pawn_PreApplyDamage.Prefix` — Vanilla Melee Modes
```

```
get_target_info(target: "Verse.Verb.TryStartCastOn")
```
```
# Verse.Verb.TryStartCastOn
- Signature: `System.Boolean Verse.Verb.TryStartCastOn(Verse.LocalTargetInfo,...)`
- Modifiers: public
- Returns: `System.Boolean`
- Parent: `Verse.Verb`
- Source: RimWorld Core (core)
- Overloads: `System.Boolean Verse.Verb.TryStartCastOn(Verse.LocalTargetInfo,Verse.LocalTargetInfo,...)`

## Callers (14)
- `System.Boolean Verse.Pawn.TryStartAttack(Verse.LocalTargetInfo)` — RimWorld Core
- `System.Boolean RimWorld.Pawn_MeleeVerbs.TryMeleeAttack(...)` — RimWorld Core
- ...

## Callees (14)
- `System.Void Verse.Verb.WarmupComplete()` — RimWorld Core
- `System.Boolean Verse.Verb.CanHitTarget(Verse.LocalTargetInfo)` — RimWorld Core
- ...

## Harmony Patches (1)
- **Prefix** (Verse.LocalTargetInfo,Verse.LocalTargetInfo,...) `VFM_VanillaFireModes.Patches.Patch_TryStartCastOn.Prefix` — Vanilla Fire Modes
```

---

### `list_type_members`

List methods, fields, or properties of a type.

```
list_type_members(type_name: "Verse.Verb", kind: "methods")
```
```
# Verse.Verb

## Methods (37)
- `IsStillUsableBy` public → System.Boolean
- `TryStartCastOn` public → System.Boolean
- `TryStartCastOn` public virtual → System.Boolean
- `WarmupComplete` public virtual → System.Void
- `TryCastShot` protected virtual abstract → System.Boolean
- `CanHitTarget` public virtual → System.Boolean
- ...
```

---

### `get_callers`

Find callers of a method, field, or property. Auto-detects target type.

```
get_callers(method: "Verse.Verb.WarmupComplete", limit: 3)
```
```
## Callers of Verse.Verb.WarmupComplete (3)
- `System.Void Verse.Stance_Warmup.Expire()` — RimWorld Core
- `System.Boolean Verse.Verb.TryStartCastOn(...)` — RimWorld Core
- `System.Void Verse.Verb_LaunchProjectile.WarmupComplete()` — RimWorld Core
```

Field reference tracking:
```
get_callers(method: "Verse.PawnKindDef.isFighter", limit: 5)
```
```
## References to Verse.PawnKindDef.isFighter (field) (5)
- `RimWorld.MechClusterGenerator.MechKindSuitableForCluster` → `Verse.PawnKindDef.isFighter` [read]
- `RimWorld.PawnGroupMakerUtility.PawnGenOptionValid` → `Verse.PawnKindDef.isFighter` [read]
- `RimWorld.PawnGroupKindWorker_Normal.MinPointsToGenerateAnything` → `Verse.PawnKindDef.isFighter` [read]
- `RimWorld.ComplexThreatWorker_SleepingMechanoids.MechKindSuitableForComplex` → `Verse.PawnKindDef.isFighter` [read]
- `Verse.PawnKindDef..ctor` → `Verse.PawnKindDef.isFighter` [write]
```

---

### `get_callees`

Direct callees of a method. Set `include_field_access=true` to also see field/property accesses.

```
get_callees(method: "Verse.Verb.WarmupComplete", include_field_access: true)
```
```
## Callees of Verse.Verb.WarmupComplete (2)
- `System.Void Verse.Verb.TryCastNextBurstShot()` — RimWorld Core
- `System.Int32 Verse.Verb.get_ShotsPerBurst()` — RimWorld Core

## Field Accesses (2)
- `Verse.Verb.WarmupComplete` → `Verse.Verb.burstShotsLeft` [write] : System.Int32 — RimWorld Core
- `Verse.Verb.WarmupComplete` → `Verse.Verb.state` [write] : Verse.VerbState — RimWorld Core
```

---

### `get_call_tree`

Recursive call tree with cycle detection. Supports methods only.

```
get_call_tree(method: "Verse.Pawn.Kill", direction: "callees", max_depth: 2)
```
```
## Call Tree: Verse.Pawn.Kill → callees (depth 2)
Verse.Pawn.Kill
├── MechanitorUtility.IsMechanitor
│   └── MechanitorUtility.ShouldBeMechanitor
├── Verse.Pawn.DoKillSideEffects
│   ├── Verse.BattleLogEntry_StateTransition..ctor
│   ├── RimWorld.HistoryEventsManager.RecordEvent
│   └── RimWorld.TaleUtility.Notify_PawnDied
├── Verse.Pawn.PreDeathPawnModifications
│   └── RimWorld.Pawn_RelationsTracker.Notify_PawnKilled
├── Verse.Pawn.MakeCorpse
├── Verse.Pawn_HealthTracker.SetDead
│   └── Verse.Log.Error
└── ... (55 more)
```

---

### `search_defs`

Search Defs by name, or browse all Defs of a type with empty query.

```
search_defs(query: "MeleeWeapon_Knife", limit: 3)
```
```
## Defs matching "MeleeWeapon_Knife" (1)
- **MeleeWeapon_Knife** (ThingDef) "knife" — RimWorld Core
```

---

### `get_def_details`

Full Def details with raw XML.

```
get_def_details(def_name: "MeleeWeapon_Knife")
```
````
# MeleeWeapon_Knife (ThingDef)
- Label: knife
- Description: One of mankind's oldest manufactured objects...
- Parent: BaseMeleeWeapon_Sharp_Quality
- Source: RimWorld Core
- File: Data/Core/Defs/ThingDefs_Misc/Weapons/MeleeNeolithic.xml

## XML
```xml
<ThingDef ParentName="BaseMeleeWeapon_Sharp_Quality">
  <defName>MeleeWeapon_Knife</defName>
  <label>knife</label>
  <tools>
    <li><label>blade</label><capacities><li>Cut</li></capacities>...</li>
    ...
  </tools>
</ThingDef>
```
````

---

### `list_def_types`

All Def types with counts.

```
list_def_types()
```
```
## Def Types (266)
- ThingDef: 6992
- RecipeDef: 1770
- SoundDef: 1256
- ...
```

---

### `find_def_references`

Find Defs that reference a target Def.

```
find_def_references(def_name: "ComponentIndustrial", limit: 5)
```
```
## Defs referencing "ComponentIndustrial" (5)
- **Sanguophage** (AbilityCategoryDef) — Biotech
- **Cyclops** (BodyDef) "cyclops" — Odyssey
- **Lancer** (BodyDef) "lancer" — RimWorld Core
- **Pikeman** (BodyDef) "pikeman" — RimWorld Core
- **Scorcher** (BodyDef) "scorcher" — Biotech
```

---

### `find_harmony_patches`

Find Harmony patches on a target type or method. Shows parameter types for overload disambiguation.

```
find_harmony_patches(target_type: "Verse.VerbProperties", target_method: "AdjustedArmorPenetration")
```
```
## Harmony Patches on Verse.VerbProperties.AdjustedArmorPenetration (2)
- **Postfix** on `Verse.VerbProperties.AdjustedArmorPenetration(Verse.Tool,Verse.Pawn,Verse.Thing,Verse.HediffComp_VerbGiver)` by `VMM_VanillaMeleeModes.Patches.Patch_ArmorPenetration.Patch_ArmorPenetration_1` — Vanilla Melee Modes
- **Postfix** on `Verse.VerbProperties.AdjustedArmorPenetration(Verse.Tool,Verse.Pawn,Verse.ThingDef,Verse.ThingDef,Verse.HediffComp_VerbGiver)` by `VMM_VanillaMeleeModes.Patches.Patch_ArmorPenetration.Patch_ArmorPenetration_2` — Vanilla Melee Modes
```

---

### `list_harmony_patches`

List all patches from a specific mod.

```
list_harmony_patches(source: "Vanilla Melee Modes", limit: 5)
```
```
## Harmony Patches from Vanilla Melee Modes (4)
- **Postfix** on `Verse.AI.Pawn_JobTracker.StartJob` by `VMM_VanillaMeleeModes.Patches.Patch_AutoMode_OnPlayerMeleeJob.Postfix` — Vanilla Melee Modes
- **Prefix** on `Verse.Pawn.PreApplyDamage` by `VMM_VanillaMeleeModes.Patches.Patch_Pawn_PreApplyDamage.Prefix` — Vanilla Melee Modes
- **Postfix** on `Verse.VerbProperties.AdjustedArmorPenetration(Verse.Tool,Verse.Pawn,Verse.Thing,Verse.HediffComp_VerbGiver)` by `VMM_VanillaMeleeModes.Patches.Patch_ArmorPenetration.Patch_ArmorPenetration_1` — Vanilla Melee Modes
- **Postfix** on `Verse.VerbProperties.AdjustedArmorPenetration(Verse.Tool,Verse.Pawn,Verse.ThingDef,Verse.ThingDef,Verse.HediffComp_VerbGiver)` by `VMM_VanillaMeleeModes.Patches.Patch_ArmorPenetration.Patch_ArmorPenetration_2` — Vanilla Melee Modes
```

---

### `decompile_target`

On-demand decompilation. Returns only source code. Use this to see field default values.

```
decompile_target(target: "Verse.Pawn.Kill")
```
```csharp
public override void Kill(DamageInfo? dinfo, Hediff exactCulprit = null)
{
    int num = 0;
    health.isBeingKilled = true;
    try
    {
        IntVec3 positionHeld = base.PositionHeld;
        Map map = base.Map;
        ...
        DoKillSideEffects(dinfo, exactCulprit, spawned);
        PreDeathPawnModifications(dinfo, map);
        DropBeforeDying(dinfo, ref map, ref spawned);
        health.SetDead();
        ...
    }
    catch (Exception arg)
    {
        Log.Error($"Error while killing {this.ToStringSafe()} during phase {num}: {arg}");
    }
}
```

---

### `list_xml_patches`

List XML Patches from a specific mod with pagination.

```
list_xml_patches(source: "RimWorld Core", limit: 3)
```
```
## XML Patches from RimWorld Core (page 1, total 0)
None.
```

---

### `find_xml_patches`

Find XML Patches by def_name. Set `include_raw=true` for full XML.

```
find_xml_patches(def_name: "MeleeWeapon_Mace", limit: 3)
```
```
## XML Patches referencing MeleeWeapon_Mace (0)
None.
```

---

### `build_database`

Build/rebuild knowledge database from game files. WARNING: clears ALL existing data.

```
build_database()
```
```json
{"status":"success","types":16157,"methods":90777,"calls":228834,"defs":13809}
```

---

### `add_mod`

Add a mod to the knowledge base. Idempotent (replaces existing data for the same mod).

```
add_mod(mod_path: "E:\\Code\\mod\\Rimworld\\Vanilla-Melee-Modes")
```
```json
{"status":"success","types":51,"methods":157,"calls":141,"defs":7}
```

---

### `remove_mod`

Remove a mod from the knowledge base. Idempotent (no-op if already removed).

```
remove_mod(mod_name: "Vanilla Melee Modes")
```
```json
{"status":"success","removed":"Vanilla Melee Modes"}
```

---

### `list_sources`

Show loaded sources.

```
list_sources()
```
```
## Sources (8)
- **RimWorld Core** (core) — Ludeon.RimWorld
- **Anomaly** (dlc) — Ludeon.RimWorld.Anomaly
- **Biotech** (dlc) — Ludeon.RimWorld.Biotech
- **Ideology** (dlc) — Ludeon.RimWorld.Ideology
- **Odyssey** (dlc) — Ludeon.RimWorld.Odyssey
- **Royalty** (dlc) — Ludeon.RimWorld.Royalty
- **Vanilla Fire Modes** (mod) — Aliza.VanillaFireModes
- **Vanilla Melee Modes** (mod) — Aliza.VanillaMeleeModes
```

## Output Format

- **Query tools** return LLM-friendly Markdown
- **Management tools** (`build_database`, `add_mod`, `remove_mod`) return JSON
- **Errors** return plain text with `isError: true`
