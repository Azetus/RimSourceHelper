namespace RimAnalyzer.Models;

// 对应 XmlPatches 表的实体模型
public class XmlPatchEntity
{
    public long Id { get; set; }
    public long SourceId { get; set; }
    public string? TargetXPaths { get; set; }
    public string? OperationClasses { get; set; }
    public string RawXml { get; set; } = string.Empty;
    public string? SourceFile { get; set; }
}
