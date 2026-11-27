using System.Reflection.Metadata.Ecma335;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using Mono.Cecil;
using RimAnalyzer.Analysis.Metadata;

namespace RimAnalyzer.Analysis.Decompiler;

// 封装 ICSharpCode.Decompiler，提供类型和方法的按需反编译
public static class DecompilerService
{
    // 反编译整个类型
    public static string DecompileType(string assemblyPath, string[] referencePaths, string typeFullName)
    {
        var decompiler = CreateDecompiler(assemblyPath, referencePaths);
        // Cecil 使用 '/' 分隔嵌套类型，ICSharpCode.Decompiler 使用 '+'
        var normalizedName = typeFullName.Replace('/', '+');
        var fullTypeName = new FullTypeName(normalizedName);
        return decompiler.DecompileTypeAsString(fullTypeName);
    }

    // 反编译指定方法（所有重载）
    public static string DecompileMethod(string assemblyPath, string[] referencePaths,
        string typeFullName, string methodName)
    {
        var decompiler = CreateDecompiler(assemblyPath, referencePaths);
        var normalizedName = typeFullName.Replace('/', '+');
        var fullTypeName = new FullTypeName(normalizedName);

        var type = decompiler.TypeSystem.FindType(fullTypeName).GetDefinition();
        if (type is null)
            throw new InvalidOperationException($"Type not found in assembly: {typeFullName}");

        var methods = type.Methods.Where(m => m.Name == methodName).ToList();
        if (methods.Count == 0)
            throw new InvalidOperationException($"Method '{methodName}' not found in type '{typeFullName}'");

        // 反编译所有重载，合并输出
        var sources = methods
            .Select(m => decompiler.DecompileAsString(m.MetadataToken))
            .ToList();

        return string.Join("\n", sources);
    }

    // 通过完整签名反编译单个方法
    public static string DecompileMethodBySignature(string assemblyPath, string[] referencePaths,
        string typeFullName, string methodName, string signature)
    {
        using var module = ModuleDefinition.ReadModule(assemblyPath);
        var type = module.GetType(typeFullName)
            ?? throw new InvalidOperationException($"Type not found in assembly: {typeFullName}");

        // 复用建库时的签名格式，再按匹配方法的元数据令牌反编译。
        var methods = type.Methods
            .Where(method => method.Name == methodName && MetadataCollector.BuildSignature(method) == signature)
            .Take(2)
            .ToArray();

        if (methods.Length == 0)
            throw new InvalidOperationException($"Method signature not found in assembly: {signature}");
        if (methods.Length > 1)
            throw new InvalidOperationException($"Method signature matches multiple overloads in assembly: {signature}");

        var decompiler = CreateDecompiler(assemblyPath, referencePaths);
        return decompiler.DecompileAsString(MetadataTokens.EntityHandle(methods[0].MetadataToken.ToInt32()));
    }

    private static CSharpDecompiler CreateDecompiler(string assemblyPath, string[] referencePaths)
    {
        var settings = new DecompilerSettings
        {
            ThrowOnAssemblyResolveErrors = false
        };

        var resolver = new UniversalAssemblyResolver(assemblyPath, false, null);
        foreach (var refPath in referencePaths)
        {
            if (Directory.Exists(refPath))
                resolver.AddSearchDirectory(refPath);
        }

        return new CSharpDecompiler(assemblyPath, resolver, settings);
    }
}
