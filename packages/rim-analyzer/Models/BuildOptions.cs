namespace RimAnalyzer.Models;

// build 命令选项，对应 --assemblies/--references/--defs-path/--output 等参数
public class BuildOptions
{
    public required string[] Assemblies { get; init; }
    public required string[] References { get; init; }
    public required string DefsPath { get; init; }
    public required string Output { get; init; }
    public bool Force { get; init; }
    public bool Verbose { get; init; }
}
