using System.CommandLine;
using System.Text.Json;
using Dapper;
using RimAnalyzer.Analysis.Embedding;
using RimAnalyzer.Database;
using RimAnalyzer.Models;
using RimAnalyzer.VectorDB;

namespace RimAnalyzer.Commands;

// index 子命令：从 SQLite 元数据构建语义索引（全量或单 Source 增量）
public static class IndexCommand
{
    private const int BatchSize = 32;
    private const int LogInterval = 1000;

    public static Command Create()
    {
        var dbOption = new Option<string>("--db") { Description = "SQLite knowledge database path", Required = true };
        var vectorDbOption = new Option<string>("--vector-db") { Description = "Vector index file path", Required = true };
        var embeddingUrlOption = new Option<string>("--embedding-url") { Description = "embedding-service URL (e.g. http://127.0.0.1:8000)", Required = true };
        var sourceIdOption = new Option<long?>("--source-id") { Description = "Optional: rebuild only this SourceId" };
        var verboseOption = new Option<bool>("--verbose") { Description = "Enable verbose logging" };

        var command = new Command("index", "Build or rebuild the semantic vector index")
        {
            dbOption, vectorDbOption, embeddingUrlOption, sourceIdOption, verboseOption
        };

        command.SetAction((parseResult, _) =>
        {
            var dbPath = parseResult.GetValue(dbOption)!;
            var vectorDbPath = parseResult.GetValue(vectorDbOption)!;
            var embeddingUrl = parseResult.GetValue(embeddingUrlOption)!;
            var sourceId = parseResult.GetValue(sourceIdOption);
            var verbose = parseResult.GetValue(verboseOption);

            try
            {
                var result = Execute(dbPath, vectorDbPath, embeddingUrl, sourceId, verbose ? Console.Error.WriteLine : null);
                Console.WriteLine(JsonSerializer.Serialize(result));
            }
            catch (Exception ex)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { status = "error", error = ex.Message }));
                Environment.ExitCode = 1;
            }

            return Task.CompletedTask;
        });

        return command;
    }

    private static object Execute(string dbPath, string vectorDbPath, string embeddingUrl,
        long? sourceId, Action<string>? log)
    {
        if (!File.Exists(dbPath))
            throw new FileNotFoundException($"Database not found: {dbPath}");

        using var db = DatabaseContext.Open(dbPath, force: false);
        var conn = db.Connection;

        // 1. 获取 embedding 模型信息
        var client = new EmbeddingClient(embeddingUrl);
        if (!client.HealthCheck())
            throw new InvalidOperationException("embedding-service is not available");

        var info = client.GetInfo();
        log?.Invoke($"[INFO] Embedding model: {info.Model}, dimension: {info.Dimension}");

        // 2. 初始化向量存储
        using var store = new SqliteVectorStore(vectorDbPath);
        store.Initialize(info.Dimension);

        if (sourceId.HasValue)
        {
            log?.Invoke($"[INFO] Rebuilding index for SourceId={sourceId.Value}...");
            store.DeleteBySourceId(sourceId.Value);
        }
        else
        {
            log?.Invoke("[INFO] Full rebuild, clearing vector db...");
            store.Clear();
            store.Initialize(info.Dimension);
        }

        // 3. 收集实体
        var entities = CollectEntities(conn, sourceId, log);

        // 4. 批量查询辅助数据
        var calleeMap = BuildCalleeMap(conn, entities.MethodIds);
        var interfaceMap = BuildInterfaceMap(conn, entities.TypeIds);
        log?.Invoke($"[INFO] Callee map: {calleeMap.Count} methods, Interface map: {interfaceMap.Count} types");

        // 5. 构建语义文本 + 写入向量存储
        var allDocs = new List<VectorDocument>();
        var allTexts = new List<string>();
        var totalCount = 0;

        void FlushBatch()
        {
            if (allDocs.Count == 0) return;

            var vectors = client.Embed(allTexts.ToArray());
            store.AddBatch(allDocs, vectors);
            totalCount += allDocs.Count;

            allDocs.Clear();
            allTexts.Clear();
        }

        void AddEntity(VectorDocument doc, string text)
        {
            allDocs.Add(doc);
            allTexts.Add(text);

            if (allDocs.Count >= BatchSize)
                FlushBatch();

            if (totalCount > 0 && totalCount % LogInterval < BatchSize)
                log?.Invoke($"[INFO] Indexed {totalCount} entities...");
        }

        // Types
        foreach (var t in entities.Types)
        {
            interfaceMap.TryGetValue(t.Id, out var ifaces);
            var text = SemanticFormatter.FormatType(t, ifaces ?? []);
            AddEntity(new VectorDocument(t.Id, t.SourceId, "type", t.FullName), text);
        }
        FlushBatch();
        log?.Invoke($"[INFO] Indexed {entities.Types.Count} types");

        // Methods
        foreach (var m in entities.Methods)
        {
            calleeMap.TryGetValue(m.Id, out var callees);
            var text = SemanticFormatter.FormatMethod(m.ParentFullName, m.Name, m.ReturnType, m.ParamTypes, callees ?? []);
            AddEntity(new VectorDocument(m.Id, m.SourceId, "method", m.FullName), text);
        }
        FlushBatch();
        log?.Invoke($"[INFO] Indexed {entities.Methods.Count} methods");

        // Fields
        foreach (var f in entities.Fields)
        {
            var text = SemanticFormatter.FormatField(f.ParentFullName, f.Name, f.FieldType);
            AddEntity(new VectorDocument(f.Id, f.SourceId, "field", $"{f.ParentFullName}.{f.Name}"), text);
        }
        FlushBatch();
        log?.Invoke($"[INFO] Indexed {entities.Fields.Count} fields");

        // Properties
        foreach (var p in entities.Properties)
        {
            var text = SemanticFormatter.FormatProperty(p.ParentFullName, p.Name, p.PropertyType, p.HasGetter, p.HasSetter);
            AddEntity(new VectorDocument(p.Id, p.SourceId, "property", $"{p.ParentFullName}.{p.Name}"), text);
        }
        FlushBatch();
        log?.Invoke($"[INFO] Indexed {entities.Properties.Count} properties");

        // Defs
        foreach (var d in entities.Defs)
        {
            var text = SemanticFormatter.FormatDef(d);
            AddEntity(new VectorDocument(d.Id, d.SourceId, "def", $"{d.DefType}/{d.DefName}"), text);
        }
        FlushBatch();
        log?.Invoke($"[INFO] Indexed {entities.Defs.Count} defs");

        // 6. 写入模型配置
        store.SetConfig(new VectorConfig(info.Provider, info.Model, info.Dimension));
        log?.Invoke($"[INFO] Index complete. Total entities: {totalCount}");

        return new { status = "success", entities = totalCount };
    }

    // ===== Entity Collection =====

    private record CollectedEntities(
        List<TypeEntity> Types, HashSet<long> TypeIds,
        List<MethodIndexItem> Methods, HashSet<long> MethodIds,
        List<FieldIndexItem> Fields,
        List<PropertyIndexItem> Properties,
        List<DefEntity> Defs
    );

    private static CollectedEntities CollectEntities(
        Microsoft.Data.Sqlite.SqliteConnection conn, long? sourceId, Action<string>? log)
    {
        object? param = sourceId.HasValue ? new { sid = sourceId.Value } : null;

        // Types
        var types = conn.Query<TypeEntity>(
            sourceId.HasValue
                ? "SELECT * FROM Types WHERE SourceId = @sid ORDER BY Id"
                : "SELECT * FROM Types ORDER BY Id"
            , param).Where(t =>
                t.Name != "<Module>" &&
                !t.Name.StartsWith("<>c__") &&
                !t.Name.StartsWith("<>c")).ToList();
        log?.Invoke($"[INFO] Collected {types.Count} types (filtered)");

        // Methods：排除 accessor + 编译器生成
        var methods = conn.Query(
            sourceId.HasValue
                ? """
                  SELECT m.Id, m.FullName, m.Name, m.ReturnType, m.ParamTypes, m.SourceId, m.IsAccessor,
                         t.FullName AS ParentFullName
                  FROM Methods m
                  JOIN Types t ON m.TypeId = t.Id
                  WHERE m.SourceId = @sid
                  ORDER BY m.Id
                  """
                : """
                  SELECT m.Id, m.FullName, m.Name, m.ReturnType, m.ParamTypes, m.SourceId, m.IsAccessor,
                         t.FullName AS ParentFullName
                  FROM Methods m
                  JOIN Types t ON m.TypeId = t.Id
                  ORDER BY m.Id
                  """
            , param).Cast<dynamic>()
            .Where(m => m.IsAccessor == 0L && !((string)m.FullName).Contains("<>"))
            .Select(m => new MethodIndexItem
            {
                Id = (long)m.Id,
                FullName = (string)m.FullName,
                Name = (string)m.Name,
                ReturnType = (string?)m.ReturnType,
                ParamTypes = (string?)m.ParamTypes,
                SourceId = (long)m.SourceId,
                ParentFullName = (string)m.ParentFullName
            }).ToList();
        log?.Invoke($"[INFO] Collected {methods.Count} methods (filtered)");

        // Fields
        var fields = conn.Query(
            sourceId.HasValue
                ? """
                  SELECT f.Id, f.Name, f.FieldType, f.SourceId, t.FullName AS ParentFullName
                  FROM Fields f
                  JOIN Types t ON f.TypeId = t.Id
                  WHERE f.SourceId = @sid
                  ORDER BY f.Id
                  """
                : """
                  SELECT f.Id, f.Name, f.FieldType, f.SourceId, t.FullName AS ParentFullName
                  FROM Fields f
                  JOIN Types t ON f.TypeId = t.Id
                  ORDER BY f.Id
                  """
            , param).Cast<dynamic>()
            .Select(f => new FieldIndexItem
            {
                Id = (long)f.Id,
                Name = (string)f.Name,
                FieldType = (string?)f.FieldType,
                SourceId = (long)f.SourceId,
                ParentFullName = (string)f.ParentFullName
            }).ToList();

        // Properties
        var properties = conn.Query(
            sourceId.HasValue
                ? """
                  SELECT p.Id, p.Name, p.PropertyType, p.HasGetter, p.HasSetter, p.SourceId, t.FullName AS ParentFullName
                  FROM Properties p
                  JOIN Types t ON p.TypeId = t.Id
                  WHERE p.SourceId = @sid
                  ORDER BY p.Id
                  """
                : """
                  SELECT p.Id, p.Name, p.PropertyType, p.HasGetter, p.HasSetter, p.SourceId, t.FullName AS ParentFullName
                  FROM Properties p
                  JOIN Types t ON p.TypeId = t.Id
                  ORDER BY p.Id
                  """
            , param).Cast<dynamic>()
            .Select(p => new PropertyIndexItem
            {
                Id = (long)p.Id,
                Name = (string)p.Name,
                PropertyType = (string?)p.PropertyType,
                HasGetter = (long)p.HasGetter != 0,
                HasSetter = (long)p.HasSetter != 0,
                SourceId = (long)p.SourceId,
                ParentFullName = (string)p.ParentFullName
            }).ToList();

        // Defs
        var defs = conn.Query<DefEntity>(
            sourceId.HasValue
                ? "SELECT * FROM Defs WHERE SourceId = @sid ORDER BY Id"
                : "SELECT * FROM Defs ORDER BY Id"
            , param).ToList();

        log?.Invoke($"[INFO] Collected {fields.Count} fields, {properties.Count} properties, {defs.Count} defs");

        return new CollectedEntities(
            types, types.Select(t => t.Id).ToHashSet(),
            methods, methods.Select(m => m.Id).ToHashSet(),
            fields, properties, defs
        );
    }

    // ===== Callee Map =====

    private static Dictionary<long, string[]> BuildCalleeMap(
        Microsoft.Data.Sqlite.SqliteConnection conn, HashSet<long> methodIds)
    {
        if (methodIds.Count == 0) return new();

        var idList = string.Join(",", methodIds);

        var rows = conn.Query(
            $"""
            SELECT c.CallerMethodId, m.FullName
            FROM Calls c
            JOIN Methods m ON c.CalleeMethodId = m.Id
            WHERE c.CallerMethodId IN ({idList})
            """).Cast<dynamic>();

        var map = new Dictionary<long, List<string>>();
        foreach (var r in rows)
        {
            var caller = (long)r.CallerMethodId;
            var callee = (string)r.FullName;
            if (!map.TryGetValue(caller, out var list))
                map[caller] = list = new List<string>();
            list.Add(callee);
        }

        return map.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    // ===== Interface Map =====

    private static Dictionary<long, string[]> BuildInterfaceMap(
        Microsoft.Data.Sqlite.SqliteConnection conn, HashSet<long> typeIds)
    {
        if (typeIds.Count == 0) return new();

        var idList = string.Join(",", typeIds);

        var rows = conn.Query(
            $"""
            SELECT i.ChildTypeId, t.FullName
            FROM Inheritance i
            JOIN Types t ON i.ParentTypeId = t.Id
            WHERE i.IsInterface = 1 AND i.ChildTypeId IN ({idList})
            """).Cast<dynamic>();

        var map = new Dictionary<long, List<string>>();
        foreach (var r in rows)
        {
            var child = (long)r.ChildTypeId;
            var iface = (string)r.FullName;
            if (!map.TryGetValue(child, out var list))
                map[child] = list = new List<string>();
            list.Add(iface);
        }

        return map.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
    }

    // ===== Lightweight index items (subset of full entities) =====

    private class MethodIndexItem
    {
        public long Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ReturnType { get; set; }
        public string? ParamTypes { get; set; }
        public long SourceId { get; set; }
        public string ParentFullName { get; set; } = string.Empty;
    }

    private class FieldIndexItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? FieldType { get; set; }
        public long SourceId { get; set; }
        public string ParentFullName { get; set; } = string.Empty;
    }

    private class PropertyIndexItem
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? PropertyType { get; set; }
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }
        public long SourceId { get; set; }
        public string ParentFullName { get; set; } = string.Empty;
    }
}
