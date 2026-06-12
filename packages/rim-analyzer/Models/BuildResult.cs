using System.Text.Json.Serialization;

namespace RimAnalyzer.Models;

// build 命令的 JSON 输出结果，序列化后写入 stdout
public class BuildResult
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("types")]
    public int Types { get; init; }

    [JsonPropertyName("methods")]
    public int Methods { get; init; }

    [JsonPropertyName("calls")]
    public int Calls { get; init; }

    [JsonPropertyName("defs")]
    public int Defs { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }
}
