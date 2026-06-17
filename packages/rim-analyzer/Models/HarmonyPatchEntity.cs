namespace RimAnalyzer.Models;

// 对应 HarmonyPatches 表的实体模型
public class HarmonyPatchEntity
{
    public long Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string? TargetMethod { get; set; }
    public string PatchType { get; set; } = string.Empty;
    public string PatchClass { get; set; } = string.Empty;
    public string PatchMethod { get; set; } = string.Empty;
    public string? TargetParams { get; set; }
    public string? Priority { get; set; }
    public long SourceId { get; set; }
}
