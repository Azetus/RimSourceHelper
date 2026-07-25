namespace RimAnalyzer.Models;

// add-mod 命令选项
public class AddModOptions
{
    public required string ModPath { get; init; }
    public required string Database { get; init; }
    public required string GamePath { get; init; }
    public bool Verbose { get; init; }
}
