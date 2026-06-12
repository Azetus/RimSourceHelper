using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
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

    // 占位符：实际分析逻辑将在后续阶段逐步实现
    private static async Task<BuildResult> ExecuteBuildAsync(BuildOptions options)
    {
        Log($"[INFO] Loading assemblies: {string.Join(", ", options.Assemblies)}");
        await Task.Delay(50);

        Log($"[INFO] Reference paths: {string.Join(", ", options.References)}");
        await Task.Delay(50);

        // 阶段1：元数据收集（Types, Methods, Fields, Properties）
        Log("[INFO] Collecting metadata (types, methods, fields, properties)...");
        await Task.Delay(50);

        // 阶段2：继承关系构建
        Log("[INFO] Building inheritance graph...");
        await Task.Delay(50);

        // 阶段3：IL 调用图分析
        Log("[INFO] Analyzing IL call graph (call/callvirt/newobj)...");
        await Task.Delay(50);

        // 阶段4：Defs XML 解析
        Log($"[INFO] Parsing defs XML from: {options.DefsPath}");
        await Task.Delay(50);

        // 阶段5：写入 SQLite
        Log($"[INFO] Writing database to: {options.Output}");
        await Task.Delay(50);

        Log("[INFO] Build complete.");

        return new BuildResult
        {
            Status = "success",
            Types = 0,
            Methods = 0,
            Calls = 0,
            Defs = 0
        };
    }

    // 日志输出到 stderr，避免污染 stdout 的 JSON 结果
    private static void Log(string message)
    {
        Console.Error.WriteLine(message);
    }
}
