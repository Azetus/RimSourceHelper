using System.CommandLine;
using RimAnalyzer.Commands;

namespace RimAnalyzer;

class Program
{
    static int Main(string[] args)
    {
        var rootCommand = new RootCommand("RimWorld DLL analysis tool");
        rootCommand.Add(BuildCommand.Create());
        return rootCommand.Parse(args).Invoke();
    }
}
