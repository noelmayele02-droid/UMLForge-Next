using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UMLForge.Models;

namespace UMLForge.Services;

/// <summary>Imports declarations from C# source using Roslyn syntax trees (not regex parsing).</summary>
public static class CSharpImportService
{
    public static UmlModel ImportFiles(IEnumerable<string> paths, string projectName = "Projet importÃ©")
    {
        var model = new UmlModel { Name = projectName };
        foreach (var path in paths.Where(File.Exists).Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path));
            var root = tree.GetCompilationUnitRoot();
            var ns = root.Members.OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "Imported";
            foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                var item = new UmlClass
                {
                    Name = declaration.Identifier.ValueText,
                    Namespace = declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? ns,
                    Kind = declaration is InterfaceDeclarationSyntax ? "interface" : declaration is RecordDeclarationSyntax ? "record" : declaration is StructDeclarationSyntax ? "struct" : "class",
                    Accessibility = declaration.Modifiers.Any(SyntaxKind.PublicKeyword) ? "public" : "internal",
                    X = 40 + (model.Classes.Count % 4) * 250,
                    Y = 40 + (model.Classes.Count / 4) * 210
                };
                var baseType = declaration.BaseList?.Types.FirstOrDefault()?.Type.ToString();
                item.BaseType = string.IsNullOrWhiteSpace(baseType) ? null : baseType;
                foreach (var prop in declaration.Members.OfType<PropertyDeclarationSyntax>())
                    item.Attributes.Add($"{prop.Identifier.ValueText}: {prop.Type}");
                foreach (var field in declaration.Members.OfType<FieldDeclarationSyntax>())
                    foreach (var variable in field.Declaration.Variables)
                        item.Attributes.Add($"{variable.Identifier.ValueText}: {field.Declaration.Type}");
                foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
                {
                    var args = string.Join(", ", method.ParameterList.Parameters.Select(p => $"{p.Identifier.ValueText}: {p.Type?.ToString() ?? "object"}"));
                    item.Methods.Add($"{method.Identifier.ValueText}({args}): {method.ReturnType}");
                }
                if (model.Classes.All(c => c.Name != item.Name || c.Namespace != item.Namespace)) model.Classes.Add(item);
            }
        }
        return model;
    }
}
