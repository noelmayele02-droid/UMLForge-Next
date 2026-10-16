using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using UMLForge.Models;

namespace UMLForge.Services;

/// <summary>
/// Maps persistent UML IDs to Roslyn symbols. Exact names are preferred; a conservative
/// structural fallback can identify renames. Ambiguous structural matches are never guessed.
/// </summary>
public static class UmlCodeMappingService
{
    public static async Task<UmlMappingReport> RefreshClassMappingsAsync(UmlModel model, Solution solution, CancellationToken cancellationToken = default)
        => await RefreshMappingsAsync(model, solution, cancellationToken);

    public static async Task<UmlMappingReport> RefreshMappingsAsync(UmlModel model, Solution solution, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(solution);

        var seenMappingIds = new HashSet<Guid>();
        for (var i = model.CodeMappings.Count - 1; i >= 0; i--)
            if (!seenMappingIds.Add(model.CodeMappings[i].UmlElementId)) model.CodeMappings.RemoveAt(i);
        var oldMappings = model.CodeMappings.ToDictionary(m => m.UmlElementId, m => new MappingSnapshot(m.FullyQualifiedName, m.ElementKind, m.SymbolFingerprint, m.FilePath));
        var symbols = new List<INamedTypeSymbol>();
        foreach (var project in solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null) continue;
            CollectTypes(compilation.Assembly.GlobalNamespace, symbols);
        }

        var requests = new List<ElementRequest>();
        foreach (var item in model.Classes)
        {
            var ns = item.Namespace?.Trim('.');
            var fqn = string.IsNullOrWhiteSpace(ns) ? item.Name : ns + "." + item.Name;
            requests.Add(new(item.Id, "Class", item.Name, fqn, null, item.IsPartial));
            foreach (var property in item.Properties)
                requests.Add(new(property.Id, "Property", property.Name, fqn, property, false));
            foreach (var operation in item.Operations)
                requests.Add(new(operation.Id, "Operation", operation.Name, fqn, operation, false));
        }
        foreach (var item in model.Interfaces)
        {
            var interfaceName = QualifiedName(item.Namespace, item.Name);
            requests.Add(new(item.Id, "Interface", item.Name, interfaceName, null, false));
            foreach (var property in item.Properties) requests.Add(new(property.Id, "Property", property.Name, interfaceName, property, false));
            foreach (var operation in item.Operations) requests.Add(new(operation.Id, "Operation", operation.Name, interfaceName, operation, false));
        }
        foreach (var item in model.Enumerations)
        {
            var enumerationName = QualifiedName(item.Namespace, item.Name);
            requests.Add(new(item.Id, "Enumeration", item.Name, enumerationName, null, false));
            foreach (var literal in item.Literals) requests.Add(new(literal.Id, "EnumerationLiteral", literal.Name, enumerationName, literal, false));
        }
        foreach (var item in model.Signals)
        {
            var signalName = QualifiedName(item.Namespace, item.Name);
            requests.Add(new(item.Id, "Signal", item.Name, signalName, null, false));
            foreach (var property in item.Attributes) requests.Add(new(property.Id, "Property", property.Name, signalName, property, false));
        }
        foreach (var item in model.PrimitiveTypes) requests.Add(new(item.Id, "PrimitiveType", item.Name, item.CSharpTypeName ?? QualifiedName(item.Namespace, item.Name), null, false));

        var added = 0; var updated = 0; var unresolved = 0; var ambiguous = 0; var renamed = 0;
        var now = DateTimeOffset.UtcNow;
        foreach (var request in requests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mapping = model.CodeMappings.FirstOrDefault(m => m.UmlElementId == request.Id);
            if (mapping is null)
            {
                mapping = new UmlCodeMapping { UmlElementId = request.Id };
                model.CodeMappings.Add(mapping); added++;
            }
            else updated++;

            var previous = oldMappings.GetValueOrDefault(request.Id);
            var candidates = FindCandidates(request, symbols).ToList();
            var wasRenamed = false;
            if (candidates.Count == 0 && previous is not null && !string.IsNullOrWhiteSpace(previous.Fingerprint))
            {
                var structural = FindStructuralCandidates(request, previous, symbols).ToList();
                if (structural.Count == 1) { candidates = structural; wasRenamed = true; }
                else if (structural.Count > 1)
                {
                    mapping.Status = "Ambiguous"; mapping.UmlElementName = request.Name; mapping.ElementKind = request.Kind;
                    mapping.LastMappedUtc = now; unresolved++; ambiguous++;
                    continue;
                }
            }

            mapping.UmlElementName = request.Name;
            mapping.ElementKind = request.Kind;
            mapping.FullyQualifiedName = request.FullyQualifiedName;
            mapping.LastMappedUtc = now;
            if (candidates.Count == 0)
            {
                mapping.Status = "Unresolved"; mapping.ProjectName = ""; mapping.FilePath = ""; mapping.SymbolFingerprint = ""; unresolved++;
                continue;
            }
            if (candidates.Count > 1)
            {
                mapping.Status = "Ambiguous"; mapping.ProjectName = ""; mapping.FilePath = ""; mapping.SymbolFingerprint = ""; unresolved++; ambiguous++;
                continue;
            }

            var symbol = candidates[0];
            mapping.FullyQualifiedName = symbol is INamedTypeSymbol mappedType
                ? mappedType.ToDisplayString()
                : symbol.ContainingType?.ToDisplayString() ?? request.FullyQualifiedName;
            mapping.ProjectName = solution.Projects.FirstOrDefault(p => string.Equals(p.AssemblyName, symbol.ContainingAssembly?.Name, StringComparison.Ordinal))?.Name ?? symbol.ContainingAssembly?.Name ?? "";
            mapping.FilePath = symbol.Locations.FirstOrDefault(l => l.IsInSource)?.SourceTree?.FilePath ?? "";
            mapping.SymbolFingerprint = Fingerprint(symbol, request.Kind);
            var locations = symbol.Locations.Count(l => l.IsInSource);
            mapping.Status = wasRenamed ? "MappedAfterRename" : request.IsPartial || locations > 1 ? "MappedPartial" : "Mapped";
            if (wasRenamed) renamed++;
        }

        // Remove stale entries for UML elements deleted from the supported mapped collections.
        var liveIds = requests.Select(r => r.Id).ToHashSet();
        for (var i = model.CodeMappings.Count - 1; i >= 0; i--)
            if (!liveIds.Contains(model.CodeMappings[i].UmlElementId)) model.CodeMappings.RemoveAt(i);

        return new UmlMappingReport(added, updated, unresolved, model.CodeMappings.Count(m => m.Status.StartsWith("Mapped", StringComparison.Ordinal)), ambiguous, renamed,
            requests.Count(r => r.Kind == "Property"), requests.Count(r => r.Kind == "Operation"), requests.Count(r => r.Kind == "Interface"), requests.Count(r => r.Kind is "Enumeration" or "EnumerationLiteral"));
    }

    private static IEnumerable<ISymbol> FindCandidates(ElementRequest request, IReadOnlyList<INamedTypeSymbol> types)
    {
        var parent = types.Where(t => t.ToDisplayString() == request.FullyQualifiedName || t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "") == request.FullyQualifiedName).ToList();
        if (request.Kind is "Class" or "Interface" or "Enumeration" or "Signal" or "PrimitiveType")
        {
            var expected = request.Kind switch { "Class" => TypeKind.Class, "Interface" => TypeKind.Interface, "Enumeration" => TypeKind.Enum, _ => (TypeKind?)null };
            return types.Where(t => (t.ToDisplayString() == request.FullyQualifiedName || t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "") == request.FullyQualifiedName)
                && (expected is null || t.TypeKind == expected));
        }
        if (parent.Count != 1) return Array.Empty<ISymbol>();
        var owner = parent[0];
        if (request.Kind == "Property") return owner.GetMembers(request.Name).Where(m => m is IPropertySymbol or IFieldSymbol);
        if (request.Kind == "Operation")
        {
            var methods = owner.GetMembers(request.Name).OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Ordinary);
            if (request.Detail is UmlOperation op)
                methods = methods.Where(m => m.Parameters.Length == op.Parameters.Count
                    && (string.IsNullOrWhiteSpace(op.ReturnType) || NormalizeType(m.ReturnType) == NormalizeType(op.ReturnType))
                    && m.Parameters.Select(p => NormalizeType(p.Type)).SequenceEqual(op.Parameters.Select(p => NormalizeType(p.Type)), StringComparer.Ordinal));
            return methods;
        }
        if (request.Kind == "EnumerationLiteral") return owner.GetMembers(request.Name).Where(m => m is IFieldSymbol f && f.HasConstantValue);
        return Array.Empty<ISymbol>();
    }

    private static IEnumerable<ISymbol> FindStructuralCandidates(ElementRequest request, MappingSnapshot previous, IReadOnlyList<INamedTypeSymbol> types)
    {
        if (request.Kind is "Class" or "Interface" or "Enumeration")
        {
            var typeKind = request.Kind switch { "Interface" => TypeKind.Interface, "Enumeration" => TypeKind.Enum, _ => TypeKind.Class };
            return types.Where(t => t.TypeKind == typeKind && Fingerprint(t, request.Kind) == previous.Fingerprint);
        }
        // Member rename fallback is scoped to the previously mapped containing file and containing type.
        var oldOwner = previous.FullyQualifiedName;
        var ownerName = oldOwner;
        var owners = types.Where(t => t.ToDisplayString().Equals(ownerName, StringComparison.Ordinal) || t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "").Equals(ownerName, StringComparison.Ordinal) || (!string.IsNullOrWhiteSpace(previous.FilePath) && t.Locations.Any(l => l.IsInSource && string.Equals(l.SourceTree?.FilePath, previous.FilePath, StringComparison.OrdinalIgnoreCase))));
        return owners.SelectMany(t => t.GetMembers()).Where(m => MemberKindMatches(request.Kind, m) && Fingerprint(m, request.Kind) == previous.Fingerprint);
    }

    private static bool MemberKindMatches(string kind, ISymbol symbol) => kind switch
    {
        "Property" => symbol is IPropertySymbol or IFieldSymbol,
        "Operation" => symbol is IMethodSymbol { MethodKind: MethodKind.Ordinary },
        "EnumerationLiteral" => symbol is IFieldSymbol { HasConstantValue: true },
        _ => false
    };

    private static string Fingerprint(ISymbol symbol, string kind)
    {
        string shape = symbol switch
        {
            INamedTypeSymbol t => $"{kind}|{t.TypeKind}|{t.Arity}|" + string.Join(";", t.GetMembers().Where(m => !m.IsImplicitlyDeclared).Select(m => MemberShape(m)).OrderBy(x => x, StringComparer.Ordinal)),
            IPropertySymbol p => $"Property|{NormalizeType(p.Type)}|{p.IsStatic}|{p.GetMethod is not null}|{p.SetMethod is not null}",
            IFieldSymbol f => $"Field|{NormalizeType(f.Type)}|{f.IsStatic}|{f.IsReadOnly}|{f.HasConstantValue}",
            IMethodSymbol m => $"Operation|{NormalizeType(m.ReturnType)}|{m.IsStatic}|{m.Parameters.Length}|" + string.Join(",", m.Parameters.Select(p => NormalizeType(p.Type))),
            _ => kind + "|" + symbol.Kind
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(shape)));
    }

    private static string MemberShape(ISymbol member) => member switch
    {
        IPropertySymbol p => $"P:{NormalizeType(p.Type)}:{p.IsStatic}:{p.GetMethod is not null}:{p.SetMethod is not null}",
        IFieldSymbol f when !f.IsImplicitlyDeclared => $"F:{NormalizeType(f.Type)}:{f.IsStatic}:{f.IsReadOnly}",
        IMethodSymbol m when !m.IsImplicitlyDeclared && m.MethodKind == MethodKind.Ordinary => $"M:{NormalizeType(m.ReturnType)}:{m.IsStatic}({string.Join(",", m.Parameters.Select(p => NormalizeType(p.Type)))})",
        _ => ""
    };

    private static string QualifiedName(string? ns, string name) => string.IsNullOrWhiteSpace(ns) ? name : ns.Trim('.') + "." + name;
    private static string NormalizeType(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat).Replace("?", "");
    private static string NormalizeType(string type) => (type ?? "").Replace("global::", "").Replace("System.", "").Replace("?", "").Trim();
    private static void CollectTypes(INamespaceSymbol ns, List<INamedTypeSymbol> result)
    {
        foreach (var type in ns.GetTypeMembers()) { result.Add(type); CollectNested(type, result); }
        foreach (var child in ns.GetNamespaceMembers()) CollectTypes(child, result);
    }
    private static void CollectNested(INamedTypeSymbol type, List<INamedTypeSymbol> result)
    { foreach (var nested in type.GetTypeMembers()) { result.Add(nested); CollectNested(nested, result); } }

    private sealed record ElementRequest(Guid Id, string Kind, string Name, string FullyQualifiedName, object? Detail, bool IsPartial);
    private sealed record MappingSnapshot(string FullyQualifiedName, string Kind, string Fingerprint, string FilePath);
}

public sealed record UmlMappingReport(int Added, int Updated, int Unresolved, int MappedTotal, int Ambiguous = 0, int Renamed = 0,
    int PropertiesConsidered = 0, int OperationsConsidered = 0, int InterfacesConsidered = 0, int EnumerationsAndLiteralsConsidered = 0);
