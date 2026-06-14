using System.Xml.Linq;
using RimAnalyzer.Models;

namespace RimAnalyzer.Analysis.Defs;

// 扫描 XML 文件，提取每个 Def 元素为 DefEntity
public static class DefParser
{
    // 扫描目录下所有 XML，返回解析出的 DefEntity 列表
    public static List<DefEntity> ParseDirectory(string defsPath, string basePath, long sourceId, Action<string> log)
    {
        if (!Directory.Exists(defsPath))
        {
            log($"[WARN] Defs directory not found: {defsPath}");
            return [];
        }

        var xmlFiles = Directory.GetFiles(defsPath, "*.xml", SearchOption.AllDirectories);
        return ParseFilesInternal(xmlFiles, basePath, sourceId, log);
    }

    // 解析指定文件列表
    public static List<DefEntity> ParseFiles(IEnumerable<string> filePaths, string basePath, long sourceId, Action<string> log)
    {
        return ParseFilesInternal(filePaths.ToArray(), basePath, sourceId, log);
    }

    private static List<DefEntity> ParseFilesInternal(string[] files, string basePath, long sourceId, Action<string> log)
    {
        var result = new List<DefEntity>();

        foreach (var file in files)
        {
            try
            {
                var defs = ParseFile(file, basePath, sourceId);
                result.AddRange(defs);
            }
            catch (Exception ex)
            {
                log($"[WARN] Failed to parse XML: {file} — {ex.Message}");
            }
        }

        log($"[INFO] Parsed {result.Count} defs from {files.Length} XML files.");
        return result;
    }

    private static List<DefEntity> ParseFile(string filePath, string basePath, long sourceId)
    {
        var doc = XDocument.Load(filePath);
        if (doc.Root?.Name.LocalName != "Defs")
            return [];

        var relativePath = Path.GetRelativePath(basePath, filePath).Replace('\\', '/');
        var result = new List<DefEntity>();

        foreach (var element in doc.Root.Elements())
        {
            var defType = element.Name.LocalName;
            var defName = element.Element("defName")?.Value ?? element.Attribute("Name")?.Value;
            var isAbstract = element.Attribute("Abstract")?.Value == "True";

            result.Add(new DefEntity
            {
                DefName = defName,
                DefType = defType,
                ParentDef = element.Attribute("ParentName")?.Value,
                Label = element.Element("label")?.Value,
                Description = element.Element("description")?.Value,
                IsAbstract = isAbstract,
                RawXml = element.ToString(),
                SourceFile = relativePath,
                SourceId = sourceId
            });
        }

        return result;
    }
}
