import type Database from "better-sqlite3";
import type { Config } from "../config.js";
import { withDatabase } from "../utils/database.js";

const MAX_CHILDREN_PER_NODE = 20;

// get_callers: 查找直接调用方（一层）
export async function getCallers(args: Record<string, unknown>, config: Config) {
  const method = args.method as string;

  return withDatabase(config.databasePath, (db) => {
    const ids = resolveMethodIds(db, method);
    if (ids.length === 0)
      return { content: [{ type: "text" as const, text: `Method not found: ${method}` }], isError: true };

    const placeholders = ids.map(() => "?").join(",");
    const callers = db.prepare(
      `SELECT DISTINCT m.FullName, m.Signature, m.ReturnType, s.Name as source
       FROM Methods m
       JOIN Calls c ON m.Id = c.CallerMethodId
       JOIN Sources s ON m.SourceId = s.Id
       WHERE c.CalleeMethodId IN (${placeholders})
       ORDER BY m.FullName`
    ).all(...ids);

    return { content: [{ type: "text" as const, text: JSON.stringify(callers, null, 2) }] };
  });
}

// get_callees: 查找直接被调用方（一层）
export async function getCallees(args: Record<string, unknown>, config: Config) {
  const method = args.method as string;

  return withDatabase(config.databasePath, (db) => {
    const ids = resolveMethodIds(db, method);
    if (ids.length === 0)
      return { content: [{ type: "text" as const, text: `Method not found: ${method}` }], isError: true };

    const placeholders = ids.map(() => "?").join(",");
    const callees = db.prepare(
      `SELECT DISTINCT m.FullName, m.Signature, m.ReturnType, s.Name as source
       FROM Methods m
       JOIN Calls c ON m.Id = c.CalleeMethodId
       JOIN Sources s ON m.SourceId = s.Id
       WHERE c.CallerMethodId IN (${placeholders})
       ORDER BY m.FullName`
    ).all(...ids);

    return { content: [{ type: "text" as const, text: JSON.stringify(callees, null, 2) }] };
  });
}

// get_call_tree: 递归调用树（含环检测）
export async function getCallTree(args: Record<string, unknown>, config: Config) {
  const method = args.method as string;
  const direction = args.direction as "callers" | "callees";
  const maxDepth = (args.max_depth as number) ?? 3;

  return withDatabase(config.databasePath, (db) => {
    const ids = resolveMethodIds(db, method);
    if (ids.length === 0)
      return { content: [{ type: "text" as const, text: `Method not found: ${method}` }], isError: true };

    // 预编译查询语句（递归中复用）
    const neighborStmt = direction === "callers"
      ? db.prepare(
          `SELECT DISTINCT m.Id, m.FullName, m.Signature
           FROM Methods m JOIN Calls c ON m.Id = c.CallerMethodId
           WHERE c.CalleeMethodId = ?`)
      : db.prepare(
          `SELECT DISTINCT m.Id, m.FullName, m.Signature
           FROM Methods m JOIN Calls c ON m.Id = c.CalleeMethodId
           WHERE c.CallerMethodId = ?`);

    const infoStmt = db.prepare("SELECT FullName, Signature FROM Methods WHERE Id = ?");

    // 对所有匹配的方法 ID 构建树（处理重载）
    const trees = ids.map(id => buildTreeNode(id, maxDepth, 0, new Set<number>(), neighborStmt, infoStmt));

    const result = trees.length === 1 ? trees[0] : trees;
    return { content: [{ type: "text" as const, text: JSON.stringify(result, null, 2) }] };
  });
}

// --- 内部辅助 ---

interface TreeNode {
  method: string;
  signature: string;
  isCycle?: boolean;
  truncated?: number;
  children: TreeNode[];
}

// 将方法名（FullName 或 Signature）解析为数据库 ID 列表
function resolveMethodIds(db: Database.Database, method: string): number[] {
  // 优先 FullName 匹配（可能有多个重载）
  const byFullName = db.prepare("SELECT Id FROM Methods WHERE FullName = ?").all(method) as { Id: number }[];
  if (byFullName.length > 0) return byFullName.map(r => r.Id);

  // 尝试 Signature 精确匹配
  const bySignature = db.prepare("SELECT Id FROM Methods WHERE Signature = ?").get(method) as { Id: number } | undefined;
  if (bySignature) return [bySignature.Id];

  return [];
}

// 递归构建调用树节点
function buildTreeNode(
  methodId: number,
  maxDepth: number,
  depth: number,
  pathVisited: Set<number>,
  neighborStmt: Database.Statement,
  infoStmt: Database.Statement
): TreeNode {
  const info = infoStmt.get(methodId) as { FullName: string; Signature: string };

  // 路径级环检测
  if (pathVisited.has(methodId)) {
    return { method: info.FullName, signature: info.Signature, isCycle: true, children: [] };
  }

  // 达到最大深度
  if (depth >= maxDepth) {
    return { method: info.FullName, signature: info.Signature, children: [] };
  }

  // 标记当前路径
  const newPath = new Set(pathVisited);
  newPath.add(methodId);

  // 查询邻居
  const neighbors = neighborStmt.all(methodId) as { Id: number; FullName: string; Signature: string }[];

  const node: TreeNode = { method: info.FullName, signature: info.Signature, children: [] };

  // 截断过多子节点
  const limit = Math.min(neighbors.length, MAX_CHILDREN_PER_NODE);
  for (let i = 0; i < limit; i++) {
    node.children.push(buildTreeNode(neighbors[i].Id, maxDepth, depth + 1, newPath, neighborStmt, infoStmt));
  }

  if (neighbors.length > MAX_CHILDREN_PER_NODE) {
    node.truncated = neighbors.length - MAX_CHILDREN_PER_NODE;
  }

  return node;
}
