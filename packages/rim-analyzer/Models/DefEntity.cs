namespace RimAnalyzer.Models;

// 对应 Defs 表的实体模型
public class DefEntity
{
    public long Id { get; set; }
    public string DefName { get; set; } = string.Empty;
    public string DefType { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? ParentDef { get; set; }
    public string? SourceFile { get; set; }
    public string? MergedJson { get; set; }
    public long SourceId { get; set; }
}
