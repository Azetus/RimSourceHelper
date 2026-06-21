import type { DatabaseSync } from "node:sqlite";
import type { Config } from "../config.js";
import type { TargetSearchResult, TypeInfoResult, MethodInfoResult, MethodReference, HarmonyPatchEntry, TypeMembersResult, MemberMethod, MemberField, MemberProperty } from "../types.js";
import { withDatabase } from "../utils/database.js";
import { runAnalyzer } from "../utils/analyzer.js";
import { formatFindTarget, formatTypeInfo, formatMethodInfo, formatTypeMembers } from "../utils/formatter.js";

// find_target: 模糊搜索类型或方法，返回摘要列表
export async function findTarget(args: Record<string, unknown>, config: Config) {
  const query = args.query as string;
  const kind = args.kind as string | undefined;
  const source = args.source as string | undefined;
  const limit = (args.limit as number) ?? 20;

  const results = withDatabase(config.databasePath, (db) => {
    const items: TargetSearchResult[] = [];

    if (kind !== "method" && kind !== "field" && kind !== "property") {
      const sql = source
        ? `SELECT 'type' as Kind, t.FullName, t.Name, t.Namespace, t.IsAbstract, t.IsInterface, s.Name as Source
           FROM Types t JOIN Sources s ON t.SourceId = s.Id
           WHERE (t.Name LIKE ? OR t.FullName = ?) AND s.Name = ?
           ORDER BY CASE WHEN t.Name = ? OR t.FullName = ? THEN 0 WHEN t.Name LIKE ? THEN 1 ELSE 2 END, t.Name
           LIMIT ?`
        : `SELECT 'type' as Kind, t.FullName, t.Name, t.Namespace, t.IsAbstract, t.IsInterface, s.Name as Source
           FROM Types t JOIN Sources s ON t.SourceId = s.Id
           WHERE (t.Name LIKE ? OR t.FullName = ?)
           ORDER BY CASE WHEN t.Name = ? OR t.FullName = ? THEN 0 WHEN t.Name LIKE ? THEN 1 ELSE 2 END, t.Name
           LIMIT ?`;
      const params: (string | number)[] = source
        ? [`%${query}%`, query, source, query, query, `${query}%`, limit]
        : [`%${query}%`, query, query, query, `${query}%`, limit];
      items.push(...db.prepare(sql).all(...params) as unknown as TargetSearchResult[]);
    }

    if (kind !== "type" && kind !== "field" && kind !== "property") {
      const sql = source
        ? `SELECT 'method' as Kind, m.FullName, m.Name, m.Signature, m.ReturnType, s.Name as Source
           FROM Methods m JOIN Sources s ON m.SourceId = s.Id
           WHERE (m.Name LIKE ? OR m.FullName = ?) AND s.Name = ? AND m.IsAccessor = 0
           ORDER BY CASE WHEN m.Name = ? OR m.FullName = ? THEN 0 WHEN m.Name LIKE ? THEN 1 ELSE 2 END, m.Name
           LIMIT ?`
        : `SELECT 'method' as Kind, m.FullName, m.Name, m.Signature, m.ReturnType, s.Name as Source
           FROM Methods m JOIN Sources s ON m.SourceId = s.Id
           WHERE (m.Name LIKE ? OR m.FullName = ?) AND m.IsAccessor = 0
           ORDER BY CASE WHEN m.Name = ? OR m.FullName = ? THEN 0 WHEN m.Name LIKE ? THEN 1 ELSE 2 END, m.Name
           LIMIT ?`;
      const params: (string | number)[] = source
        ? [`%${query}%`, query, source, query, query, `${query}%`, limit]
        : [`%${query}%`, query, query, query, `${query}%`, limit];
      items.push(...db.prepare(sql).all(...params) as unknown as TargetSearchResult[]);
    }

    if (kind === "field") {
      const sql = source
        ? `SELECT 'field' as Kind, f.Name, t.FullName as TypeFullName, f.FieldType, f.IsStatic, s.Name as Source
           FROM Fields f JOIN Types t ON f.TypeId = t.Id JOIN Sources s ON f.SourceId = s.Id
           WHERE f.Name LIKE ? AND s.Name = ?
           ORDER BY CASE WHEN f.Name = ? THEN 0 WHEN f.Name LIKE ? THEN 1 ELSE 2 END, f.Name
           LIMIT ?`
        : `SELECT 'field' as Kind, f.Name, t.FullName as TypeFullName, f.FieldType, f.IsStatic, s.Name as Source
           FROM Fields f JOIN Types t ON f.TypeId = t.Id JOIN Sources s ON f.SourceId = s.Id
           WHERE f.Name LIKE ?
           ORDER BY CASE WHEN f.Name = ? THEN 0 WHEN f.Name LIKE ? THEN 1 ELSE 2 END, f.Name
           LIMIT ?`;
      const params: (string | number)[] = source
        ? [`%${query}%`, source, query, `${query}%`, limit]
        : [`%${query}%`, query, `${query}%`, limit];
      items.push(...db.prepare(sql).all(...params) as unknown as TargetSearchResult[]);
    }

    if (kind === "property") {
      const sql = source
        ? `SELECT 'property' as Kind, p.Name, t.FullName as TypeFullName, p.PropertyType, p.HasGetter, p.HasSetter, s.Name as Source
           FROM Properties p JOIN Types t ON p.TypeId = t.Id JOIN Sources s ON p.SourceId = s.Id
           WHERE p.Name LIKE ? AND s.Name = ?
           ORDER BY CASE WHEN p.Name = ? THEN 0 WHEN p.Name LIKE ? THEN 1 ELSE 2 END, p.Name
           LIMIT ?`
        : `SELECT 'property' as Kind, p.Name, t.FullName as TypeFullName, p.PropertyType, p.HasGetter, p.HasSetter, s.Name as Source
           FROM Properties p JOIN Types t ON p.TypeId = t.Id JOIN Sources s ON p.SourceId = s.Id
           WHERE p.Name LIKE ?
           ORDER BY CASE WHEN p.Name = ? THEN 0 WHEN p.Name LIKE ? THEN 1 ELSE 2 END, p.Name
           LIMIT ?`;
      const params: (string | number)[] = source
        ? [`%${query}%`, source, query, `${query}%`, limit]
        : [`%${query}%`, query, `${query}%`, limit];
      items.push(...db.prepare(sql).all(...params) as unknown as TargetSearchResult[]);
    }

    return items;
  });

  return { content: [{ type: "text" as const, text: formatFindTarget(results) }] };
}

// get_target_info: 获取类型或方法的全量信息
export async function getTargetInfo(args: Record<string, unknown>, config: Config) {
  const target = args.target as string;
  const includeSource = (args.include_source as boolean) ?? false;

  const info = withDatabase(config.databasePath, (db): TypeInfoResult | MethodInfoResult | null => {
    const type = db.prepare("SELECT * FROM Types WHERE FullName = ?").get(target) as Record<string, unknown> | undefined;
    if (type) return gatherTypeInfo(db, type);

    const methods = db.prepare("SELECT * FROM Methods WHERE FullName = ?").all(target) as unknown as Record<string, unknown>[];
    if (methods.length > 0) return gatherMethodInfo(db, methods);

    const method = db.prepare("SELECT * FROM Methods WHERE Signature = ?").get(target) as Record<string, unknown> | undefined;
    if (method) return gatherMethodInfo(db, [method]);

    return null;
  });

  if (!info) {
    return { content: [{ type: "text" as const, text: `Target not found: ${target}` }], isError: true };
  }

  if (includeSource) {
    try {
      const stdout = await runAnalyzer(config.analyzerPath, ["decompile", "--target", target, "--db", config.databasePath]);
      const parsed = JSON.parse(stdout);
      if (parsed.status === "success") info.Decompiled = parsed.source;
    } catch { info.Decompiled = "(decompilation failed)"; }
  }

  const text = info.Kind === "type" ? formatTypeInfo(info) : formatMethodInfo(info);
  return { content: [{ type: "text" as const, text }] };
}

// list_type_members: 列出类型的成员
export async function listTypeMembers(args: Record<string, unknown>, config: Config) {
  const typeName = args.type_name as string;
  const kind = (args.kind as string) ?? "all";

  const result = withDatabase(config.databasePath, (db): TypeMembersResult | string => {
    const type = db.prepare("SELECT Id, FullName FROM Types WHERE FullName = ?").get(typeName) as { Id: number; FullName: string } | undefined;

    if (!type) {
      const method = db.prepare(
        "SELECT m.FullName, t.FullName as TypeFullName FROM Methods m JOIN Types t ON m.TypeId = t.Id WHERE m.FullName = ? LIMIT 1"
      ).get(typeName) as { FullName: string; TypeFullName: string } | undefined;

      if (method) return `'${typeName}' is a method, not a type. Its parent type is '${method.TypeFullName}'.`;
      return `Type not found: ${typeName}`;
    }

    const membersResult: TypeMembersResult = { TypeName: type.FullName };

    if (kind === "methods" || kind === "all") {
      membersResult.Methods = db.prepare(
        "SELECT Name, Signature, ReturnType, IsStatic, IsVirtual, IsAbstract, Accessibility FROM Methods WHERE TypeId = ? AND IsAccessor = 0"
      ).all(type.Id) as unknown as MemberMethod[];
    }
    if (kind === "fields" || kind === "all") {
      membersResult.Fields = db.prepare(
        "SELECT Name, FieldType, IsStatic, Accessibility FROM Fields WHERE TypeId = ?"
      ).all(type.Id) as unknown as MemberField[];
    }
    if (kind === "properties" || kind === "all") {
      membersResult.Properties = db.prepare(
        "SELECT Name, PropertyType, HasGetter, HasSetter, Accessibility FROM Properties WHERE TypeId = ?"
      ).all(type.Id) as unknown as MemberProperty[];
    }

    return membersResult;
  });

  if (typeof result === "string") {
    return { content: [{ type: "text" as const, text: result }], isError: true };
  }

  return { content: [{ type: "text" as const, text: formatTypeMembers(result) }] };
}

// decompile: 按需反编译，仅返回源码（无元数据）
export async function decompile(args: Record<string, unknown>, config: Config) {
  const target = args.target as string;

  try {
    const stdout = await runAnalyzer(config.analyzerPath, [
      "decompile", "--target", target, "--db", config.databasePath
    ]);
    const parsed = JSON.parse(stdout);
    if (parsed.status === "error") {
      return { content: [{ type: "text" as const, text: parsed.error }], isError: true };
    }
    return { content: [{ type: "text" as const, text: `\`\`\`csharp\n${parsed.source}\n\`\`\`` }] };
  } catch (err) {
    return { content: [{ type: "text" as const, text: `Decompilation failed: ${err}` }], isError: true };
  }
}

// --- 内部辅助 ---

function gatherTypeInfo(db: DatabaseSync, type: Record<string, unknown>): TypeInfoResult {
  const typeId = type.Id as number;
  const typeFullName = type.FullName as string;

  const source = db.prepare("SELECT Name, Type FROM Sources WHERE Id = ?").get(type.SourceId as number) as unknown as { Name: string; Type: string };

  const parents = db.prepare(
    `SELECT t.FullName, i.IsInterface FROM Types t JOIN Inheritance i ON t.Id = i.ParentTypeId WHERE i.ChildTypeId = ?`
  ).all(typeId) as unknown as { FullName: string; IsInterface: number }[];

  const children = db.prepare(
    `SELECT t.FullName FROM Types t JOIN Inheritance i ON t.Id = i.ChildTypeId WHERE i.ParentTypeId = ?`
  ).all(typeId) as unknown as { FullName: string }[];

  const methodCount = (db.prepare("SELECT COUNT(*) as Count FROM Methods WHERE TypeId = ?").get(typeId) as unknown as { Count: number }).Count;
  const fieldCount = (db.prepare("SELECT COUNT(*) as Count FROM Fields WHERE TypeId = ?").get(typeId) as unknown as { Count: number }).Count;
  const propertyCount = (db.prepare("SELECT COUNT(*) as Count FROM Properties WHERE TypeId = ?").get(typeId) as unknown as { Count: number }).Count;

  const patches = db.prepare(
    `SELECT h.TargetMethod, h.TargetParams, h.PatchType, h.PatchClass, h.PatchMethod, h.Priority, s.Name as Source
     FROM HarmonyPatches h JOIN Sources s ON h.SourceId = s.Id WHERE h.TargetType = ?`
  ).all(typeFullName) as unknown as HarmonyPatchEntry[];

  return {
    Kind: "type",
    FullName: typeFullName,
    Namespace: type.Namespace as string | null,
    BaseType: type.BaseType as string | null,
    IsAbstract: !!type.IsAbstract,
    IsInterface: !!type.IsInterface,
    IsEnum: !!type.IsEnum,
    IsSealed: !!type.IsSealed,
    Accessibility: type.Accessibility as string | null,
    Source: source,
    Parents: parents,
    Children: children,
    MemberCounts: { Methods: methodCount, Fields: fieldCount, Properties: propertyCount },
    HarmonyPatches: patches
  };
}

function gatherMethodInfo(db: DatabaseSync, methods: Record<string, unknown>[]): MethodInfoResult {
  const primary = methods[0];
  const methodFullName = primary.FullName as string;
  const methodName = primary.Name as string;
  const typeId = primary.TypeId as number;
  const methodId = primary.Id as number;

  const source = db.prepare("SELECT Name, Type FROM Sources WHERE Id = ?").get(primary.SourceId as number) as unknown as { Name: string; Type: string };

  const parentType = db.prepare("SELECT FullName, Namespace, BaseType FROM Types WHERE Id = ?").get(typeId) as unknown as { FullName: string; Namespace: string; BaseType: string } | null;

  const overloads = db.prepare(
    "SELECT Signature, ReturnType FROM Methods WHERE FullName = ? AND Id != ?"
  ).all(methodFullName, methodId) as unknown as { Signature: string; ReturnType: string }[];

  const allMethodIds = db.prepare("SELECT Id FROM Methods WHERE FullName = ?").all(methodFullName) as unknown as { Id: number }[];
  const idPlaceholders = allMethodIds.map(() => "?").join(",");
  const idValues = allMethodIds.map(m => m.Id);

  const CALL_LIMIT = 50;
  const callersRaw = db.prepare(
    `SELECT DISTINCT m.FullName, m.Signature, s.Name as Source
     FROM Methods m JOIN Calls c ON m.Id = c.CallerMethodId JOIN Sources s ON m.SourceId = s.Id
     WHERE c.CalleeMethodId IN (${idPlaceholders}) LIMIT ${CALL_LIMIT + 1}`
  ).all(...idValues) as unknown as MethodReference[];
  const callersTruncated = callersRaw.length > CALL_LIMIT;
  const callers = callersTruncated ? callersRaw.slice(0, CALL_LIMIT) : callersRaw;

  const calleesRaw = db.prepare(
    `SELECT DISTINCT m.FullName, m.Signature, s.Name as Source
     FROM Methods m JOIN Calls c ON m.Id = c.CalleeMethodId JOIN Sources s ON m.SourceId = s.Id
     WHERE c.CallerMethodId IN (${idPlaceholders}) LIMIT ${CALL_LIMIT + 1}`
  ).all(...idValues) as unknown as MethodReference[];
  const calleesTruncated = calleesRaw.length > CALL_LIMIT;
  const callees = calleesTruncated ? calleesRaw.slice(0, CALL_LIMIT) : calleesRaw;

  const parentFullName = parentType?.FullName;
  const patches = parentFullName
    ? db.prepare(
        `SELECT h.TargetParams, h.PatchType, h.PatchClass, h.PatchMethod, h.Priority, s.Name as Source
         FROM HarmonyPatches h JOIN Sources s ON h.SourceId = s.Id
         WHERE h.TargetType = ? AND h.TargetMethod = ?`
      ).all(parentFullName, methodName) as unknown as HarmonyPatchEntry[]
    : [];

  return {
    Kind: "method",
    FullName: methodFullName,
    Signature: primary.Signature as string,
    ReturnType: primary.ReturnType as string,
    IsStatic: !!primary.IsStatic,
    IsVirtual: !!primary.IsVirtual,
    IsAbstract: !!primary.IsAbstract,
    Accessibility: primary.Accessibility as string | null,
    Source: source,
    ParentType: parentType,
    Overloads: overloads,
    Callers: callers,
    CallersTruncated: callersTruncated,
    Callees: callees,
    CalleesTruncated: calleesTruncated,
    HarmonyPatches: patches
  };
}
