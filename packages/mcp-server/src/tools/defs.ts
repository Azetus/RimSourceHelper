import type { Config } from "../config.js";

// search_defs: 按名称搜索 Def
export async function searchDefs(args: Record<string, unknown>, config: Config) {
  const query = args.query as string;
  const defType = args.def_type as string | undefined;
  const limit = (args.limit as number) ?? 100;

  // TODO: SQLite 查询 Defs 表
  return { content: [{ type: "text" as const, text: `TODO: search_defs(query="${query}", def_type=${defType}, limit=${limit})` }] };
}

// get_def_details: 获取 Def 完整信息（含 RawXml）
export async function getDefDetails(args: Record<string, unknown>, config: Config) {
  const defName = args.def_name as string;
  const defType = args.def_type as string | undefined;

  // TODO: SQLite 精确查询 Defs 表
  return { content: [{ type: "text" as const, text: `TODO: get_def_details(def_name="${defName}", def_type=${defType})` }] };
}

// list_def_types: 列出所有 Def 类型及数量
export async function listDefTypes(args: Record<string, unknown>, config: Config) {
  // TODO: SQLite GROUP BY DefType
  return { content: [{ type: "text" as const, text: "TODO: list_def_types()" }] };
}

// find_def_references: 查找引用指定 Def 的其他 Def
export async function findDefReferences(args: Record<string, unknown>, config: Config) {
  const defName = args.def_name as string;

  // TODO: SQLite 查询 DefReferences JOIN Defs
  return { content: [{ type: "text" as const, text: `TODO: find_def_references(def_name="${defName}")` }] };
}
