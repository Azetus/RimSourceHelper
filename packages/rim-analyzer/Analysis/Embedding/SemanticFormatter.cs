using System.Text.Json;
using System.Text.RegularExpressions;
using RimAnalyzer.Models;

namespace RimAnalyzer.Analysis.Embedding;

// 从 SQLite 元数据构建语义文本（纯文本，小写，空格分隔）。纯函数，不访问 DB。
public static class SemanticFormatter
{
    private const int MaxDescriptionLength = 256;

    // Type 模板
    // type Verse.Pawn abstract base Verse.ThingWithComps implements Verse.IStrippable RimWorld.IBillGiver
    // pawn thing with comps strippable bill giver
    public static string FormatType(TypeEntity type, string[] interfaces)
    {
        var line1 = new List<string> { "type", type.FullName };

        if (type.IsInterface) line1.Add("interface");
        else if (type.IsAbstract) line1.Add("abstract");
        if (type.IsSealed && !type.IsInterface) line1.Add("sealed");
        if (type.IsEnum) line1.Add("enum");

        if (!string.IsNullOrEmpty(type.BaseType) && type.BaseType != "System.Object")
            line1.Add($"base {type.BaseType}");

        if (interfaces.Length > 0)
            line1.Add($"implements {string.Join(" ", interfaces)}");

        var tokens = new List<string> { type.Name };
        if (!string.IsNullOrEmpty(type.BaseType) && type.BaseType != "System.Object")
            tokens.Add(ShortName(type.BaseType));
        tokens.AddRange(interfaces.Select(ShortName));

        return $"{string.Join(" ", line1)}\n{Tokenize(tokens)}";
    }

    // Method 模板
    // method Verse.CompShield.PreApplyDamage returns boolean params damageInfo
    // comp shield pre apply damage damage info
    // calls Verse.CompShield.ConsumeEnergy
    // consume energy reduce shield
    public static string FormatMethod(string parentFullName, string methodName,
        string? returnType, string? paramTypesJson, string[] calleeFullNames)
    {
        var paramTypeShortNames = ParseParamShortNames(paramTypesJson);

        var line1 = new List<string> { "method", $"{parentFullName}.{methodName}" };

        var ret = ShortName(returnType ?? "void");
        line1.Add($"returns {ret}");

        if (paramTypeShortNames.Length > 0)
            line1.Add($"params {string.Join(" ", paramTypeShortNames)}");

        var tokens = new List<string> { methodName, ShortName(parentFullName) };
        tokens.AddRange(paramTypeShortNames);

        var result = new List<string> { string.Join(" ", line1), Tokenize(tokens) };

        if (calleeFullNames.Length > 0)
        {
            result.Add($"calls {string.Join(" ", calleeFullNames)}");
            result.Add(Tokenize(calleeFullNames.Select(ShortName)));
        }

        return string.Join("\n", result);
    }

    // Field 模板
    // field Verse.PawnKindDef.isFighter type boolean
    // is fighter
    public static string FormatField(string parentFullName, string fieldName, string? fieldType)
    {
        var line1 = $"field {parentFullName}.{fieldName} type {ShortName(fieldType ?? "object")}";
        return $"{line1}\n{Tokenize(fieldName)}";
    }

    // Property 模板
    // property Verse.Thing.Label type string get set
    // label
    public static string FormatProperty(string parentFullName, string propName,
        string? propType, bool hasGetter, bool hasSetter)
    {
        var parts = new List<string> { "property", $"{parentFullName}.{propName}", "type", ShortName(propType ?? "object") };
        if (hasGetter) parts.Add("get");
        if (hasSetter) parts.Add("set");
        return $"{string.Join(" ", parts)}\n{Tokenize(propName)}";
    }

    // Def 模板
    // def ThingDef MeleeWeapon_Knife abstract label knife
    // one of mankind's oldest manufactured objects a short bladed weapon used since ancient times
    public static string FormatDef(DefEntity def)
    {
        var line1 = new List<string> { "def", def.DefType!, def.DefName! };

        if (def.IsAbstract)
            line1.Add("abstract");

        if (!string.IsNullOrEmpty(def.Label))
            line1.Add($"label {def.Label}");

        var desc = def.Description ?? "";
        if (desc.Length > MaxDescriptionLength)
            desc = desc[..MaxDescriptionLength];

        return string.IsNullOrWhiteSpace(desc)
            ? string.Join(" ", line1)
            : $"{string.Join(" ", line1)}\n{desc}";
    }

    // 从 FullName 取最后一段并做 CamelCase 分词
    // "System.Boolean" → "boolean",  "Verse.DamageInfo" → "damage info"
    public static string ShortName(string fullName)
    {
        if (string.IsNullOrEmpty(fullName))
            return "void";

        var dot = fullName.LastIndexOf('.');
        var name = dot >= 0 ? fullName[(dot + 1)..] : fullName;
        return Tokenize(name);
    }

    // 解析 JSON 参数类型数组 → ShortName 列表
    private static string[] ParseParamShortNames(string? paramTypesJson)
    {
        if (string.IsNullOrEmpty(paramTypesJson)) return [];

        var fullNames = JsonSerializer.Deserialize<string[]>(paramTypesJson);
        if (fullNames is null) return [];

        return fullNames.Select(ShortName).ToArray();
    }

    // CamelCase 分词 + 全小写：PreApplyDamage → pre apply damage
    public static string Tokenize(string nameOrNames)
    {
        return Tokenize(new[] { nameOrNames });
    }

    private static string Tokenize(IEnumerable<string> names)
    {
        var parts = new List<string>();
        foreach (var name in names)
        {
            if (string.IsNullOrEmpty(name)) continue;
            var s = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
            s = Regex.Replace(s, "([A-Z]+)([A-Z][a-z])", "$1 $2");
            s = s.Replace('_', ' ');
            foreach (var w in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                parts.Add(w.ToLowerInvariant());
        }
        return string.Join(" ", parts);
    }
}
