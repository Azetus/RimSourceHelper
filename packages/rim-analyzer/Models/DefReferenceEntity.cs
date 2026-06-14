namespace RimAnalyzer.Models;

// 对应 DefReferences 表的实体模型（仅记录引用关系，不含具体路径）
public class DefReferenceEntity
{
    public long SourceDefId { get; set; }
    public string TargetDefName { get; set; } = string.Empty;
}
