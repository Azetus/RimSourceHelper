using Mono.Cecil;

namespace RimAnalyzer.Analysis;

// 从游戏根目录定位程序集和游戏数据。
public class GamePathResolver
{
    private const string MainAssemblyName = "Assembly-CSharp.dll";
    private const string DataRelative = "Data";
    private readonly string legacyManagedDir;

    public string GameRoot { get; }
    public string ManagedDir { get; }
    public string MainAssemblyPath { get; }
    public string DataDir { get; }

    public GamePathResolver(string gamePath)
    {
        GameRoot = Path.GetFullPath(gamePath);
        DataDir = Path.Combine(GameRoot, DataRelative);

        // 优先使用 Waylith 根目录中的主程序集，再回退到 Unity 的 Managed 目录。
        legacyManagedDir = Path.Combine(GameRoot, "RimWorldWin64_Data", "Managed");
        ManagedDir = File.Exists(Path.Combine(GameRoot, MainAssemblyName)) ? GameRoot : legacyManagedDir;

        MainAssemblyPath = Path.Combine(ManagedDir, MainAssemblyName);
    }

    // 验证关键路径是否存在
    public void Validate()
    {
        if (!Directory.Exists(GameRoot))
            throw new DirectoryNotFoundException($"Game root not found: {GameRoot}");
        if (!File.Exists(MainAssemblyPath))
            throw new FileNotFoundException(
                $"Main assembly not found. Checked: {Path.Combine(GameRoot, MainAssemblyName)} and {Path.Combine(legacyManagedDir, MainAssemblyName)}.");
        if (!Directory.Exists(DataDir))
            throw new DirectoryNotFoundException($"Data directory not found: {DataDir}");
    }

    // 检测已安装的 DLC（Data 目录下除 Core 外的子目录）
    public List<string> DetectDlcs()
    {
        var dlcDirs = new List<string>();
        foreach (var dir in Directory.GetDirectories(DataDir))
        {
            var name = Path.GetFileName(dir);
            if (name != "Core" && Directory.Exists(Path.Combine(dir, "Defs")))
                dlcDirs.Add(name);
        }
        return dlcDirs;
    }

    // 获取所有 Defs 目录（Core + 各 DLC）
    public List<(string Name, string DefsPath)> GetAllDefsPaths()
    {
        var result = new List<(string, string)>();

        var coreDefs = Path.Combine(DataDir, "Core", "Defs");
        if (Directory.Exists(coreDefs))
            result.Add(("Core", coreDefs));

        foreach (var dlc in DetectDlcs())
        {
            var dlcDefs = Path.Combine(DataDir, dlc, "Defs");
            if (Directory.Exists(dlcDefs))
                result.Add((dlc, dlcDefs));
        }

        return result;
    }

    // 从 Assembly-CSharp.dll 读取游戏版本号（如 "1.6"）
    public string DetectGameVersion()
    {
        try
        {
            using var asm = AssemblyDefinition.ReadAssembly(
                MainAssemblyPath,
                new ReaderParameters { ReadSymbols = false }
            );
            return asm.Name.Version.ToString();
        }
        catch
        {
            return "unknown";
        }
    }
}
