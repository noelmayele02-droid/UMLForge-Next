using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UMLForge.Models;

namespace UMLForge.Services;

public static class CodeGenerator
{
    public static void Generate(UmlModel model, string folder)
    {
        Directory.CreateDirectory(folder);
        var projectName = SafeIdentifier(model.Name);
        var projectFile = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>{EscapeXml(model.TargetFramework)}</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>{EscapeXml(model.LanguageVersion)}</LangVersion>
  </PropertyGroup>
</Project>";
        File.WriteAllText(Path.Combine(folder, projectName + ".csproj"), projectFile, new UTF8Encoding(false));
        foreach (var cls in model.Classes)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// UMLForge: generated declarations; manual method bodies are preserved by SynchronizationService.");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine();
            sb.AppendLine($"namespace {SafeNamespace(cls.Namespace)};");
            sb.AppendLine();
            var kind = cls.Kind is "interface" or "record" or "struct" ? cls.Kind : "class";
            var inheritance = string.IsNullOrWhiteSpace(cls.BaseType) ? "" : $" : {SafeType(cls.BaseType)}";
            sb.AppendLine($"{SafeAccessibility(cls.Accessibility)} partial {kind} {SafeIdentifier(cls.Name)}{inheritance}");
            sb.AppendLine("{");
            foreach (var raw in cls.Attributes.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (TryParseMember(raw, out var name, out var type))
                    sb.AppendLine(kind == "interface" ? $"    {SafeType(type)} {SafeIdentifier(name)} {{ get; set; }}" : $"    public {SafeType(type)} {SafeIdentifier(name)} {{ get; set; }}");
                else sb.AppendLine($"    // TODO UMLForge: propriÃ©tÃ© non reconnue : {SafeComment(raw)}");
            }
            if (cls.Attributes.Count > 0 && cls.Methods.Count > 0) sb.AppendLine();
            foreach (var raw in cls.Methods.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!TryParseMethod(raw, out var methodName, out var returnType, out var parameters))
                { sb.AppendLine($"    // TODO UMLForge: mÃ©thode non reconnue : {SafeComment(raw)}"); continue; }
                sb.AppendLine(kind == "interface" ? $"    {SafeType(returnType)} {SafeIdentifier(methodName)}({parameters});" : $"    public {SafeType(returnType)} {SafeIdentifier(methodName)}({parameters})");
                if (kind != "interface")
                {
                    sb.AppendLine("    {");
                    if (returnType.Trim() == "void") sb.AppendLine("        // TODO: ImplÃ©menter la mÃ©thode.");
                    else sb.AppendLine("        throw new NotImplementedException();");
                    sb.AppendLine("    }");
                }
                sb.AppendLine();
            }
            sb.AppendLine("}");
            var namespacePath = SafeNamespace(cls.Namespace).Replace('.', Path.DirectorySeparatorChar);
            var classFolder = Path.Combine(folder, namespacePath);
            Directory.CreateDirectory(classFolder);
            var fileName = SafeIdentifier(cls.Name) + ".cs";
            var outputPath = Path.Combine(classFolder, fileName);
            if (!File.Exists(outputPath))
            {
                var rootCandidate = Path.Combine(folder, fileName);
                if (File.Exists(rootCandidate)) outputPath = rootCandidate;
                else
                {
                    var matches = Directory.EnumerateFiles(folder, fileName, SearchOption.AllDirectories).ToList();
                    if (matches.Count == 1) outputPath = matches[0];
                }
            }
            var generatedSource = sb.ToString();
            if (File.Exists(outputPath))
            {
                try { generatedSource = SynchronizationService.PreserveMethodBodies(File.ReadAllText(outputPath), generatedSource); }
                catch { /* If an existing file cannot be parsed, never destroy it. */ continue; }
            }
            File.WriteAllText(outputPath, generatedSource, new UTF8Encoding(false));
        }
    }

    public static bool TryParseMember(string input, out string name, out string type)
    {
        name = ""; type = "string";
        var parts = input.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || string.IsNullOrWhiteSpace(parts[0])) return false;
        name = parts[0]; if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1])) type = parts[1];
        return IsIdentifier(name) && IsSafeType(type);
    }

    public static bool TryParseMethod(string input, out string name, out string returnType, out string parameters)
    {
        name = ""; returnType = "void"; parameters = "";
        var match = Regex.Match(input.Trim(), @"^(?<name>@?[A-Za-z_][A-Za-z0-9_]*)\s*\((?<args>[^)]*)\)\s*(?::\s*(?<ret>[\w?.<>,\[\] ]+))?$");
        if (!match.Success) return false;
        name = match.Groups["name"].Value.TrimStart('@');
        returnType = match.Groups["ret"].Success ? match.Groups["ret"].Value.Trim() : "void";
        if (!IsSafeType(returnType)) return false;
        var args = new List<string>(); var rawArgs = match.Groups["args"].Value.Trim();
        if (rawArgs.Length > 0)
        {
            foreach (var arg in rawArgs.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = arg.Split(':', 2, StringSplitOptions.TrimEntries);
                if (pair.Length != 2 || !IsIdentifier(pair[0]) || !IsSafeType(pair[1])) return false;
                args.Add($"{SafeType(pair[1])} {SafeIdentifier(pair[0])}");
            }
        }
        parameters = string.Join(", ", args);
        return IsIdentifier(name);
    }

    public static string SafeType(string type) => IsSafeType(type) ? type.Trim() : "string";
    public static string SafeNamespace(string value) => string.Join(".", (value ?? "Generated").Split('.', StringSplitOptions.RemoveEmptyEntries).Select(SafeIdentifier).DefaultIfEmpty("Generated"));
    public static string SafeIdentifier(string value)
    {
        value = Regex.Replace(value ?? "", @"[^A-Za-z0-9_]", "");
        if (string.IsNullOrWhiteSpace(value)) value = "GeneratedItem";
        if (char.IsDigit(value[0])) value = "_" + value;
        return value;
    }
    private static bool IsIdentifier(string value) => Regex.IsMatch(value.TrimStart('@'), @"^[A-Za-z_][A-Za-z0-9_]*$");
    private static bool IsSafeType(string value) => Regex.IsMatch(value.Trim(), @"^(global::)?[A-Za-z_][A-Za-z0-9_.]*(\s*<\s*[\w\s.,?<>\[\]]+\s*>)?(\[\])?\??$");
    private static string SafeAccessibility(string value) => value is "public" or "internal" ? value : "public";
    private static string SafeComment(string value) => value.Replace("\r", " ").Replace("\n", " ").Replace("*/", "* /");
    private static string EscapeXml(string value) => System.Security.SecurityElement.Escape(value) ?? "";
}
