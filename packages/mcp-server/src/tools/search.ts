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

    if (kind !== "method") {
      const sql = source
        ? `SELECT 'type' as kind, t.FullName, t.Name, t.Namespace, t.IsAbstract, t.IsInterface, s.Name as source
           FROM Types t JOIN Sources s ON t.SourceId = s.Id
           WHERE t.Name LIKE ? AND s.Name = ? LIMIT ?`
        : `SELECT 'type' as kind, t.FullName, t.Name, t.Namespace, t.IsAbstract, t.IsInterface, s.Name as source
           FROM Types t JOIN Sources s ON t.SourceId = s.Id
           WHERE t.Name LIKE ? LIMIT ?`;
      const params: (string | number)[] = source ? [`%${query}%`, source, limit] : [`%${query}%`, limit];
      items.push(...db.prepare(sql).all(...params) as unknown as TargetSearchResult[]);
    }

    if (kind !== "type") {
      const sql = source
        ? `SELECT 'method' as kind, m.FullName, m.Name, m.Signature, m.ReturnType, s.Name as source
           FROM Methods m JOIN Sources s ON m.SourceId = s.Id
           WHERE m.Name LIKE ? AND s.Name = ? LIMIT ?`
        : `SELECT 'method' as kind, m.FullName, m.Name, m.Signature, m.ReturnType, s.Name as source
           FROM Methods m JOIN Sources s ON m.SourceId = s.Id
           WHERE m.Name LIKE ? LIMIT ?`;
      const params: (string | number)[] = source ? [`%${query}%`, source, limit] : [`%${query}%`, limit];
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
      if (parsed.status === "success") info.decompiled = parsed.source;
    } catch { info.decompiled = "(decompilation failed)"; }
  }

  const text = info.kind === "type" ? formatTypeInfo(info) : formatMethodInfo(info);
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

    const membersResult: TypeMembersResult = { typeName: type.FullName };

    if (kind === "methods" || kind === "all") {
      membersResult.methods = db.prepare(
        "SELECT Name, Signature, ReturnType, IsStatic, IsVirtual, IsAbstract, Accessibility FROM Methods WHERE TypeId = ?"
      ).all(type.Id) as unknown as MemberMethod[];
    }
    if (kind === "fields" || kind === "all") {
      membersResult.fields = db.prepare(
        "SELECT Name, FieldType, IsStatic, Accessibility FROM Fields WHERE TypeId = ?"
      ).all(type.Id) as unknown as MemberField[];
    }
    if (kind === "properties" || kind === "all") {
      membersResult.properties = db.prepare(
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

  const methodCount = (db.prepare("SELECT COUNT(*) as count FROM Methods WHERE TypeId = ?").get(typeId) as unknown as { count: number }).count;
  const fieldCount = (db.prepare("SELECT COUNT(*) as count FROM Fields WHERE TypeId = ?").get(typeId) as unknown as { count: number }).count;
  const propertyCount = (db.prepare("SELECT COUNT(*) as count FROM Properties WHERE TypeId = ?").get(typeId) as unknown as { count: number }).count;

  const patches = db.prepare(
    `SELECT h.TargetMethod, h.PatchType, h.PatchClass, h.PatchMethod, h.Priority, s.Name as source
     FROM HarmonyPatches h JOIN Sources s ON h.SourceId = s.Id WHERE h.TargetType = ?`
  ).all(typeFullName) as unknown as HarmonyPatchEntry[];

  return {
    kind: "type",
    fullName: typeFullName,
    namespace: type.Namespace as string | null,
    baseType: type.BaseType as string | null,
    isAbstract: !!type.IsAbstract,
    isInterface: !!type.IsInterface,
    isEnum: !!type.IsEnum,
    isSealed: !!type.IsSealed,
    accessibility: type.Accessibility as string | null,
    source,
    parents,
    children,
    memberCounts: { methods: methodCount, fields: fieldCount, properties: propertyCount },
    harmonyPatches: patches
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
    `SELECT DISTINCT m.FullName, m.Signature, s.Name as source
     FROM Methods m JOIN Calls c ON m.Id = c.CallerMethodId JOIN Sources s ON m.SourceId = s.Id
     WHERE c.CalleeMethodId IN (${idPlaceholders}) LIMIT ${CALL_LIMIT + 1}`
  ).all(...idValues) as unknown as MethodReference[];
  const callersTruncated = callersRaw.length > CALL_LIMIT;
  const callers = callersTruncated ? callersRaw.slice(0, CALL_LIMIT) : callersRaw;

  const calleesRaw = db.prepare(
    `SELECT DISTINCT m.FullName, m.Signature, s.Name as source
     FROM Methods m JOIN Calls c ON m.Id = c.CalleeMethodId JOIN Sources s ON m.SourceId = s.Id
     WHERE c.CallerMethodId IN (${idPlaceholders}) LIMIT ${CALL_LIMIT + 1}`
  ).all(...idValues) as unknown as MethodReference[];
  const calleesTruncated = calleesRaw.length > CALL_LIMIT;
  const callees = calleesTruncated ? calleesRaw.slice(0, CALL_LIMIT) : calleesRaw;

  const parentFullName = parentType?.FullName;
  const patches = parentFullName
    ? db.prepare(
        `SELECT h.PatchType, h.PatchClass, h.PatchMethod, h.Priority, s.Name as source
         FROM HarmonyPatches h JOIN Sources s ON h.SourceId = s.Id
         WHERE h.TargetType = ? AND h.TargetMethod = ?`
      ).all(parentFullName, methodName) as unknown as HarmonyPatchEntry[]
    : [];

  return {
    kind: "method",
    fullName: methodFullName,
    signature: primary.Signature as string,
    returnType: primary.ReturnType as string,
    isStatic: !!primary.IsStatic,
    isVirtual: !!primary.IsVirtual,
    isAbstract: !!primary.IsAbstract,
    accessibility: primary.Accessibility as string | null,
    source,
    parentType,
    overloads,
    callers,
    callersTruncated,
    callees,
    calleesTruncated,
    harmonyPatches: patches
  };
}
