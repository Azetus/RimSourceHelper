namespace RimAnalyzer.Models;

// 对应 Fields 表的实体模型
public class FieldEntity
{
    public long Id { get; set; }
    public long TypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? FieldType { get; set; }
    public bool IsStatic { get; set; }
    public string? Accessibility { get; set; }
}
