using System.Xml.Linq;

namespace RimAnalyzer.Analysis;

// 解析 Mod 目录结构：读取 About.xml、定位 DLL 和 Defs 文件
public class ModResolver
{
    public string ModRoot { get; }
    public string Name { get; private set; } = string.Empty;
    public string? PackageId { get; private set; }
    public List<string> AssemblyPaths { get; private set; } = new();
    public List<string> DefFiles { get; private set; } = new();

    // 搜索 DLL 时跳过的目录
    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "Textures", "Sounds", "Languages", "Source", "Patches"
    };

    public ModResolver(string modPath)
    {
        ModRoot = Path.GetFullPath(modPath);
    }

    // 解析 Mod 元数据和文件
    public void Resolve()
    {
        if (!Directory.Exists(ModRoot))
            throw new DirectoryNotFoundException($"Mod directory not found: {ModRoot}");

        ParseAboutXml();
        FindAssemblies();
        FindDefFiles();
    }

    private void ParseAboutXml()
    {
        var aboutPath = Path.Combine(ModRoot, "About", "About.xml");
        if (!File.Exists(aboutPath))
            throw new FileNotFoundException($"About.xml not found: {aboutPath}");

        var doc = XDocument.Load(aboutPath);
        var root = doc.Root;
        Name = root?.Element("name")?.Value ?? Path.GetFileName(ModRoot);
        PackageId = root?.Element("packageId")?.Value;
    }

    // 递归查找所有 DLL 文件
    private void FindAssemblies()
    {
        AssemblyPaths = FindFilesRecursive(ModRoot, "*.dll", SkipDirs);
    }

    // 递归查找所有 XML 文件（后续解析时再过滤非 Def 文件）
    private void FindDefFiles()
    {
        DefFiles = FindFilesRecursive(ModRoot, "*.xml", SkipDirs)
            .Where(f => !f.EndsWith("About.xml", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith("LoadFolders.xml", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static List<string> FindFilesRecursive(string root, string pattern, HashSet<string> skipDirs)
    {
        var results = new List<string>();
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            try
            {
                results.AddRange(Directory.GetFiles(dir, pattern));
                foreach (var subDir in Directory.GetDirectories(dir))
                {
                    var name = Path.GetFileName(subDir);
                    if (!skipDirs.Contains(name))
                        stack.Push(subDir);
                }
            }
            catch (UnauthorizedAccessException) { }
        }

        return results;
    }
}
