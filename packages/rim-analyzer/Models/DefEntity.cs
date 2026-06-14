namespace RimAnalyzer.Models;

// 对应 Defs 表的实体模型
public class DefEntity
{
    public long Id { get; set; }
    public string? DefName { get; set; }
    public string DefType { get; set; } = string.Empty;
    public string? ParentDef { get; set; }
    public string? Label { get; set; }
    public string? Description { get; set; }
    public bool IsAbstract { get; set; }
    public string RawXml { get; set; } = string.Empty;
    public string? SourceFile { get; set; }
    public long SourceId { get; set; }
}
