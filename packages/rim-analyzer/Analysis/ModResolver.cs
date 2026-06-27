using System.Xml.Linq;

namespace RimAnalyzer.Analysis;

// 解析 Mod 目录结构：读取 About.xml、解析 LoadFolders.xml、定位文件
public class ModResolver
{
    public string ModRoot { get; }
    public string Name { get; private set; } = string.Empty;
    public string? PackageId { get; private set; }
    public List<string> AssemblyPaths { get; private set; } = new();
    public List<string> DefFiles { get; private set; } = new();
    public List<string> PatchFiles { get; private set; } = new();

    private static readonly HashSet<string> SkipDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "Textures", "Sounds", "Languages", "Source"
    };

    private record LoadFolder(string FolderName);

    public ModResolver(string modPath)
    {
        ModRoot = Path.GetFullPath(modPath);
    }

    // 解析 Mod 元数据和文件（gameVersion 用于 LoadFolders 版本匹配，null 则用 legacy 回退）
    public void Resolve(string? gameVersion = null)
    {
        if (!Directory.Exists(ModRoot))
            throw new DirectoryNotFoundException($"Mod directory not found: {ModRoot}");

        ParseAboutXml();
        var loadDirs = ResolveLoadFolders(gameVersion);
        FindAssemblies(loadDirs);
        FindDefFiles(loadDirs);
        FindPatchFiles(loadDirs);
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

    // ===== LoadFolders.xml 解析（复制 RimWorld 逻辑）=====

    private List<string> ResolveLoadFolders(string? gameVersion)
    {
        var dict = ParseLoadFoldersXml();
        List<LoadFolder>? folders = null;

        if (dict.Count > 0 && gameVersion is not null)
            folders = MatchVersion(dict, gameVersion);

        if (folders is null)
            return LegacyLoadFolders(gameVersion);

        // 反向遍历（后写优先，与 RimWorld 一致）
        var result = new List<string>();
        for (int i = folders.Count - 1; i >= 0; i--)
        {
            var name = folders[i].FolderName;
            result.Add(string.IsNullOrEmpty(name) ? ModRoot : Path.Combine(ModRoot, name));
        }
        return result;
    }

    private Dictionary<string, List<LoadFolder>> ParseLoadFoldersXml()
    {
        var result = new Dictionary<string, List<LoadFolder>>();
        var path = Path.Combine(ModRoot, "LoadFolders.xml");
        if (!File.Exists(path))
            return result;

        var doc = XDocument.Load(path);
        if (doc.Root is null)
            return result;

        foreach (var node in doc.Root.Elements())
        {
            var name = node.Name.LocalName.ToLowerInvariant();
            if (name.StartsWith("v"))
                name = name.Substring(1);

            if (!result.ContainsKey(name))
                result[name] = new List<LoadFolder>();

            foreach (var li in node.Elements("li"))
            {
                var folderName = li.Value.Trim();
                if (folderName == "/" || folderName == "\\")
                    folderName = "";
                result[name].Add(new LoadFolder(folderName));
            }
        }

        return result;
    }

    private List<LoadFolder>? MatchVersion(Dictionary<string, List<LoadFolder>> dict, string gameVersion)
    {
        // 1. 精确匹配
        if (dict.TryGetValue(gameVersion, out var folders))
            return folders;

        // 2. 最佳匹配：最高版本 ≤ 当前版本
        Version? parsedCurrent = null;
        try { parsedCurrent = new Version(gameVersion); } catch { }

        if (parsedCurrent is not null)
        {
            var candidates = new List<(Version Ver, string Key)>();
            foreach (var key in dict.Keys)
            {
                if (key == "default" || !key.Contains('.'))
                    continue;
                try
                {
                    var ver = new Version(key);
                    if (ver <= parsedCurrent)
                        candidates.Add((ver, key));
                }
                catch { }
            }
            candidates.Sort((a, b) => b.Ver.CompareTo(a.Ver)); // 降序
            if (candidates.Count > 0)
                return dict[candidates[0].Key];
        }

        // 3. default 回退
        if (dict.TryGetValue("default", out var defFolders))
            return defFolders;

        return null;
    }

    private List<string> LegacyLoadFolders(string? gameVersion)
    {
        var result = new List<string>();
        result.Add(ModRoot);

        Version? parsedCurrent = null;
        try { parsedCurrent = new Version(gameVersion!); } catch { }

        if (parsedCurrent is not null)
        {
            // 精确版本目录
            var exactDir = Path.Combine(ModRoot, gameVersion!);
            if (Directory.Exists(exactDir))
                result.Insert(0, exactDir); // 版本目录优先级高于根目录

            // 扫描所有版本目录，取最高 ≤ 当前版本
            Version? bestVer = null;
            string? bestDir = null;
            foreach (var dir in Directory.GetDirectories(ModRoot))
            {
                var dirName = Path.GetFileName(dir);
                try
                {
                    var ver = new Version(dirName);
                    if (ver <= parsedCurrent && (bestVer is null || ver > bestVer))
                    {
                        bestVer = ver;
                        bestDir = dir;
                    }
                }
                catch { }
            }
            if (bestDir is not null && bestDir != exactDir)
                result.Insert(0, bestDir);
        }

        return result;
    }

    // ===== 文件扫描（目录列表 + TryAdd 去重）=====

    private void FindAssemblies(List<string> loadDirs)
    {
        AssemblyPaths = ScanFiles("*.dll", loadDirs);
    }

    private void FindDefFiles(List<string> loadDirs)
    {
        DefFiles = ScanFiles("*.xml", loadDirs)
            .Where(f => !Path.GetFileName(f).Equals("About.xml", StringComparison.OrdinalIgnoreCase))
            .Where(f => !Path.GetFileName(f).Equals("LoadFolders.xml", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void FindPatchFiles(List<string> loadDirs)
    {
        PatchFiles = ScanFiles("*.xml", loadDirs)
            .Where(f => !Path.GetFileName(f).Equals("About.xml", StringComparison.OrdinalIgnoreCase))
            .Where(f => !Path.GetFileName(f).Equals("LoadFolders.xml", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private List<string> ScanFiles(string pattern, List<string> loadDirs)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();

        foreach (var dir in loadDirs)
        {
            if (!Directory.Exists(dir))
                continue;

            foreach (var file in Directory.GetFiles(dir, pattern, SearchOption.AllDirectories))
            {
                var parentDir = Path.GetFileName(Path.GetDirectoryName(file));
                if (parentDir is not null && SkipDirs.Contains(parentDir))
                    continue;

                var relPath = Path.GetRelativePath(dir, file);
                if (seen.Add(relPath))
                    result.Add(file);
            }
        }

        return result;
    }
}
