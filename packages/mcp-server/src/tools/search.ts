import type { Config } from "../config.js";

// find_target: 模糊搜索类型或方法
export async function findTarget(args: Record<string, unknown>, config: Config) {
  const query = args.query as string;
  const kind = args.kind as string | undefined;
  const source = args.source as string | undefined;

  // TODO: SQLite 查询 Types + Methods 表
  return { content: [{ type: "text" as const, text: `TODO: find_target(query="${query}", kind=${kind}, source=${source})` }] };
}

// get_target_info: 获取类型或方法的全量信息
export async function getTargetInfo(args: Record<string, unknown>, config: Config) {
  const target = args.target as string;
  const includeSource = args.include_source as boolean ?? false;

  // TODO: SQLite 多表联查 + 可选调用 rim-analyzer decompile
  return { content: [{ type: "text" as const, text: `TODO: get_target_info(target="${target}", include_source=${includeSource})` }] };
}

// list_type_members: 列出类型的成员
export async function listTypeMembers(args: Record<string, unknown>, config: Config) {
  const typeName = args.type_name as string;
  const kind = args.kind as string ?? "all";

  // TODO: SQLite 查询 Methods/Fields/Properties WHERE TypeId
  return { content: [{ type: "text" as const, text: `TODO: list_type_members(type_name="${typeName}", kind="${kind}")` }] };
}
