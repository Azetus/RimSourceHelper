namespace RimAnalyzer.Models;

// 对应 Properties 表的实体模型
public class PropertyEntity
{
    public long Id { get; set; }
    public long TypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PropertyType { get; set; }
    public bool HasGetter { get; set; }
    public bool HasSetter { get; set; }
    public string? Accessibility { get; set; }
}
