namespace RimAnalyzer.Models;

// 对应 Methods 表的实体模型
public class MethodEntity
{
    public long Id { get; set; }
    public long TypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string? ReturnType { get; set; }
    public bool IsStatic { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsAccessor { get; set; }
    public string? Accessibility { get; set; }
    public long SourceId { get; set; }
}
