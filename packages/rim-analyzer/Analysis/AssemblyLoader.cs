using Mono.Cecil;

namespace RimAnalyzer.Analysis;

// 加载目标 DLL 为 Cecil AssemblyDefinition，配置引用解析器
public static class AssemblyLoader
{
    public static IReadOnlyList<AssemblyDefinition> Load(
        string[] assemblyPaths, string[] referencePaths, Action<string> log)
    {
        var resolver = new DefaultAssemblyResolver();

        // 注册引用目录，使 Cecil 能跨程序集解析类型引用
        foreach (var refPath in referencePaths)
        {
            if (Directory.Exists(refPath))
            {
                resolver.AddSearchDirectory(refPath);
                log($"[INFO] Registered reference directory: {refPath}");
            }
            else
            {
                log($"[WARN] Reference directory not found, skipping: {refPath}");
            }
        }

        var readerParams = new ReaderParameters
        {
            AssemblyResolver = resolver,
            ReadSymbols = false
        };

        var assemblies = new List<AssemblyDefinition>();

        foreach (var path in assemblyPaths)
        {
            try
            {
                var asm = AssemblyDefinition.ReadAssembly(path, readerParams);
                assemblies.Add(asm);
                log($"[INFO] Loaded assembly: {asm.Name.Name} ({path})");
            }
            catch (Exception ex)
            {
                log($"[WARN] Failed to load assembly: {path} — {ex.Message}");
            }
        }

        log($"[INFO] Loaded {assemblies.Count}/{assemblyPaths.Length} assemblies.");
        return assemblies;
    }
}
