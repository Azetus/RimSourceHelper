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

// build 子命令：从游戏根目录分析 Core + DLC 并构建知识库
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

        var forceOption = new Option<bool>("--force")
        {
            Description = "Force overwrite existing database"
        };

        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Enable verbose logging"
        };

        var command = new Command("build", "Analyze RimWorld Core + DLCs and build knowledge database")
        {
            gamePathOption,
            outputOption,
            forceOption,
            verboseOption
        };

        command.SetAction((parseResult, _) =>
        {
            var options = new BuildOptions
            {
                GamePath = parseResult.GetValue(gamePathOption)!,
                Output = parseResult.GetValue(outputOption)!,
                Force = parseResult.GetValue(forceOption),
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

            // 打开数据库
            using var db = DatabaseContext.Open(options.Output, options.Force);

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

            // 调用图分析
            var callPairs = CallGraphAnalyzer.Analyze(assemblies, methodDefToId, Log);
            var callCount = CallGraphWriter.Write(db, callPairs, Log);

            // 检测 DLC（Defs 解析 TODO）
            var dlcs = resolver.DetectDlcs();
            if (dlcs.Count > 0)
                Log($"[INFO] Detected DLCs: {string.Join(", ", dlcs)} (Defs parsing not yet implemented)");

            Log("[INFO] Build complete.");

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

    private static void Log(string message) => Console.Error.WriteLine(message);
}
