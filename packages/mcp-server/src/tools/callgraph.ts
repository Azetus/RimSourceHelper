import type { DatabaseSync, StatementSync } from "node:sqlite";
import type { Config } from "../config.js";
import type { MethodReference, FieldAccessEntry, CallTreeNode } from "../types.js";
import { withDatabase } from "../utils/database.js";
import { formatMethodList, formatCallTree, formatFieldAccesses } from "../utils/formatter.js";

const MAX_CHILDREN_PER_NODE = 20;

// get_callers: 查找直接调用方（一层）— 自动检测方法/字段/属性
export async function getCallers(args: Record<string, unknown>, config: Config) {
  const method = args.method as string;
  const limit = (args.limit as number) ?? 50;
  const offset = (args.offset as number) ?? 0;

  const result = withDatabase(config.databasePath, (db): MethodReference[] | FieldAccessEntry[] | string => {
    // 尝试作为方法/属性查找
    const ids = resolveMethodIds(db, method);
    if (ids.length > 0) {
      const placeholders = ids.map(() => "?").join(",");
      return db.prepare(
        `SELECT DISTINCT m.FullName, m.Signature, m.ReturnType, s.Name as Source
         FROM Methods m JOIN Calls c ON m.Id = c.CallerMethodId JOIN Sources s ON m.SourceId = s.Id
         WHERE c.CalleeMethodId IN (${placeholders}) ORDER BY m.FullName LIMIT ? OFFSET ?`
      ).all(...ids, limit, offset) as unknown as MethodReference[];
    }

    // 尝试作为字段查找
    const fieldId = resolveFieldId(db, method);
    if (fieldId !== null) {
      return db.prepare(
        `SELECT DISTINCT m.FullName as MethodFullName, m.Signature as MethodSignature, t.FullName as TypeFullName, f.Name, f.FieldType, fa.AccessType, s.Name as Source
         FROM FieldAccesses fa
         JOIN Methods m ON fa.MethodId = m.Id
         JOIN Fields f ON fa.FieldId = f.Id
         JOIN Types t ON f.TypeId = t.Id
         JOIN Sources s ON m.SourceId = s.Id
         WHERE fa.FieldId = ? ORDER BY m.FullName LIMIT ? OFFSET ?`
      ).all(fieldId, limit, offset) as unknown as FieldAccessEntry[];
    }

    return `Method or field not found: ${method}`;
  });

  if (typeof result === "string") {
    return { content: [{ type: "text" as const, text: result }], isError: true };
  }

  // 判断结果类型：有 AccessType 字段 → 字段访问；有 Signature 字段 → 方法引用
  if (result.length > 0 && "AccessType" in result[0]) {
    return { content: [{ type: "text" as const, text: formatFieldAccesses(result as FieldAccessEntry[], `## References to ${method} (field) (${result.length})`) }] };
  }

  return { content: [{ type: "text" as const, text: formatMethodList(result as MethodReference[], `## Callers of ${method} (${result.length})`) }] };
}

// get_callees: 查找直接被调用方（一层）
export async function getCallees(args: Record<string, unknown>, config: Config) {
  const method = args.method as string;
  const limit = (args.limit as number) ?? 50;
  const offset = (args.offset as number) ?? 0;
  const includeFieldAccess = (args.include_field_access as boolean) ?? false;

  const result = withDatabase(config.databasePath, (db): { callees: MethodReference[]; fieldAccesses: FieldAccessEntry[] } | string => {
    const ids = resolveMethodIds(db, method);
    if (ids.length === 0) return `Method not found: ${method}`;

    const placeholders = ids.map(() => "?").join(",");
    const callees = db.prepare(
      `SELECT DISTINCT m.FullName, m.Signature, m.ReturnType, s.Name as Source
       FROM Methods m JOIN Calls c ON m.Id = c.CalleeMethodId JOIN Sources s ON m.SourceId = s.Id
       WHERE c.CallerMethodId IN (${placeholders}) ORDER BY m.FullName LIMIT ? OFFSET ?`
    ).all(...ids, limit, offset) as unknown as MethodReference[];

    const fieldAccesses: FieldAccessEntry[] = [];
    if (includeFieldAccess) {
      fieldAccesses.push(...db.prepare(
        `SELECT DISTINCT m.FullName as MethodFullName, m.Signature as MethodSignature, t.FullName as TypeFullName, f.Name, f.FieldType, fa.AccessType, s.Name as Source
         FROM FieldAccesses fa
         JOIN Methods m ON fa.MethodId = m.Id
         JOIN Fields f ON fa.FieldId = f.Id
         JOIN Types t ON f.TypeId = t.Id
         JOIN Sources s ON m.SourceId = s.Id
         WHERE fa.MethodId IN (${placeholders})
         ORDER BY fa.AccessType, t.FullName, f.Name`
      ).all(...ids) as unknown as FieldAccessEntry[]);
    }

    return { callees, fieldAccesses };
  });

  if (typeof result === "string") {
    return { content: [{ type: "text" as const, text: result }], isError: true };
  }

  const lines: string[] = [];
  lines.push(formatMethodList(result.callees, `## Callees of ${method} (${result.callees.length})`));
  if (result.fieldAccesses.length > 0) {
    lines.push("");
    lines.push(formatFieldAccesses(result.fieldAccesses, `## Field Accesses (${result.fieldAccesses.length})`));
  }
  return { content: [{ type: "text" as const, text: lines.join("\n") }] };
}

// get_call_tree: 递归调用树（含环检测）— 仅支持方法（不支持字段）
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

  const tree = result[0];
  const title = `## Call Tree: ${method} → ${direction} (depth ${maxDepth})`;
  return { content: [{ type: "text" as const, text: formatCallTree(tree, title) }] };
}

// --- 内部辅助 ---

function resolveMethodIds(db: DatabaseSync, method: string): number[] {
  const byFullName = db.prepare("SELECT Id FROM Methods WHERE FullName = ?").all(method) as unknown as { Id: number }[];
  if (byFullName.length > 0) return byFullName.map(r => r.Id);

  const bySignature = db.prepare("SELECT Id FROM Methods WHERE Signature = ?").get(method) as unknown as { Id: number } | undefined;
  if (bySignature) return [bySignature.Id];

  const lastDot = method.lastIndexOf(".");
  if (lastDot > 0) {
    const typeName = method.substring(0, lastDot);
    const memberName = method.substring(lastDot + 1);
    const accessor = db.prepare(
      `SELECT m.Id FROM Methods m JOIN Types t ON m.TypeId = t.Id
       WHERE t.FullName = ? AND (m.Name = ? OR m.Name = ?)`
    ).all(typeName, `get_${memberName}`, `set_${memberName}`) as unknown as { Id: number }[];
    if (accessor.length > 0) return accessor.map(r => r.Id);
  }

  return [];
}

function resolveFieldId(db: DatabaseSync, target: string): number | null {
  const lastDot = target.lastIndexOf(".");
  if (lastDot <= 0) return null;
  const typeName = target.substring(0, lastDot);
  const fieldName = target.substring(lastDot + 1);

  const field = db.prepare(
    "SELECT f.Id FROM Fields f JOIN Types t ON f.TypeId = t.Id WHERE t.FullName = ? AND f.Name = ?"
  ).get(typeName, fieldName) as { Id: number } | undefined;
  return field?.Id ?? null;
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
    return { Method: info.FullName, Signature: info.Signature, IsCycle: true, Children: [] };
  }

  if (depth >= maxDepth) {
    return { Method: info.FullName, Signature: info.Signature, Children: [] };
  }

  const newPath = new Set(pathVisited);
  newPath.add(methodId);

  const neighbors = neighborStmt.all(methodId) as unknown as { Id: number; FullName: string; Signature: string }[];
  const node: CallTreeNode = { Method: info.FullName, Signature: info.Signature, Children: [] };

  const limit = Math.min(neighbors.length, MAX_CHILDREN_PER_NODE);
  for (let i = 0; i < limit; i++) {
    node.Children.push(buildTreeNode(neighbors[i].Id, maxDepth, depth + 1, newPath, neighborStmt, infoStmt));
  }

  if (neighbors.length > MAX_CHILDREN_PER_NODE) {
    node.Truncated = neighbors.length - MAX_CHILDREN_PER_NODE;
  }

  return node;
}
