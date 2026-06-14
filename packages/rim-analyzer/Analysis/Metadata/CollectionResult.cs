using Mono.Cecil;
using RimAnalyzer.Models;

namespace RimAnalyzer.Analysis.Metadata;

// MetadataCollector 的完整输出，包含类型元数据和 MethodDefinition→Entity 映射
public class CollectionResult
{
    public List<TypeMetadata> Types { get; init; } = new();
    // Cecil MethodDefinition → 对应的 MethodEntity（Signature 已赋值，Id 未赋值）
    public Dictionary<MethodDefinition, MethodEntity> MethodMap { get; init; } = new();
}
