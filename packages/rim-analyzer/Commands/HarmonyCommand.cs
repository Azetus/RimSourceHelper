using System.CommandLine;
using System.Text.Json;
using RimAnalyzer.Analysis;
using RimAnalyzer.Analysis.Harmony;

namespace RimAnalyzer.Commands;

// harmony 子命令：实时分析 Mod 中的 Harmony Patch 声明（无状态，不需 DB）
public static class HarmonyCommand
{
    public static Command Create()
    {
        var modPathOption = new Option<string>("--mod-path")
        {
            Description = "Mod root directory or DLL path",
            Required = true
        };

        var gamePathOption = new Option<string>("--game-path")
        {
            Description = "RimWorld game root (for resolving Harmony DLL references)"
        };

        var command = new Command("harmony", "Analyze Harmony patches in a mod (stateless)")
        {
            modPathOption,
            gamePathOption
        };

        command.SetAction((parseResult, _) =>
        {
            var modPath = parseResult.GetValue(modPathOption)!;
            var gamePath = parseResult.GetValue(gamePathOption);

            try
            {
                var result = Execute(modPath, gamePath);
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

    private static object Execute(string modPath, string? gamePath)
    {
        // 支持传入单个 DLL 或 Mod 目录
        string[] dllPaths;
        string modName;

        if (File.Exists(modPath) && modPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            dllPaths = [modPath];
            modName = Path.GetFileNameWithoutExtension(modPath);
        }
        else
        {
            var resolver = new ModResolver(modPath);
            resolver.Resolve();
            dllPaths = resolver.AssemblyPaths.ToArray();
            modName = resolver.Name;
        }

        if (dllPaths.Length == 0)
            throw new InvalidOperationException("No DLL files found.");

        // 构建引用路径：DLL 所在目录 + 游戏 Managed 目录（解析 0Harmony.dll 等依赖）
        var refPaths = dllPaths.Select(Path.GetDirectoryName).Where(d => d is not null).Distinct().ToList();
        if (gamePath is not null)
            refPaths.Add(Path.Combine(gamePath, "RimWorldWin64_Data", "Managed"));

        var assemblies = AssemblyLoader.Load(dllPaths, refPaths.ToArray()!, Log);
        if (assemblies.Count == 0)
            throw new InvalidOperationException("No assemblies were loaded.");

        try
        {
            var patches = HarmonyAnalyzer.Analyze(assemblies, Log);

            return new
            {
                status = "success",
                modName,
                patchCount = patches.Count,
                patches = patches.Select(p => new
                {
                    targetType = p.TargetType,
                    targetMethod = p.TargetMethod,
                    patchType = p.PatchType,
                    patchClass = p.PatchClass,
                    patchMethod = p.PatchMethod,
                    priority = p.Priority
                })
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
