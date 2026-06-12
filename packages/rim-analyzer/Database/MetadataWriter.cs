using RimAnalyzer.Models;

namespace RimAnalyzer.Database;

// 数据处理与写入编排：将 MetadataCollector 的结果写入数据库
public static class MetadataWriter
{
    public record WriteResult(int Types, int Methods, int Fields, int Properties, int Inheritance);

    public static WriteResult Write(DatabaseContext db, List<TypeMetadata> collected, Action<string> log)
    {
        // 步骤1：插入所有类型
        var typeEntities = collected.Select(c => c.Type).ToList();
        var typeCount = db.Types.BulkInsert(typeEntities);
        log($"[INFO] Inserted {typeCount} types.");

        // 步骤2：构建 FullName → Id 映射
        var typeIdMap = db.Types.GetFullNameToIdMap();

        // 步骤3：为成员分配 TypeId，收集到扁平列表
        var allMethods = new List<MethodEntity>();
        var allFields = new List<FieldEntity>();
        var allProperties = new List<PropertyEntity>();

        foreach (var meta in collected)
        {
            if (!typeIdMap.TryGetValue(meta.Type.FullName, out var typeId))
                continue;

            foreach (var m in meta.Methods)
            {
                m.TypeId = typeId;
                allMethods.Add(m);
            }
            foreach (var f in meta.Fields)
            {
                f.TypeId = typeId;
                allFields.Add(f);
            }
            foreach (var p in meta.Properties)
            {
                p.TypeId = typeId;
                allProperties.Add(p);
            }
        }

        // 步骤4：批量插入成员
        var methodCount = db.Methods.BulkInsert(allMethods);
        var fieldCount = db.Fields.BulkInsert(allFields);
        var propCount = db.Properties.BulkInsert(allProperties);
        log($"[INFO] Inserted {methodCount} methods, {fieldCount} fields, {propCount} properties.");

        // 步骤5：构建继承关系
        var inheritanceRelations = BuildInheritanceRelations(collected, typeIdMap);
        var inheritCount = db.Inheritance.BulkInsert(inheritanceRelations);
        log($"[INFO] Inserted {inheritCount} inheritance relations.");

        return new WriteResult(typeCount, methodCount, fieldCount, propCount, inheritCount);
    }

    // 解析 BaseType 和 Interfaces 为 (ParentId, ChildId, IsInterface) 元组
    private static List<(long ParentTypeId, long ChildTypeId, bool IsInterface)> BuildInheritanceRelations(
        List<TypeMetadata> collected, Dictionary<string, long> typeIdMap)
    {
        var relations = new List<(long, long, bool)>();

        foreach (var meta in collected)
        {
            if (!typeIdMap.TryGetValue(meta.Type.FullName, out var childId))
                continue;

            // 基类关系
            if (meta.Type.BaseType is not null &&
                typeIdMap.TryGetValue(meta.Type.BaseType, out var baseId))
            {
                relations.Add((baseId, childId, false));
            }

            // 接口关系
            foreach (var iface in meta.Interfaces)
            {
                if (typeIdMap.TryGetValue(iface, out var ifaceId))
                    relations.Add((ifaceId, childId, true));
            }
        }

        return relations;
    }
}
