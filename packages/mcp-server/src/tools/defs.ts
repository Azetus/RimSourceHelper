import type { Config } from "../config.js";
import type { DefSummary, DefDetails, DefTypeCount, XmlPatchResult } from "../types.js";
import { withDatabase } from "../utils/database.js";
import { formatDefList, formatDefDetails, formatDefTypes, formatXmlPatchList, formatXmlPatchDetail } from "../utils/formatter.js";

// search_defs: 按名称搜索 Def，返回摘要列表
export async function searchDefs(args: Record<string, unknown>, config: Config) {
  const query = args.query as string;
  const defType = args.def_type as string | undefined;
  const limit = (args.limit as number) ?? 100;
  const offset = (args.offset as number) ?? 0;

  const defs = withDatabase(config.databasePath, (db) => {
    let sql = `SELECT d.DefName, d.DefType, d.Label, d.IsAbstract, d.ParentDef, s.Name as Source
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

    sql += " ORDER BY d.DefType, d.DefName LIMIT ? OFFSET ?";
    params.push(limit, offset);

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
      ? `SELECT d.*, s.Name as Source FROM Defs d JOIN Sources s ON d.SourceId = s.Id WHERE d.DefName = ? AND d.DefType = ?`
      : `SELECT d.*, s.Name as Source FROM Defs d JOIN Sources s ON d.SourceId = s.Id WHERE d.DefName = ?`;
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
      "SELECT DefType, COUNT(*) as Count FROM Defs GROUP BY DefType ORDER BY Count DESC"
    ).all() as unknown as DefTypeCount[];
  });

  return { content: [{ type: "text" as const, text: formatDefTypes(types) }] };
}

// find_def_references: 查找引用指定 Def 的其他 Def
export async function findDefReferences(args: Record<string, unknown>, config: Config) {
  const defName = args.def_name as string;
  const limit = (args.limit as number) ?? 50;
  const offset = (args.offset as number) ?? 0;

  const refs = withDatabase(config.databasePath, (db) => {
    return db.prepare(
      `SELECT d.DefName, d.DefType, d.Label, s.Name as Source
       FROM Defs d
       JOIN DefReferences r ON d.Id = r.SourceDefId
       JOIN Sources s ON d.SourceId = s.Id
       WHERE r.TargetDefName = ?
       ORDER BY d.DefType, d.DefName LIMIT ? OFFSET ?`
    ).all(defName, limit, offset) as unknown as DefSummary[];
  });

  return { content: [{ type: "text" as const, text: formatDefList(refs, `## Defs referencing "${defName}" (${refs.length})`) }] };
}

// list_xml_patches: 分页列出 Mod 的 XML Patches
export async function listXmlPatches(args: Record<string, unknown>, config: Config) {
  const source = args.source as string;
  const offset = (args.offset as number) ?? 0;
  const limit = (args.limit as number) ?? 50;

  return withDatabase(config.databasePath, (db) => {
    const sourceRow = db.prepare("SELECT Id, Name FROM Sources WHERE Name = ?").get(source) as { Id: number; Name: string } | undefined;
    if (!sourceRow) return { content: [{ type: "text" as const, text: `Source not found: ${source}` }], isError: true };

    const patches = db.prepare(
      `SELECT p.TargetXPaths, p.OperationClasses, p.SourceFile, s.Name as Source
       FROM XmlPatches p JOIN Sources s ON p.SourceId = s.Id
       WHERE p.SourceId = ? LIMIT ? OFFSET ?`
    ).all(sourceRow.Id, limit, offset) as unknown as XmlPatchResult[];

    const total = (db.prepare("SELECT COUNT(*) FROM XmlPatches WHERE SourceId = ?").get(sourceRow.Id) as { "COUNT(*)": number })["COUNT(*)"];

    return { content: [{ type: "text" as const, text: formatXmlPatchList(patches, source, offset, limit, total) }] };
  });
}

// find_xml_patches: 按 defName 搜索 XML Patches
export async function findXmlPatches(args: Record<string, unknown>, config: Config) {
  const defName = args.def_name as string;
  const source = args.source as string | undefined;
  const includeRaw = (args.include_raw as boolean) ?? false;
  const limit = (args.limit as number) ?? 50;

  return withDatabase(config.databasePath, (db) => {
    if (!source) {
      const patches = db.prepare(
        `SELECT p.*, s.Name as Source FROM XmlPatches p JOIN Sources s ON p.SourceId = s.Id
         WHERE p.TargetXPaths LIKE ? ORDER BY s.Name LIMIT ?`
      ).all(`%defName="${defName}"%`, limit) as unknown as XmlPatchResult[];

      return { content: [{ type: "text" as const, text: formatXmlPatchDetail(patches, defName, `## XML Patches referencing ${defName} (${patches.length})`, includeRaw) }] };
    }

    const patches = db.prepare(
      `SELECT p.*, s.Name as Source FROM XmlPatches p JOIN Sources s ON p.SourceId = s.Id
       WHERE p.TargetXPaths LIKE ? AND s.Name = ? ORDER BY s.Name LIMIT ?`
    ).all(`%defName="${defName}"%`, source, limit) as unknown as XmlPatchResult[];

    return { content: [{ type: "text" as const, text: formatXmlPatchDetail(patches, defName, `## XML Patches referencing ${defName} in ${source} (${patches.length})`, includeRaw) }] };
  });
}
