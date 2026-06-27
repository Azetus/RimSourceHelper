using System.CommandLine;
using System.Text.Json;
using Mono.Cecil;
using RimAnalyzer.Analysis;
using RimAnalyzer.Analysis.CallGraph;
using RimAnalyzer.Analysis.Defs;
using RimAnalyzer.Analysis.Metadata;
using RimAnalyzer.Database;
using RimAnalyzer.Models;

namespace RimAnalyzer.Commands;

// build 子命令：从游戏根目录分析 Core + DLC，始终全量重建数据库
public static class BuildCommand
{
    public static Command Create()
    {
        var gamePathOption = new Option<string>("--game-path")
        {
            Description = "RimWorld game root directory",
            Required = true
        };

        var outputOption = new Option<string>("--output")
        {
            Description = "Output SQLite database path",
            Required = true
        };

        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Enable verbose logging"
        };

        var command = new Command("build", "Analyze RimWorld Core + DLCs and build knowledge database")
        {
            gamePathOption,
            outputOption,
            verboseOption
        };

        command.SetAction((parseResult, _) =>
        {
            var options = new BuildOptions
            {
                GamePath = parseResult.GetValue(gamePathOption)!,
                Output = parseResult.GetValue(outputOption)!,
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

    private static BuildResult Execute(BuildOptions options)
    {
        // 解析游戏路径
        var resolver = new GamePathResolver(options.GamePath);
        resolver.Validate();
        Log($"[INFO] Game root: {resolver.GameRoot}");

        // 加载程序集
        var assemblies = AssemblyLoader.Load(
            [resolver.MainAssemblyPath],
            [resolver.ManagedDir],
            Log);

        if (assemblies.Count == 0)
            throw new InvalidOperationException("Failed to load Assembly-CSharp.dll.");

        try
        {
            // 元数据收集
            var collection = MetadataCollector.Collect(assemblies, Log);

            // 始终全量重建：删除已有 DB 并创建新数据库
            using var db = DatabaseContext.Open(options.Output, force: true);

            // 创建 Source 记录
            var coreSource = new SourceEntity
            {
                Name = "RimWorld Core",
                Type = "core",
                PackageId = "Ludeon.RimWorld",
                AssemblyPath = resolver.MainAssemblyPath,
                RootPath = resolver.GameRoot
            };
            var sourceId = db.Sources.Insert(coreSource);
            Log($"[INFO] Created source: {coreSource.Name} (id={sourceId})");

            // 存储 game-path 和 game-version 供 decompile 等命令使用
            db.Metadata.Set("game_path", resolver.GameRoot);
            db.Metadata.Set("game_version", resolver.DetectGameVersion());
            Log($"[INFO] Game version: {resolver.DetectGameVersion()}");

            // 写入元数据
            var writeResult = MetadataWriter.Write(db, collection.Types, sourceId, Log);

            // 构建 MethodDefinition → Id 映射
            var sigToId = db.Methods.GetSignatureToIdMap();
            var methodDefToId = new Dictionary<MethodDefinition, long>();
            foreach (var (def, entity) in collection.MethodMap)
            {
                if (sigToId.TryGetValue(entity.Signature, out var id))
                    methodDefToId[def] = id;
            }
            Log($"[INFO] Built method mapping: {methodDefToId.Count} entries.");

            // 构建 FieldDefinition → Id 映射
            var fieldSigToId = db.Fields.GetSignatureToIdMap();
            var fieldDefToId = new Dictionary<FieldDefinition, long>();
            foreach (var (def, entity) in collection.FieldMap)
            {
                var sig = $"{def.DeclaringType.FullName}.{def.Name}";
                if (fieldSigToId.TryGetValue(sig, out var id))
                    fieldDefToId[def] = id;
            }
            Log($"[INFO] Built field mapping: {fieldDefToId.Count} entries.");

            // 调用图分析（含字段访问）
            var graphResult = CallGraphAnalyzer.Analyze(assemblies, methodDefToId, fieldDefToId, Log);
            var callCount = CallGraphWriter.Write(db, graphResult.Calls, Log);
            if (graphResult.FieldAccesses.Count > 0)
            {
                var faCount = db.FieldAccesses.BulkInsert(graphResult.FieldAccesses);
                Log($"[INFO] Inserted {faCount} field accesses.");
            }

            // Defs 解析（Core + DLCs）
            var allDefs = new List<DefEntity>();

            var coreDefsPath = Path.Combine(resolver.DataDir, "Core", "Defs");
            allDefs.AddRange(DefParser.ParseDirectory(coreDefsPath, resolver.GameRoot, sourceId, Log));

            foreach (var dlcName in resolver.DetectDlcs())
            {
                var dlcSource = new SourceEntity
                {
                    Name = dlcName,
                    Type = "dlc",
                    PackageId = $"Ludeon.RimWorld.{dlcName}",
                    RootPath = Path.Combine(resolver.DataDir, dlcName)
                };
                var dlcSourceId = db.Sources.Insert(dlcSource);

                var dlcDefsPath = Path.Combine(resolver.DataDir, dlcName, "Defs");
                allDefs.AddRange(DefParser.ParseDirectory(dlcDefsPath, resolver.GameRoot, dlcSourceId, Log));
            }

            var defResult = DefWriter.Write(db, allDefs, Log);

            Log("[INFO] Build complete.");

            return new BuildResult
            {
                Status = "success",
                Types = writeResult.Types,
                Methods = writeResult.Methods,
                Calls = callCount,
                Defs = defResult.Defs
            };
        }
        finally
        {
            foreach (var asm in assemblies)
                asm.Dispose();
        }
    }

    private static void Log(string message) => Console.Error.WriteLine(message);
}
