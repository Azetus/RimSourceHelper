namespace RimAnalyzer.Models;

// MetadataCollector 的输出：一个类型及其所有成员
public class TypeMetadata
{
    public TypeEntity Type { get; set; } = new();
    public List<MethodEntity> Methods { get; set; } = new();
    public List<FieldEntity> Fields { get; set; } = new();
    public List<PropertyEntity> Properties { get; set; } = new();
    public List<string> Interfaces { get; set; } = new();
}
