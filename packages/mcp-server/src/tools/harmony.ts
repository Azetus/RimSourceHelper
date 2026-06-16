import type { Config } from "../config.js";

// find_harmony_patches: 按目标查找 Harmony Patches
export async function findHarmonyPatches(args: Record<string, unknown>, config: Config) {
  const targetType = args.target_type as string | undefined;
  const targetMethod = args.target_method as string | undefined;

  // TODO: SQLite 查询 HarmonyPatches JOIN Sources
  return { content: [{ type: "text" as const, text: `TODO: find_harmony_patches(target_type=${targetType}, target_method=${targetMethod})` }] };
}

// list_harmony_patches: 列出全部或按 Source 过滤的 Patches
export async function listHarmonyPatches(args: Record<string, unknown>, config: Config) {
  const source = args.source as string | undefined;

  // TODO: SQLite 查询 HarmonyPatches，可选按 Source 过滤
  return { content: [{ type: "text" as const, text: `TODO: list_harmony_patches(source=${source})` }] };
}
