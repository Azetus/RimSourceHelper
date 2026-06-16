import type { DatabaseSync, StatementSync } from "node:sqlite";
import type { Config } from "../config.js";
import { withDatabase } from "../utils/database.js";
import { runAnalyzer } from "../utils/analyzer.js";

// find_target: 模糊搜索类型或方法，返回摘要列表
export async function findTarget(args: Record<string, unknown>, config: Config) {
  const query = args.query as string;
  const kind = args.kind as string | undefined;
  const source = args.source as string | undefined;

  return withDatabase(config.databasePath, (db) => {
    const results: Record<string, unknown>[] = [];

    if (kind !== "method") {
      const sql = source
        ? `SELECT 'type' as kind, t.FullName, t.Name, t.Namespace, t.IsAbstract, t.IsInterface, s.Name as source
           FROM Types t JOIN Sources s ON t.SourceId = s.Id
           WHERE t.Name LIKE ? AND s.Name = ?`
        : `SELECT 'type' as kind, t.FullName, t.Name, t.Namespace, t.IsAbstract, t.IsInterface, s.Name as source
           FROM Types t JOIN Sources s ON t.SourceId = s.Id
           WHERE t.Name LIKE ?`;
      const params = source ? [`%${query}%`, source] : [`%${query}%`];
      results.push(...db.prepare(sql).all(...params) as Record<string, unknown>[]);
    }

    if (kind !== "type") {
      const sql = source
        ? `SELECT 'method' as kind, m.FullName, m.Name, m.Signature, m.ReturnType, s.Name as source
           FROM Methods m JOIN Sources s ON m.SourceId = s.Id
           WHERE m.Name LIKE ? AND s.Name = ?`
        : `SELECT 'method' as kind, m.FullName, m.Name, m.Signature, m.ReturnType, s.Name as source
           FROM Methods m JOIN Sources s ON m.SourceId = s.Id
           WHERE m.Name LIKE ?`;
      const params = source ? [`%${query}%`, source] : [`%${query}%`];
      results.push(...db.prepare(sql).all(...params) as Record<string, unknown>[]);
    }

    return { content: [{ type: "text" as const, text: JSON.stringify(results, null, 2) }] };
  });
}

// get_target_info: 获取类型或方法的全量信息
export async function getTargetInfo(args: Record<string, unknown>, config: Config) {
  const target = args.target as string;
  const includeSource = (args.include_source as boolean) ?? false;

  // Phase 1: 同步 DB 查询
  const info = withDatabase(config.databasePath, (db) => {
    // 尝试作为类型查找
    const type = db.prepare("SELECT * FROM Types WHERE FullName = ?").get(target) as Record<string, unknown> | undefined;
    if (type) return gatherTypeInfo(db, type);

    // 尝试作为方法 FullName 查找
    const methods = db.prepare("SELECT * FROM Methods WHERE FullName = ?").all(target) as Record<string, unknown>[];
    if (methods.length > 0) return gatherMethodInfo(db, methods);

    // 尝试作为方法 Signature 精确查找
    const method = db.prepare("SELECT * FROM Methods WHERE Signature = ?").get(target) as Record<string, unknown> | undefined;
    if (method) return gatherMethodInfo(db, [method]);

    return null;
  });

  if (!info) {
    return { content: [{ type: "text" as const, text: `Target not found: ${target}` }], isError: true };
  }

  // Phase 2: 异步反编译（在 withDatabase 外，连接已关闭）
  if (includeSource) {
    try {
      const stdout = await runAnalyzer(config.analyzerPath, [
        "decompile", "--target", target, "--db", config.databasePath
      ]);
      const parsed = JSON.parse(stdout);
      if (parsed.status === "success") {
        (info as Record<string, unknown>).decompiled = parsed.source;
      }
    } catch {
      (info as Record<string, unknown>).decompiled = "(decompilation failed)";
    }
  }

  return { content: [{ type: "text" as const, text: JSON.stringify(info, null, 2) }] };
}

// list_type_members: 列出类型的成员
export async function listTypeMembers(args: Record<string, unknown>, config: Config) {
  const typeName = args.type_name as string;
  const kind = (args.kind as string) ?? "all";

  return withDatabase(config.databasePath, (db) => {
    // 查找类型
    const type = db.prepare("SELECT Id, FullName FROM Types WHERE FullName = ?").get(typeName) as { Id: number; FullName: string } | undefined;

    if (!type) {
      // 检查是否是方法名，提供有用的错误提示
      const method = db.prepare(
        "SELECT m.FullName, t.FullName as TypeFullName FROM Methods m JOIN Types t ON m.TypeId = t.Id WHERE m.FullName = ? LIMIT 1"
      ).get(typeName) as { FullName: string; TypeFullName: string } | undefined;

      if (method) {
        return {
          content: [{ type: "text" as const, text: `'${typeName}' is a method, not a type. Its parent type is '${method.TypeFullName}'.` }],
          isError: true
        };
      }
      return { content: [{ type: "text" as const, text: `Type not found: ${typeName}` }], isError: true };
    }

    const result: Record<string, unknown> = { typeName: type.FullName };

    if (kind === "methods" || kind === "all") {
      result.methods = db.prepare(
        "SELECT Name, Signature, ReturnType, IsStatic, IsVirtual, IsAbstract, Accessibility FROM Methods WHERE TypeId = ?"
      ).all(type.Id);
    }
    if (kind === "fields" || kind === "all") {
      result.fields = db.prepare(
        "SELECT Name, FieldType, IsStatic, Accessibility FROM Fields WHERE TypeId = ?"
      ).all(type.Id);
    }
    if (kind === "properties" || kind === "all") {
      result.properties = db.prepare(
        "SELECT Name, PropertyType, HasGetter, HasSetter, Accessibility FROM Properties WHERE TypeId = ?"
      ).all(type.Id);
    }

    return { content: [{ type: "text" as const, text: JSON.stringify(result, null, 2) }] };
  });
}

// --- 内部辅助函数 ---

function gatherTypeInfo(db: DatabaseSync, type: Record<string, unknown>) {
  const typeId = type.Id as number;
  const typeFullName = type.FullName as string;

  // Source
  const source = db.prepare("SELECT Name, Type FROM Sources WHERE Id = ?").get(type.SourceId as number);

  // 继承：父类（含接口标记）
  const parents = db.prepare(
    `SELECT t.FullName, i.IsInterface FROM Types t
     JOIN Inheritance i ON t.Id = i.ParentTypeId
     WHERE i.ChildTypeId = ?`
  ).all(typeId);

  // 继承：子类
  const children = db.prepare(
    `SELECT t.FullName FROM Types t
     JOIN Inheritance i ON t.Id = i.ChildTypeId
     WHERE i.ParentTypeId = ?`
  ).all(typeId);

  // 成员统计
  const methodCount = (db.prepare("SELECT COUNT(*) as count FROM Methods WHERE TypeId = ?").get(typeId) as { count: number }).count;
  const fieldCount = (db.prepare("SELECT COUNT(*) as count FROM Fields WHERE TypeId = ?").get(typeId) as { count: number }).count;
  const propertyCount = (db.prepare("SELECT COUNT(*) as count FROM Properties WHERE TypeId = ?").get(typeId) as { count: number }).count;

  // Harmony Patches（针对该类型任意方法的 Patch）
  const patches = db.prepare(
    `SELECT h.TargetMethod, h.PatchType, h.PatchClass, h.PatchMethod, h.Priority, s.Name as source
     FROM HarmonyPatches h JOIN Sources s ON h.SourceId = s.Id
     WHERE h.TargetType = ?`
  ).all(typeFullName);

  return {
    kind: "type",
    fullName: typeFullName,
    namespace: type.Namespace,
    baseType: type.BaseType,
    isAbstract: !!type.IsAbstract,
    isInterface: !!type.IsInterface,
    isEnum: !!type.IsEnum,
    isSealed: !!type.IsSealed,
    accessibility: type.Accessibility,
    source,
    parents,
    children,
    memberCounts: { methods: methodCount, fields: fieldCount, properties: propertyCount },
    harmonyPatches: patches
  };
}

function gatherMethodInfo(db: DatabaseSync, methods: Record<string, unknown>[]) {
  const primary = methods[0];
  const methodId = primary.Id as number;
  const methodFullName = primary.FullName as string;
  const methodName = primary.Name as string;
  const typeId = primary.TypeId as number;

  // Source
  const source = db.prepare("SELECT Name, Type FROM Sources WHERE Id = ?").get(primary.SourceId as number);

  // 父类型
  const parentType = db.prepare("SELECT FullName, Namespace, BaseType FROM Types WHERE Id = ?").get(typeId);

  // 重载（排除自身）
  const overloads = db.prepare(
    "SELECT Signature, ReturnType FROM Methods WHERE FullName = ? AND Id != ?"
  ).all(methodFullName, methodId);

  // 获取所有同名方法的 Id（聚合所有重载的调用关系）
  const allMethodIds = db.prepare("SELECT Id FROM Methods WHERE FullName = ?").all(methodFullName) as { Id: number }[];
  const idPlaceholders = allMethodIds.map(() => "?").join(",");
  const idValues = allMethodIds.map(m => m.Id);

  // 调用方（聚合所有重载）
  const callers = db.prepare(
    `SELECT DISTINCT m.FullName, m.Signature, s.Name as source
     FROM Methods m JOIN Calls c ON m.Id = c.CallerMethodId JOIN Sources s ON m.SourceId = s.Id
     WHERE c.CalleeMethodId IN (${idPlaceholders})`
  ).all(...idValues);

  // 被调用方（聚合所有重载）
  const callees = db.prepare(
    `SELECT DISTINCT m.FullName, m.Signature, s.Name as source
     FROM Methods m JOIN Calls c ON m.Id = c.CalleeMethodId JOIN Sources s ON m.SourceId = s.Id
     WHERE c.CallerMethodId IN (${idPlaceholders})`
  ).all(...idValues);

  // Harmony Patches（通过父类型 FullName + 方法名匹配）
  const parentFullName = (parentType as Record<string, unknown>)?.FullName as string | undefined;
  const patches = parentFullName
    ? db.prepare(
        `SELECT h.PatchType, h.PatchClass, h.PatchMethod, h.Priority, s.Name as source
         FROM HarmonyPatches h JOIN Sources s ON h.SourceId = s.Id
         WHERE h.TargetType = ? AND h.TargetMethod = ?`
      ).all(parentFullName, methodName)
    : [];

  return {
    kind: "method",
    fullName: methodFullName,
    signature: primary.Signature,
    returnType: primary.ReturnType,
    isStatic: !!primary.IsStatic,
    isVirtual: !!primary.IsVirtual,
    isAbstract: !!primary.IsAbstract,
    accessibility: primary.Accessibility,
    source,
    parentType,
    overloads,
    callers,
    callees,
    harmonyPatches: patches
  };
}
