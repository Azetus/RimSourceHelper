using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using Mono.Cecil;
using RimAnalyzer.Analysis;
using RimAnalyzer.Analysis.CallGraph;
using RimAnalyzer.Analysis.Metadata;
using RimAnalyzer.Database;
using RimAnalyzer.Models;

namespace RimAnalyzer.Commands;

// add-mod 子命令：将 Mod 的代码和 Defs 增量添加到已有数据库
public static class AddModCommand
{
    public static Command Create()
    {
        var modPathOption = new Option<string>("--mod-path")
        {
            Description = "Mod root directory",
            Required = true
        };

        var dbOption = new Option<string>("--db")
        {
            Description = "Existing database path",
            Required = true
        };

        var gamePathOption = new Option<string>("--game-path")
        {
            Description = "RimWorld game root directory (for reference DLLs)",
            Required = true
        };

        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Enable verbose logging"
        };

        var command = new Command("add-mod", "Add a mod's code and defs to existing database")
        {
            modPathOption,
            dbOption,
            gamePathOption,
            verboseOption
        };

        command.SetAction((parseResult, _) =>
        {
            var options = new AddModOptions
            {
                ModPath = parseResult.GetValue(modPathOption)!,
                Database = parseResult.GetValue(dbOption)!,
                GamePath = parseResult.GetValue(gamePathOption)!,
                Verbose = parseResult.GetValue(verboseOption)
            };

            try
            {
                var result = Execute(options);
                Console.WriteLine(JsonSerializer.Serialize(result));
            }
            catch (Exception ex)
            {
                Console.WriteLine(JsonSerializer.Serialize(
                    new BuildResult { Status = "error", Error = ex.Message }));
                Environment.ExitCode = 1;
            }

            return Task.CompletedTask;
        });

        return command;
    }

    private static BuildResult Execute(AddModOptions options)
    {
        // 解析 Mod 目录
        var mod = new ModResolver(options.ModPath);
        mod.Resolve();
        Log($"[INFO] Mod: {mod.Name} (packageId={mod.PackageId})");
        Log($"[INFO] Found {mod.AssemblyPaths.Count} DLLs, {mod.DefFiles.Count} XML files.");

        if (mod.AssemblyPaths.Count == 0)
        {
            Log("[WARN] No assemblies found in mod. Only Defs will be processed (not yet implemented).");
            return new BuildResult { Status = "success", Types = 0, Methods = 0, Calls = 0, Defs = 0 };
        }

        // 加载 Mod DLL（引用游戏 Managed 目录做 Resolve）
        var gameResolver = new GamePathResolver(options.GamePath);
        var assemblies = AssemblyLoader.Load(
            mod.AssemblyPaths.ToArray(),
            [gameResolver.ManagedDir],
            Log);

        if (assemblies.Count == 0)
            throw new InvalidOperationException("No mod assemblies were loaded successfully.");

        try
        {
            // 元数据收集
            var collection = MetadataCollector.Collect(assemblies, Log);

            // 打开已有数据库（不覆盖）
            using var db = DatabaseContext.Open(options.Database, force: false);

            // 如果已存在同名 Source，先清除旧数据（幂等操作）
            var existing = db.Sources.FindByName(mod.Name);
            if (existing is not null)
            {
                Log($"[INFO] Source '{mod.Name}' already exists, replacing...");
                RemoveSourceData(db, existing.Id);
            }

            // 创建 Source 记录
            var modSource = new SourceEntity
            {
                Name = mod.Name,
                Type = "mod",
                PackageId = mod.PackageId,
                AssemblyPath = mod.AssemblyPaths.FirstOrDefault(),
                RootPath = mod.ModRoot
            };
            var sourceId = db.Sources.Insert(modSource);
            Log($"[INFO] Created source: {modSource.Name} (id={sourceId})");

            // 写入元数据
            var writeResult = MetadataWriter.Write(db, collection.Types, sourceId, Log);

            // 构建 MethodDefinition → Id 映射（包含已有数据库中的方法用于跨 Mod 调用图）
            var sigToId = db.Methods.GetSignatureToIdMap();
            var methodDefToId = new Dictionary<MethodDefinition, long>();
            foreach (var (def, entity) in collection.MethodMap)
            {
                if (sigToId.TryGetValue(entity.Signature, out var id))
                    methodDefToId[def] = id;
            }
            Log($"[INFO] Built method mapping: {methodDefToId.Count} entries.");

            // 调用图分析（Mod 内部调用 + 对 Core 的调用）
            var callPairs = CallGraphAnalyzer.Analyze(assemblies, methodDefToId, Log);
            var callCount = CallGraphWriter.Write(db, callPairs, Log);

            Log("[INFO] Mod added successfully.");

            return new BuildResult
            {
                Status = "success",
                Types = writeResult.Types,
                Methods = writeResult.Methods,
                Calls = callCount,
                Defs = 0
            };
        }
        finally
        {
            foreach (var asm in assemblies)
                asm.Dispose();
        }
    }

    // 删除指定 Source 的所有关联数据
    private static void RemoveSourceData(DatabaseContext db, long sourceId)
    {
        var conn = db.Connection;
        Dapper.SqlMapper.Execute(conn, "DELETE FROM Calls WHERE CallerMethodId IN (SELECT Id FROM Methods WHERE SourceId = @sourceId) OR CalleeMethodId IN (SELECT Id FROM Methods WHERE SourceId = @sourceId)", new { sourceId });
        Dapper.SqlMapper.Execute(conn, "DELETE FROM Inheritance WHERE ParentTypeId IN (SELECT Id FROM Types WHERE SourceId = @sourceId) OR ChildTypeId IN (SELECT Id FROM Types WHERE SourceId = @sourceId)", new { sourceId });
        Dapper.SqlMapper.Execute(conn, "DELETE FROM DefReferences WHERE SourceDefId IN (SELECT Id FROM Defs WHERE SourceId = @sourceId)", new { sourceId });
        Dapper.SqlMapper.Execute(conn, "DELETE FROM Properties WHERE SourceId = @sourceId", new { sourceId });
        Dapper.SqlMapper.Execute(conn, "DELETE FROM Fields WHERE SourceId = @sourceId", new { sourceId });
        Dapper.SqlMapper.Execute(conn, "DELETE FROM Methods WHERE SourceId = @sourceId", new { sourceId });
        Dapper.SqlMapper.Execute(conn, "DELETE FROM Types WHERE SourceId = @sourceId", new { sourceId });
        Dapper.SqlMapper.Execute(conn, "DELETE FROM Defs WHERE SourceId = @sourceId", new { sourceId });
        Dapper.SqlMapper.Execute(conn, "DELETE FROM Sources WHERE Id = @sourceId", new { sourceId });
    }

    private static void Log(string message) => Console.Error.WriteLine(message);
}
