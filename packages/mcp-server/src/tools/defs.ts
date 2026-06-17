import type { Config } from "../config.js";
import type { DefSummary, DefDetails, DefTypeCount } from "../types.js";
import { withDatabase } from "../utils/database.js";
import { formatDefList, formatDefDetails, formatDefTypes } from "../utils/formatter.js";

// search_defs: 按名称搜索 Def，返回摘要列表
export async function searchDefs(args: Record<string, unknown>, config: Config) {
  const query = args.query as string;
  const defType = args.def_type as string | undefined;
  const limit = (args.limit as number) ?? 100;

  const defs = withDatabase(config.databasePath, (db) => {
    let sql = `SELECT d.DefName, d.DefType, d.Label, d.IsAbstract, d.ParentDef, s.Name as source
               FROM Defs d JOIN Sources s ON d.SourceId = s.Id WHERE 1=1`;
    const params: (string | number)[] = [];

    if (query) {
      sql += " AND d.DefName LIKE ?";
      params.push(`%${query}%`);
    }
    if (defType) {
      sql += " AND d.DefType = ?";
      params.push(defType);
    }

    sql += " ORDER BY d.DefType, d.DefName LIMIT ?";
    params.push(limit);

    return db.prepare(sql).all(...params) as unknown as DefSummary[];
  });

  const title = query
    ? `## Defs matching "${query}" (${defs.length})`
    : defType ? `## ${defType} Defs (${defs.length})` : `## All Defs (${defs.length})`;
  return { content: [{ type: "text" as const, text: formatDefList(defs, title) }] };
}

// get_def_details: 获取 Def 完整信息（含 RawXml）
export async function getDefDetails(args: Record<string, unknown>, config: Config) {
  const defName = args.def_name as string;
  const defType = args.def_type as string | undefined;

  const def = withDatabase(config.databasePath, (db) => {
    const sql = defType
      ? `SELECT d.*, s.Name as source FROM Defs d JOIN Sources s ON d.SourceId = s.Id WHERE d.DefName = ? AND d.DefType = ?`
      : `SELECT d.*, s.Name as source FROM Defs d JOIN Sources s ON d.SourceId = s.Id WHERE d.DefName = ?`;
    const params = defType ? [defName, defType] : [defName];
    return db.prepare(sql).get(...params) as unknown as DefDetails | undefined;
  });

  if (!def) {
    return { content: [{ type: "text" as const, text: `Def not found: ${defName}` }], isError: true };
  }

  return { content: [{ type: "text" as const, text: formatDefDetails(def) }] };
}

// list_def_types: 列出所有 Def 类型及数量
export async function listDefTypes(args: Record<string, unknown>, config: Config) {
  const types = withDatabase(config.databasePath, (db) => {
    return db.prepare(
      "SELECT DefType, COUNT(*) as count FROM Defs GROUP BY DefType ORDER BY count DESC"
    ).all() as unknown as DefTypeCount[];
  });

  return { content: [{ type: "text" as const, text: formatDefTypes(types) }] };
}

// find_def_references: 查找引用指定 Def 的其他 Def
export async function findDefReferences(args: Record<string, unknown>, config: Config) {
  const defName = args.def_name as string;
  const limit = (args.limit as number) ?? 50;

  const refs = withDatabase(config.databasePath, (db) => {
    return db.prepare(
      `SELECT d.DefName, d.DefType, d.Label, s.Name as source
       FROM Defs d
       JOIN DefReferences r ON d.Id = r.SourceDefId
       JOIN Sources s ON d.SourceId = s.Id
       WHERE r.TargetDefName = ?
       ORDER BY d.DefType, d.DefName LIMIT ?`
    ).all(defName, limit) as unknown as DefSummary[];
  });

  return { content: [{ type: "text" as const, text: formatDefList(refs, `## Defs referencing "${defName}" (${refs.length})`) }] };
}
