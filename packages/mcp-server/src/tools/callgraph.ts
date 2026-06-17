import type { DatabaseSync, StatementSync } from "node:sqlite";
import type { Config } from "../config.js";
import type { MethodReference, CallTreeNode } from "../types.js";
import { withDatabase } from "../utils/database.js";
import { formatMethodList, formatCallTree } from "../utils/formatter.js";

const MAX_CHILDREN_PER_NODE = 20;

// get_callers: 查找直接调用方（一层）
export async function getCallers(args: Record<string, unknown>, config: Config) {
  const method = args.method as string;
  const limit = (args.limit as number) ?? 50;

  const result = withDatabase(config.databasePath, (db): MethodReference[] | string => {
    const ids = resolveMethodIds(db, method);
    if (ids.length === 0) return `Method not found: ${method}`;

    const placeholders = ids.map(() => "?").join(",");
    return db.prepare(
      `SELECT DISTINCT m.FullName, m.Signature, m.ReturnType, s.Name as source
       FROM Methods m
       JOIN Calls c ON m.Id = c.CallerMethodId
       JOIN Sources s ON m.SourceId = s.Id
       WHERE c.CalleeMethodId IN (${placeholders})
       ORDER BY m.FullName LIMIT ?`
    ).all(...ids, limit) as unknown as MethodReference[];
  });

  if (typeof result === "string") {
    return { content: [{ type: "text" as const, text: result }], isError: true };
  }

  return { content: [{ type: "text" as const, text: formatMethodList(result, `## Callers of ${method} (${result.length})`) }] };
}

// get_callees: 查找直接被调用方（一层）
export async function getCallees(args: Record<string, unknown>, config: Config) {
  const method = args.method as string;
  const limit = (args.limit as number) ?? 50;

  const result = withDatabase(config.databasePath, (db): MethodReference[] | string => {
    const ids = resolveMethodIds(db, method);
    if (ids.length === 0) return `Method not found: ${method}`;

    const placeholders = ids.map(() => "?").join(",");
    return db.prepare(
      `SELECT DISTINCT m.FullName, m.Signature, m.ReturnType, s.Name as source
       FROM Methods m
       JOIN Calls c ON m.Id = c.CalleeMethodId
       JOIN Sources s ON m.SourceId = s.Id
       WHERE c.CallerMethodId IN (${placeholders})
       ORDER BY m.FullName LIMIT ?`
    ).all(...ids, limit) as unknown as MethodReference[];
  });

  if (typeof result === "string") {
    return { content: [{ type: "text" as const, text: result }], isError: true };
  }

  return { content: [{ type: "text" as const, text: formatMethodList(result, `## Callees of ${method} (${result.length})`) }] };
}

// get_call_tree: 递归调用树（含环检测）
export async function getCallTree(args: Record<string, unknown>, config: Config) {
  const method = args.method as string;
  const direction = args.direction as "callers" | "callees";
  const maxDepth = (args.max_depth as number) ?? 3;

  const result = withDatabase(config.databasePath, (db): CallTreeNode[] | string => {
    const ids = resolveMethodIds(db, method);
    if (ids.length === 0) return `Method not found: ${method}`;

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

    return ids.map(id => buildTreeNode(id, maxDepth, 0, new Set<number>(), neighborStmt, infoStmt));
  });

  if (typeof result === "string") {
    return { content: [{ type: "text" as const, text: result }], isError: true };
  }

  const tree = result.length === 1 ? result[0] : result[0];
  const title = `## Call Tree: ${method} → ${direction} (depth ${maxDepth})`;
  return { content: [{ type: "text" as const, text: formatCallTree(tree, title) }] };
}

// --- 内部辅助 ---

function resolveMethodIds(db: DatabaseSync, method: string): number[] {
  const byFullName = db.prepare("SELECT Id FROM Methods WHERE FullName = ?").all(method) as unknown as { Id: number }[];
  if (byFullName.length > 0) return byFullName.map(r => r.Id);

  const bySignature = db.prepare("SELECT Id FROM Methods WHERE Signature = ?").get(method) as unknown as { Id: number } | undefined;
  if (bySignature) return [bySignature.Id];

  return [];
}

function buildTreeNode(
  methodId: number,
  maxDepth: number,
  depth: number,
  pathVisited: Set<number>,
  neighborStmt: StatementSync,
  infoStmt: StatementSync
): CallTreeNode {
  const info = infoStmt.get(methodId) as unknown as { FullName: string; Signature: string };

  if (pathVisited.has(methodId)) {
    return { method: info.FullName, signature: info.Signature, isCycle: true, children: [] };
  }

  if (depth >= maxDepth) {
    return { method: info.FullName, signature: info.Signature, children: [] };
  }

  const newPath = new Set(pathVisited);
  newPath.add(methodId);

  const neighbors = neighborStmt.all(methodId) as unknown as { Id: number; FullName: string; Signature: string }[];
  const node: CallTreeNode = { method: info.FullName, signature: info.Signature, children: [] };

  const limit = Math.min(neighbors.length, MAX_CHILDREN_PER_NODE);
  for (let i = 0; i < limit; i++) {
    node.children.push(buildTreeNode(neighbors[i].Id, maxDepth, depth + 1, newPath, neighborStmt, infoStmt));
  }

  if (neighbors.length > MAX_CHILDREN_PER_NODE) {
    node.truncated = neighbors.length - MAX_CHILDREN_PER_NODE;
  }

  return node;
}
