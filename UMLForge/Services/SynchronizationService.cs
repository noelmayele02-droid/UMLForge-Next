using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UMLForge.Services;

/// <summary>
/// Conservative Roslyn-based merge. Existing method bodies and initializers are kept when a generated
/// declaration with the same semantic key is found. Unmapped custom members are retained in the partial type.
/// </summary>
public static class SynchronizationService
{
    public static string PreserveMethodBodies(string existingSource, string generatedSource)
    {
        var oldRoot = CSharpSyntaxTree.ParseText(existingSource).GetCompilationUnitRoot();
        var newRoot = CSharpSyntaxTree.ParseText(generatedSource).GetCompilationUnitRoot();
        var oldType = oldRoot.DescendantNodes().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        var newType = newRoot.DescendantNodes().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (oldType is null || newType is null || oldType.Identifier.ValueText != newType.Identifier.ValueText)
            return generatedSource;

        var oldMethods = oldType.Members.OfType<MethodDeclarationSyntax>()
            .Where(m => m.Body is not null || m.ExpressionBody is not null)
            .GroupBy(MethodKey).ToDictionary(g => g.Key, g => g.First());
        var oldProperties = oldType.Members.OfType<PropertyDeclarationSyntax>()
            .GroupBy(p => p.Identifier.ValueText).ToDictionary(g => g.Key, g => g.First());
        var generatedMembers = new List<MemberDeclarationSyntax>();
        foreach (var member in newType.Members)
        {
            if (member is MethodDeclarationSyntax generatedMethod && oldMethods.TryGetValue(MethodKey(generatedMethod), out var oldMethod))
            {
                var bodyText = generatedMethod.Body?.ToString() ?? "";
                if ((bodyText.Contains("NotImplementedException") || bodyText.Contains("TODO: ImplÃ©menter")) && oldMethod.Body is not null)
                    generatedMethod = generatedMethod.WithBody(oldMethod.Body).WithExpressionBody(null).WithSemicolonToken(default);
                else if (oldMethod.ExpressionBody is not null && oldMethod.Body is null)
                    generatedMethod = generatedMethod.WithExpressionBody(oldMethod.ExpressionBody).WithBody(null).WithSemicolonToken(default);
                generatedMembers.Add(generatedMethod);
            }
            else if (member is PropertyDeclarationSyntax generatedProperty && oldProperties.TryGetValue(generatedProperty.Identifier.ValueText, out var oldProperty))
            {
                if (oldProperty.Initializer is not null)
                    generatedProperty = generatedProperty.WithInitializer(oldProperty.Initializer).WithSemicolonToken(oldProperty.SemicolonToken);
                generatedMembers.Add(generatedProperty);
            }
            else generatedMembers.Add(member);
        }

        // Preserve custom constructors, events, fields, nested types and helper members not represented in UML.
        var representedMethods = generatedMembers.OfType<MethodDeclarationSyntax>().Select(MethodKey).ToHashSet();
        var representedProperties = generatedMembers.OfType<PropertyDeclarationSyntax>().Select(p => p.Identifier.ValueText).ToHashSet();
        foreach (var oldMember in oldType.Members)
        {
            if (oldMember is MethodDeclarationSyntax method && representedMethods.Contains(MethodKey(method))) continue;
            if (oldMember is PropertyDeclarationSyntax property && representedProperties.Contains(property.Identifier.ValueText)) continue;
            if (oldMember is ConstructorDeclarationSyntax ctor && generatedMembers.OfType<ConstructorDeclarationSyntax>().Any(c => c.ParameterList.Parameters.Count == ctor.ParameterList.Parameters.Count)) continue;
            generatedMembers.Add(oldMember);
        }
        var mergedType = newType.WithMembers(SyntaxFactory.List(generatedMembers));
        var mergedRoot = newRoot.ReplaceNode(newType, mergedType);
        return mergedRoot.NormalizeWhitespace().ToFullString() + Environment.NewLine;
    }

    private static string MethodKey(MethodDeclarationSyntax m) => m.Identifier.ValueText + "(" + string.Join(",", m.ParameterList.Parameters.Select(p => p.Type?.ToString() ?? "?")) + ")";
}
