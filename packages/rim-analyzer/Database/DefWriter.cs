using System.Text.RegularExpressions;
using System.Xml.Linq;
using RimAnalyzer.Models;

namespace RimAnalyzer.Database;

// Def 写入编排：插入 Defs + 引用检测 + 插入 DefReferences
public static partial class DefWriter
{
    public record DefWriteResult(int Defs, int References);

    public static DefWriteResult Write(DatabaseContext db, List<DefEntity> defs, Action<string> log)
    {
        if (defs.Count == 0)
            return new DefWriteResult(0, 0);

        // 步骤1：写入所有 Def
        var defCount = db.Defs.BulkInsert(defs);
        log($"[INFO] Inserted {defCount} defs.");

        // 步骤2：收集所有已知 defName（DB 全局）
        var allDefNames = db.Defs.GetAllDefNames();

        // 步骤3：获取本次写入的 Def 的 Id 映射
        var sourceIds = defs.Select(d => d.SourceId).Distinct().ToArray();
        var defIdMap = new Dictionary<string, long>();
        foreach (var sid in sourceIds)
        {
            foreach (var (name, id) in db.Defs.GetDefNameToIdMap(sid))
                defIdMap.TryAdd(name, id);
        }

        // 步骤4：扫描引用
        var references = DetectReferences(defs, allDefNames, defIdMap);
        var refCount = references.Count > 0 ? db.DefReferences.BulkInsert(references) : 0;
        log($"[INFO] Detected {refCount} def references.");

        return new DefWriteResult(defCount, refCount);
    }

    // 扫描每个 Def 的 XML 文本节点，精确匹配已知 defName
    private static List<DefReferenceEntity> DetectReferences(
        List<DefEntity> defs, HashSet<string> allDefNames, Dictionary<string, long> defIdMap)
    {
        var references = new List<DefReferenceEntity>();

        foreach (var def in defs)
        {
            if (def.DefName is null || !defIdMap.TryGetValue(def.DefName, out var defId))
                continue;

            var found = new HashSet<string>();

            try
            {
                var element = XElement.Parse(def.RawXml);
                foreach (var textNode in element.DescendantNodes().OfType<XText>())
                {
                    var value = textNode.Value.Trim();
                    if (IsValidDefReference(value, def.DefName, allDefNames))
                        found.Add(value);
                }
            }
            catch { }

            foreach (var target in found)
                references.Add(new DefReferenceEntity { SourceDefId = defId, TargetDefName = target });
        }

        return references;
    }

    // 过滤非 Def 引用的文本值
    private static bool IsValidDefReference(string value, string selfDefName, HashSet<string> knownDefNames)
    {
        if (value.Length < 2 || value.Length > 100) return false;
        if (value == selfDefName) return false;
        if (value.Contains(' ')) return false;
        if (NumericPattern().IsMatch(value)) return false;
        if (value is "true" or "false" or "True" or "False") return false;
        return knownDefNames.Contains(value);
    }

    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex NumericPattern();
}
