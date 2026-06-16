import type { Config } from "../config.js";
import { withDatabase } from "../utils/database.js";

// find_harmony_patches: 按目标类型/方法查找 Harmony Patches
export async function findHarmonyPatches(args: Record<string, unknown>, config: Config) {
  const targetType = args.target_type as string | undefined;
  const targetMethod = args.target_method as string | undefined;

  if (!targetType && !targetMethod) {
    return { content: [{ type: "text" as const, text: "At least one of target_type or target_method is required." }], isError: true };
  }

  return withDatabase(config.databasePath, (db) => {
    let sql = `SELECT h.TargetType, h.TargetMethod, h.PatchType, h.PatchClass, h.PatchMethod, h.Priority, s.Name as source
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
    const results = db.prepare(sql).all(...params);
    return { content: [{ type: "text" as const, text: JSON.stringify(results, null, 2) }] };
  });
}

// list_harmony_patches: 列出全部或按 Source 过滤的 Patches
export async function listHarmonyPatches(args: Record<string, unknown>, config: Config) {
  const source = args.source as string | undefined;

  return withDatabase(config.databasePath, (db) => {
    const sql = source
      ? `SELECT h.TargetType, h.TargetMethod, h.PatchType, h.PatchClass, h.PatchMethod, h.Priority, s.Name as source
         FROM HarmonyPatches h JOIN Sources s ON h.SourceId = s.Id
         WHERE s.Name = ?
         ORDER BY h.TargetType, h.TargetMethod`
      : `SELECT h.TargetType, h.TargetMethod, h.PatchType, h.PatchClass, h.PatchMethod, h.Priority, s.Name as source
         FROM HarmonyPatches h JOIN Sources s ON h.SourceId = s.Id
         ORDER BY s.Name, h.TargetType, h.TargetMethod`;
    const params = source ? [source] : [];

    const results = db.prepare(sql).all(...params);
    return { content: [{ type: "text" as const, text: JSON.stringify(results, null, 2) }] };
  });
}
