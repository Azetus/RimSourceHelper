namespace RimAnalyzer.Models;

// 对应 DefReferences 表的实体模型
public class DefReferenceEntity
{
    public long SourceDefId { get; set; }
    public string TargetDefName { get; set; } = string.Empty;
    public string? FieldPath { get; set; }
}
