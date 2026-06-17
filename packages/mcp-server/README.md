# mcp-server

RimWorld knowledge base MCP server. Provides 17 tools for AI coding assistants to query types, methods, call graphs, Defs, Harmony patches, and decompiled source code.

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

Fuzzy search types and methods with relevance ranking (exact → prefix → contains).

```
find_target(query: "Pawn", kind: "type", limit: 5)
```
```
## Types (5)
- `Verse.Pawn` — RimWorld Core
- `RimWorld.PawnActivityWorker` — RimWorld Core
- `RimWorld.PawnAddictionHediffsGenerator` — RimWorld Core
- `RimWorld.PawnApparelGenerator` — RimWorld Core
- `RimWorld.PawnAttackGizmoUtility` — RimWorld Core
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
- `System.Boolean RimWorld.Pawn_MeleeVerbs.TryMeleeAttack(Verse.Thing,Verse.Verb,System.Boolean)` — RimWorld Core
- `System.Void RimWorld.Building_TurretGun.BeginBurst()` — RimWorld Core
- ...

## Callees (14)
- `System.Void Verse.Verb.WarmupComplete()` — RimWorld Core
- `System.Boolean Verse.Verb.CanHitTarget(Verse.LocalTargetInfo)` — RimWorld Core
- `System.Boolean Verse.Verb.TryFindShootLineFromTo(Verse.IntVec3,Verse.LocalTargetInfo,ref Verse.ShootLine,System.Boolean)` — RimWorld Core
- ...

## Harmony Patches (1)
- **Prefix** (Verse.LocalTargetInfo,Verse.LocalTargetInfo,System.Boolean,System.Boolean,System.Boolean,System.Boolean) `VFM_VanillaFireModes.Patches.Patch_TryStartCastOn.Prefix` — Vanilla Fire Modes
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
- `TryCastNextBurstShot` protected → System.Void
- `TryCastShot` protected virtual abstract → System.Boolean
- `CanHitTarget` public virtual → System.Boolean
- `TryFindShootLineFromTo` public → System.Boolean
- ...
```

---

### `get_callers`

Direct callers of a method.

```
get_callers(method: "Verse.Verb.WarmupComplete", limit: 5)
```
```
## Callers of Verse.Verb.WarmupComplete (3)
- `System.Void Verse.Stance_Warmup.Expire()` — RimWorld Core
- `System.Boolean Verse.Verb.TryStartCastOn(Verse.LocalTargetInfo,Verse.LocalTargetInfo,System.Boolean,System.Boolean,System.Boolean,System.Boolean)` — RimWorld Core
- `System.Void Verse.Verb_LaunchProjectile.WarmupComplete()` — RimWorld Core
```

---

### `get_callees`

Direct callees of a method.

```
get_callees(method: "Verse.Pawn.Kill", limit: 5)
```
```
## Callees of Verse.Pawn.Kill (5)
- `System.Boolean MechanitorUtility.IsMechanitor(Verse.Pawn)` — RimWorld Core
- `System.Void RimWorld.BillUtility.Notify_ColonistUnavailable(Verse.Pawn)` — RimWorld Core
- `System.Void RimWorld.ColonistBar.MarkColonistsDirty()` — RimWorld Core
- `System.Void RimWorld.CompRottable.RotImmediately(RimWorld.RotStage)` — RimWorld Core
- `System.Void RimWorld.Faction.Notify_MemberDied(Verse.Pawn,...)` — RimWorld Core
```

---

### `get_call_tree`

Recursive call tree with cycle detection.

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
│   ├── RimWorld.RecordsUtility.Notify_PawnKilled
│   └── RimWorld.TaleUtility.Notify_PawnDied
├── Verse.Pawn.PreDeathPawnModifications
│   ├── RimWorld.BillStack.Clear
│   ├── RimWorld.Pawn_ApparelTracker.Notify_PawnKilled
│   └── RimWorld.Pawn_RelationsTracker.Notify_PawnKilled
├── Verse.Pawn.MakeCorpse
│   └── Verse.Pawn.MakeCorpse
├── Verse.Pawn_HealthTracker.SetDead
│   └── Verse.Log.Error
└── ... (55 more)
```

---

### `search_defs`

Search Defs by name.

```
search_defs(query: "MeleeWeapon", limit: 5)
```
```
## Defs matching "MeleeWeapon" (5)
- **MeleeWeapon_AverageArmorPenetration** (StatDef) "melee armor penetration" — RimWorld Core
- **MeleeWeapon_AverageDPS** (StatDef) "melee damage per second" — RimWorld Core
- **MeleeWeapon_CooldownMultiplier** (StatDef) "melee cooldown" — RimWorld Core
- **MeleeWeapon_DamageMultiplier** (StatDef) "melee damage multiplier" — RimWorld Core
- **BaseMeleeWeapon** (ThingDef) — RimWorld Core
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
    <li>
      <label>blade</label>
      <capacities><li>Cut</li></capacities>
      <power>12</power>
      <cooldownTime>1.5</cooldownTime>
    </li>
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
## Def Types (252)
- ThingDef: 2055
- SoundDef: 1233
- ThoughtDef: 934
- BackstoryDef: 845
- HediffDef: 346
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

Find Harmony patches on a target type or method. Includes parameter types for overload disambiguation.

```
find_harmony_patches(target_type: "Verse.Verb")
```
```
## Harmony Patches on Verse.Verb (2)
- **Prefix** on `Verse.Verb.TryStartCastOn(Verse.LocalTargetInfo,Verse.LocalTargetInfo,System.Boolean,System.Boolean,System.Boolean,System.Boolean)` by `VFM_VanillaFireModes.Patches.Patch_TryStartCastOn.Prefix` — Vanilla Fire Modes
- **Prefix** on `Verse.Verb.WarmupComplete` by `VFM_VanillaFireModes.Patches.Patch_BurstShotCount.LockCount` — Vanilla Fire Modes
```

---

### `list_harmony_patches`

List all patches from a specific mod. Parameter types shown when targeting overloaded methods.

```
list_harmony_patches(source: "Vanilla Melee Modes")
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

On-demand decompilation. Returns only source code.

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

### `build_database`

Build/rebuild knowledge database from game files. Paths from config.

```
build_database()
```
```json
{"status":"success","types":16157,"methods":79712,"calls":153596,"defs":13809}
```

---

### `add_mod`

Add a mod to the knowledge base. Idempotent.

```
add_mod(mod_path: "D:\\Steam\\steamapps\\common\\RimWorld\\Mods\\VanillaMeleeModes")
```
```json
{"status":"success","types":51,"methods":152,"calls":108,"defs":7}
```

---

### `remove_mod`

Remove a mod from the knowledge base. Idempotent.

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
