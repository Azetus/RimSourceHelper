using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

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
        string typeFullName, string methodName, string[] parameterTypes)
    {
        var decompiler = CreateDecompiler(assemblyPath, referencePaths);
        var normalizedName = typeFullName.Replace('/', '+');
        var fullTypeName = new FullTypeName(normalizedName);

        var type = decompiler.TypeSystem.FindType(fullTypeName).GetDefinition();
        if (type is null)
            throw new InvalidOperationException($"Type not found in assembly: {typeFullName}");

        // 按参数数量粗匹配（精确类型匹配复杂度高，暂用参数数量区分）
        var methods = type.Methods
            .Where(m => m.Name == methodName && m.Parameters.Count == parameterTypes.Length)
            .ToList();

        if (methods.Count == 0)
            throw new InvalidOperationException($"Method '{methodName}' with {parameterTypes.Length} parameters not found");

        var sources = methods
            .Select(m => decompiler.DecompileAsString(m.MetadataToken))
            .ToList();

        return string.Join("\n", sources);
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
