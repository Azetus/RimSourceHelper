using System.Xml.Linq;
using RimAnalyzer.Models;

namespace RimAnalyzer.Analysis.Defs;

// 扫描 <Patch> 文件，提取 Operation 信息
public static class XmlPatchParser
{
    public static List<XmlPatchEntity> ParseFiles(IEnumerable<string> filePaths, string basePath, long sourceId, Action<string> log)
    {
        var result = new List<XmlPatchEntity>();

        foreach (var file in filePaths)
        {
            try
            {
                var patch = ParseFile(file, basePath, sourceId);
                if (patch is not null)
                    result.Add(patch);
            }
            catch (Exception ex)
            {
                log($"[WARN] Failed to parse Patch XML: {file} — {ex.Message}");
            }
        }

        log($"[INFO] Parsed {result.Count} XML patches from {filePaths.Count()} files.");
        return result;
    }

    private static XmlPatchEntity? ParseFile(string filePath, string basePath, long sourceId)
    {
        var doc = XDocument.Load(filePath);
        if (doc.Root is null || doc.Root.Name.LocalName != "Patch")
            return null;

        var classes = new List<string>();
        var xpaths = new List<string>();
        CollectOperations(doc.Root, classes, xpaths);

        return new XmlPatchEntity
        {
            SourceId = sourceId,
            OperationClasses = classes.Count > 0 ? string.Join("\n", classes) : null,
            TargetXPaths = xpaths.Count > 0 ? string.Join("\n", xpaths) : null,
            RawXml = doc.Root.ToString(),
            SourceFile = Path.GetRelativePath(basePath, filePath).Replace('\\', '/')
        };
    }

    // 递归收集所有 Operation 的 Class 和 XPath（含嵌套）
    private static void CollectOperations(XElement element, List<string> classes, List<string> xpaths)
    {
        var cls = element.Attribute("Class")?.Value;
        if (cls is not null)
        {
            classes.Add(cls);
            var xpath = element.Element("xpath")?.Value;
            if (xpath is not null)
                xpaths.Add(xpath.Trim());
        }
        foreach (var child in element.Elements())
            CollectOperations(child, classes, xpaths);
    }
}
