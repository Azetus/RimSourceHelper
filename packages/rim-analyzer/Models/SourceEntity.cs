namespace RimAnalyzer.Models;

// 对应 Sources 表的实体模型
public class SourceEntity
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? PackageId { get; set; }
    public string? AssemblyPath { get; set; }
    public string? RootPath { get; set; }
}
