import type { Config } from "../config.js";
import type { HarmonyPatchResult } from "../types.js";
import { withDatabase } from "../utils/database.js";
import { formatPatchList } from "../utils/formatter.js";

// find_harmony_patches: 按目标类型/方法查找 Harmony Patches
export async function findHarmonyPatches(args: Record<string, unknown>, config: Config) {
  const targetType = args.target_type as string | undefined;
  const targetMethod = args.target_method as string | undefined;

  if (!targetType && !targetMethod) {
    return { content: [{ type: "text" as const, text: "At least one of target_type or target_method is required." }], isError: true };
  }

  const patches = withDatabase(config.databasePath, (db) => {
    let sql = `SELECT h.TargetType, h.TargetMethod, h.PatchType, h.PatchClass, h.PatchMethod, h.Priority, s.Name as Source
               FROM HarmonyPatches h JOIN Sources s ON h.SourceId = s.Id WHERE 1=1`;
    const params: (string | number)[] = [];

    if (targetType) {
      sql += " AND h.TargetType = ?";
      params.push(targetType);
    }
    if (targetMethod) {
      sql += " AND h.TargetMethod = ?";
      params.push(targetMethod);
    }

    sql += " ORDER BY h.TargetType, h.TargetMethod, s.Name";
    return db.prepare(sql).all(...params) as unknown as HarmonyPatchResult[];
  });

  const target = `${targetType ?? ""}${targetMethod ? "." + targetMethod : ""}`;
  return { content: [{ type: "text" as const, text: formatPatchList(patches, `## Harmony Patches on ${target} (${patches.length})`) }] };
}

// list_harmony_patches: 列出全部或按 Source 过滤的 Patches
export async function listHarmonyPatches(args: Record<string, unknown>, config: Config) {
  const source = args.source as string | undefined;
  const limit = (args.limit as number) ?? 100;

  const patches = withDatabase(config.databasePath, (db) => {
    const sql = source
      ? `SELECT h.TargetType, h.TargetMethod, h.PatchType, h.PatchClass, h.PatchMethod, h.Priority, s.Name as Source
         FROM HarmonyPatches h JOIN Sources s ON h.SourceId = s.Id
         WHERE s.Name = ?
         ORDER BY h.TargetType, h.TargetMethod LIMIT ?`
      : `SELECT h.TargetType, h.TargetMethod, h.PatchType, h.PatchClass, h.PatchMethod, h.Priority, s.Name as Source
         FROM HarmonyPatches h JOIN Sources s ON h.SourceId = s.Id
         ORDER BY s.Name, h.TargetType, h.TargetMethod LIMIT ?`;
    const params: (string | number)[] = source ? [source, limit] : [limit];
    return db.prepare(sql).all(...params) as unknown as HarmonyPatchResult[];
  });

  const title = source ? `## Harmony Patches from ${source} (${patches.length})` : `## All Harmony Patches (${patches.length})`;
  return { content: [{ type: "text" as const, text: formatPatchList(patches, title) }] };
}
