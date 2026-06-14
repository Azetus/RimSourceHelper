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

// build 子命令：分析 DLL 构建知识库数据库
public static class BuildCommand
{
    public static Command Create()
    {
        var assembliesOption = new Option<string[]>("--assemblies")
        {
            Description = "Target DLL paths to analyze (can be specified multiple times)",
            Required = true
        };

        var referencesOption = new Option<string[]>("--references")
        {
            Description = "Reference DLL directories (can be specified multiple times)",
            Required = true
        };

        var defsPathOption = new Option<string>("--defs-path")
        {
            Description = "Defs XML root directory",
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

        var command = new Command("build", "Analyze RimWorld DLLs and build knowledge database")
        {
            assembliesOption,
            referencesOption,
            defsPathOption,
            outputOption,
            forceOption,
            verboseOption
        };

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var options = new BuildOptions
            {
                Assemblies = parseResult.GetValue(assembliesOption)!,
                References = parseResult.GetValue(referencesOption)!,
                DefsPath = parseResult.GetValue(defsPathOption)!,
                Output = parseResult.GetValue(outputOption)!,
                Force = parseResult.GetValue(forceOption),
                Verbose = parseResult.GetValue(verboseOption)
            };

            try
            {
                var result = await ExecuteBuildAsync(options);
                var json = JsonSerializer.Serialize(result);
                Console.WriteLine(json);
            }
            catch (Exception ex)
            {
                var result = new BuildResult { Status = "error", Error = ex.Message };
                var json = JsonSerializer.Serialize(result);
                Console.WriteLine(json);
                Environment.ExitCode = 1;
            }
        });

        return command;
    }

    private static Task<BuildResult> ExecuteBuildAsync(BuildOptions options)
    {
        // 加载目标程序集
        var assemblies = AssemblyLoader.Load(options.Assemblies, options.References, Log);

        if (assemblies.Count == 0)
            throw new InvalidOperationException("No assemblies were loaded successfully.");

        try
        {
            // 阶段1：元数据收集（Types, Methods, Fields, Properties）
            var collection = MetadataCollector.Collect(assemblies, Log);

            // 阶段2：写入元数据到 SQLite
            Log($"[INFO] Writing database to: {options.Output}");
            using var db = DatabaseContext.Open(options.Output, options.Force);
            var writeResult = MetadataWriter.Write(db, collection.Types, Log);

            // 阶段3：构建 MethodDefinition → Id 映射
            var sigToId = db.Methods.GetSignatureToIdMap();
            var methodDefToId = new Dictionary<MethodDefinition, long>();
            foreach (var (def, entity) in collection.MethodMap)
            {
                if (sigToId.TryGetValue(entity.Signature, out var id))
                    methodDefToId[def] = id;
            }
            Log($"[INFO] Built method mapping: {methodDefToId.Count} entries.");

            // 阶段4：IL 调用图分析
            var callPairs = CallGraphAnalyzer.Analyze(assemblies, methodDefToId, Log);

            // 阶段5：写入调用关系
            var callCount = CallGraphWriter.Write(db, callPairs, Log);

            // 阶段6：Defs XML 解析
            Log($"[INFO] Parsing defs XML from: {options.DefsPath}");
            // TODO: DefParser

            Log("[INFO] Build complete.");

            return Task.FromResult(new BuildResult
            {
                Status = "success",
                Types = writeResult.Types,
                Methods = writeResult.Methods,
                Calls = callCount,
                Defs = 0
            });
        }
        finally
        {
            foreach (var asm in assemblies)
                asm.Dispose();
        }
    }

    // 日志输出到 stderr，避免污染 stdout 的 JSON 结果
    private static void Log(string message)
    {
        Console.Error.WriteLine(message);
    }
}
