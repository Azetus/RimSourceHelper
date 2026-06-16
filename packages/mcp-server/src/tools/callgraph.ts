import type { Config } from "../config.js";

// get_callers: 查找直接调用方
export async function getCallers(args: Record<string, unknown>, config: Config) {
  const method = args.method as string;

  // TODO: SQLite 查询 Calls JOIN Methods
  return { content: [{ type: "text" as const, text: `TODO: get_callers(method="${method}")` }] };
}

// get_callees: 查找直接被调用方
export async function getCallees(args: Record<string, unknown>, config: Config) {
  const method = args.method as string;

  // TODO: SQLite 查询 Calls JOIN Methods
  return { content: [{ type: "text" as const, text: `TODO: get_callees(method="${method}")` }] };
}

// get_call_tree: 递归调用树（含环检测）
export async function getCallTree(args: Record<string, unknown>, config: Config) {
  const method = args.method as string;
  const direction = args.direction as string;
  const maxDepth = (args.max_depth as number) ?? 3;

  // TODO: 递归查询 Calls 表，维护路径集合做环检测
  return { content: [{ type: "text" as const, text: `TODO: get_call_tree(method="${method}", direction="${direction}", max_depth=${maxDepth})` }] };
}
