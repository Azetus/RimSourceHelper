namespace RimAnalyzer.Models;

// build 命令选项：仅需游戏根目录和输出路径
public class BuildOptions
{
    public required string GamePath { get; init; }
    public required string Output { get; init; }
    public bool Verbose { get; init; }
}
