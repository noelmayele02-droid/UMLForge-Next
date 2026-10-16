using System.IO;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.MSBuild;

namespace UMLForge.Services;

/// <summary>Roslyn solution services: semantic diagnostics, symbol-aware completion, rename and formatting.</summary>
public sealed class SolutionSemanticService : IAsyncDisposable
{
    private MSBuildWorkspace? _workspace;
    public Solution? Solution { get; private set; }
    public string? SolutionPath { get; private set; }
    public bool IsLoaded => _workspace is not null && Solution is not null;

    public async Task OpenSolutionAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();
        if (_workspace is not null) _workspace.Dispose();
        _workspace = MSBuildWorkspace.Create();
        _workspace.WorkspaceFailed += (_, e) => { /* surfaced by callers through diagnostics where possible */ };
        var full = Path.GetFullPath(path);
        if (Directory.Exists(full))
        {
            var sln = Directory.GetFiles(full, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            var project = Directory.GetFiles(full, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
            full = sln ?? project ?? throw new FileNotFoundException("Aucun fichier .sln ou .csproj trouvÃ© dans le dossier.");
        }
        Solution = full.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            ? await _workspace.OpenSolutionAsync(full, cancellationToken: cancellationToken)
            : (await _workspace.OpenProjectAsync(full, cancellationToken: cancellationToken)).Solution;
        SolutionPath = full;
    }

    public async Task<IReadOnlyList<SemanticDiagnostic>> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        if (Solution is null) throw new InvalidOperationException("Aucune solution chargÃ©e.");
        var output = new List<SemanticDiagnostic>();
        foreach (var project in Solution.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compilation = await project.GetCompilationAsync(cancellationToken);
            if (compilation is null) continue;
            foreach (var diagnostic in compilation.GetDiagnostics(cancellationToken)
                         .Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning))
            {
                var span = diagnostic.Location.GetLineSpan();
                output.Add(new SemanticDiagnostic(project.Name, diagnostic.Id, diagnostic.Severity.ToString(),
                    diagnostic.GetMessage(), diagnostic.Location.SourceTree?.FilePath ?? "",
                    diagnostic.Location.IsInSource ? span.StartLinePosition.Line + 1 : 0,
                    diagnostic.Location.IsInSource ? span.StartLinePosition.Character + 1 : 0));
            }
        }
        return output;
    }

    public async Task<IReadOnlyList<string>> GetCompletionsAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        var document = FindDocument(filePath);
        var service = CompletionService.GetService(document);
        if (service is null) return Array.Empty<string>();
        var text = await document.GetTextAsync(cancellationToken);
        var safePosition = Math.Clamp(position, 0, text.Length);
        var list = await service.GetCompletionsAsync(document, safePosition, cancellationToken: cancellationToken);
        return list?.ItemsList.Select(item => item.DisplayText).Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(100).ToArray() ?? Array.Empty<string>();
    }

    public async Task<SymbolNavigationResult?> GetDefinitionAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        var document = FindDocument(filePath);
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var model = await document.GetSemanticModelAsync(cancellationToken);
        if (root is null || model is null) return null;
        var token = root.FindToken(Math.Clamp(position, 0, root.FullSpan.End));
        ISymbol? symbol = null;
        foreach (var node in token.Parent?.AncestorsAndSelf() ?? Enumerable.Empty<Microsoft.CodeAnalysis.SyntaxNode>())
        {
            symbol = model.GetDeclaredSymbol(node, cancellationToken) ?? model.GetSymbolInfo(node, cancellationToken).Symbol;
            if (symbol is not null) break;
        }
        if (symbol is null) return null;
        var locations = symbol.Locations.Where(l => l.IsInSource).Select(l =>
        {
            var span = l.GetLineSpan();
            return new SymbolLocation(l.SourceTree?.FilePath ?? "", span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1);
        }).ToArray();
        return new SymbolNavigationResult(symbol.ToDisplayString(), locations);
    }

    public async Task<IReadOnlyList<SymbolReference>> FindReferencesAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        if (Solution is null) throw new InvalidOperationException("Aucune solution chargÃ©e.");
        var document = FindDocument(filePath);
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var model = await document.GetSemanticModelAsync(cancellationToken);
        if (root is null || model is null) return Array.Empty<SymbolReference>();
        var token = root.FindToken(Math.Clamp(position, 0, root.FullSpan.End));
        ISymbol? symbol = null;
        foreach (var node in token.Parent?.AncestorsAndSelf() ?? Enumerable.Empty<Microsoft.CodeAnalysis.SyntaxNode>())
        {
            symbol = model.GetDeclaredSymbol(node, cancellationToken) ?? model.GetSymbolInfo(node, cancellationToken).Symbol;
            if (symbol is not null) break;
        }
        if (symbol is null) return Array.Empty<SymbolReference>();
        var results = await SymbolFinder.FindReferencesAsync(symbol, Solution, cancellationToken: cancellationToken);
        return results.SelectMany(r => r.Locations).Where(l => l.Location.IsInSource).Select(l =>
        {
            var span = l.Location.GetLineSpan();
            return new SymbolReference(l.Document.FilePath ?? "", span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1, false);
        }).OrderBy(r => r.File, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Line).ToArray();
    }

    public async Task<string> SortUsingsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var document = FindDocument(filePath);
        var root = await document.GetSyntaxRootAsync(cancellationToken) as Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax;
        if (root is null) throw new InvalidOperationException("Fichier C# non analysable.");
        var sorted = root.Usings.OrderBy(u => u.Alias is not null ? 0 : 1)
            .ThenBy(u => u.Name?.ToString().StartsWith("System", StringComparison.Ordinal) == true ? 0 : 1)
            .ThenBy(u => u.Name?.ToString() ?? u.ToString(), StringComparer.Ordinal)
            .ToArray();
        return root.WithUsings(Microsoft.CodeAnalysis.CSharp.SyntaxFactory.List(sorted)).ToFullString();
    }

    public async Task<RenameResult> RenameSymbolAsync(string filePath, int position, string newName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newName) || !SyntaxFacts.IsValidIdentifier(newName)) throw new ArgumentException("Le nouveau nom doit Ãªtre un identifiant C# valide.", nameof(newName));
        var document = FindDocument(filePath);
        var root = await document.GetSyntaxRootAsync(cancellationToken) ?? throw new InvalidOperationException("Syntaxe indisponible.");
        var token = root.FindToken(Math.Clamp(position, 0, root.FullSpan.End));
        var model = await document.GetSemanticModelAsync(cancellationToken) ?? throw new InvalidOperationException("ModÃ¨le sÃ©mantique indisponible.");
        ISymbol? symbol = null;
        foreach (var node in token.Parent?.AncestorsAndSelf() ?? Enumerable.Empty<Microsoft.CodeAnalysis.SyntaxNode>())
        {
            symbol = model.GetDeclaredSymbol(node, cancellationToken) ?? model.GetSymbolInfo(node, cancellationToken).Symbol;
            if (symbol is not null) break;
        }
        if (symbol is null) throw new InvalidOperationException("Aucun symbole C# rÃ©solu Ã  la position du curseur. Placez le curseur sur une classe, mÃ©thode, propriÃ©tÃ©, variable ou paramÃ¨tre reconnu.");
        if (_workspace is null || Solution is null) throw new InvalidOperationException("Aucune solution chargÃ©e.");
        var changed = await Renamer.RenameSymbolAsync(Solution, symbol, newName, _workspace.Options, cancellationToken);
        var changedFiles = new List<string>();
        var changedContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var project in changed.Projects)
        foreach (var doc in project.Documents)
        {
            var oldDoc = Solution.GetDocument(doc.Id);
            if (oldDoc is null || doc.FilePath is null) continue;
            var oldText = await oldDoc.GetTextAsync(cancellationToken);
            var newText = await doc.GetTextAsync(cancellationToken);
            if (!oldText.ContentEquals(newText))
            {
                changedFiles.Add(doc.FilePath);
                changedContents[doc.FilePath] = newText.ToString();
            }
        }
        Solution = changed;
        return new RenameResult(symbol.ToDisplayString(), newName, changedFiles.Distinct().ToArray(), changedContents);
    }

    public Task UpdateDocumentTextAsync(string filePath, string source, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var document = FindDocument(filePath);
        if (_workspace is null || Solution is null) throw new InvalidOperationException("Aucune solution chargÃ©e.");
        var updated = Solution.WithDocumentText(document.Id, SourceText.From(source));
        if (!_workspace.TryApplyChanges(updated)) throw new IOException("Roslyn n'a pas pu appliquer les changements temporaires au workspace.");
        Solution = _workspace.CurrentSolution;
        return Task.CompletedTask;
    }

    public async Task<string> FormatDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var document = FindDocument(filePath);
        var formatted = await Formatter.FormatAsync(document, cancellationToken: cancellationToken);
        return (await formatted.GetTextAsync(cancellationToken)).ToString();
    }


    public async Task<IReadOnlyList<SignatureHelpItem>> GetSignatureHelpAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        var document = FindDocument(filePath);
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var model = await document.GetSemanticModelAsync(cancellationToken);
        if (root is null || model is null) return Array.Empty<SignatureHelpItem>();
        var safePosition = Math.Clamp(position, 0, root.FullSpan.End);
        var token = root.FindToken(safePosition);
        var invocation = token.Parent?.AncestorsAndSelf().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
            .FirstOrDefault(n => n.Span.Contains(safePosition) || n.ArgumentList.CloseParenToken.Span.Contains(safePosition));
        var creation = token.Parent?.AncestorsAndSelf().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ObjectCreationExpressionSyntax>()
            .FirstOrDefault(n => n.Span.Contains(safePosition));
        var symbols = new List<IMethodSymbol>();
        if (invocation is not null)
        {
            var info = model.GetSymbolInfo(invocation, cancellationToken);
            if (info.Symbol is IMethodSymbol method) symbols.Add(method);
            symbols.AddRange(info.CandidateSymbols.OfType<IMethodSymbol>());
            symbols.AddRange(model.GetSymbolInfo(invocation.Expression, cancellationToken).CandidateSymbols.OfType<IMethodSymbol>());
            if (model.GetSymbolInfo(invocation.Expression, cancellationToken).Symbol is IMethodSymbol selected) symbols.Add(selected);
        }
        else if (creation is not null)
        {
            var info = model.GetSymbolInfo(creation, cancellationToken);
            if (info.Symbol is IMethodSymbol method) symbols.Add(method);
            symbols.AddRange(info.CandidateSymbols.OfType<IMethodSymbol>());
        }
        return symbols.GroupBy(m => m.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), StringComparer.Ordinal).Select(g => g.First()).Select(m => new SignatureHelpItem(
            m.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            m.Parameters.Select(p => $"{(p.RefKind == RefKind.None ? "" : p.RefKind.ToString().ToLowerInvariant() + " ")}{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}{(p.IsOptional ? " = " + p.ExplicitDefaultValue : "")}".Trim()).ToArray(),
            m.Parameters.Length, m.IsGenericMethod)).ToArray();
    }

    public async Task<IReadOnlyList<string>> GetAutomaticUsingCandidatesAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var document = FindDocument(filePath);
        var root = await document.GetSyntaxRootAsync(cancellationToken) as Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax;
        var compilation = await document.Project.GetCompilationAsync(cancellationToken);
        if (root is null || compilation is null) return Array.Empty<string>();
        var model = await document.GetSemanticModelAsync(cancellationToken);
        var source = await document.GetTextAsync(cancellationToken);
        var diagnostics = compilation.GetDiagnostics(cancellationToken).Where(d => d.Id == "CS0246" && d.Location.SourceTree == root.SyntaxTree);
        var missingNames = diagnostics.Select(d => d.Location.SourceSpan).Where(span => span.Length > 0 && span.End <= source.Length)
            .Select(span => source.ToString(span).Trim()).Where(name => Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(name)).Distinct(StringComparer.Ordinal).ToArray();
        if (missingNames.Length == 0) return Array.Empty<string>();
        var existing = root.Usings.Select(u => u.Name?.ToString()).Where(n => n is not null).ToHashSet(StringComparer.Ordinal);
        var candidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in missingNames)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var symbol in compilation.GetSymbolsWithName(n => n.Equals(name, StringComparison.Ordinal), SymbolFilter.Type, cancellationToken).OfType<ITypeSymbol>())
            {
                var ns = symbol.ContainingNamespace?.ToDisplayString();
                if (!string.IsNullOrWhiteSpace(ns) && ns != "<global namespace>" && !existing.Contains(ns)) candidates.Add(ns);
            }
            // Include metadata references (framework and NuGet assemblies), not just source symbols.
            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) continue;
                foreach (var symbol in FindTypes(assembly.GlobalNamespace, name))
                {
                    var ns = symbol.ContainingNamespace?.ToDisplayString();
                    if (!string.IsNullOrWhiteSpace(ns) && ns != "<global namespace>" && !existing.Contains(ns)) candidates.Add(ns);
                }
            }
        }
        return candidates.OrderBy(n => n.StartsWith("System", StringComparison.Ordinal) ? 0 : 1).ThenBy(n => n, StringComparer.Ordinal).Take(50).ToArray();
    }

    private static IEnumerable<INamedTypeSymbol> FindTypes(INamespaceSymbol root, string name)
    {
        var stack = new Stack<INamespaceSymbol>(); stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var type in current.GetTypeMembers())
            {
                if (type.Name.Equals(name, StringComparison.Ordinal)) yield return type;
                foreach (var nested in type.GetTypeMembers()) if (nested.Name.Equals(name, StringComparison.Ordinal)) yield return nested;
            }
            foreach (var child in current.GetNamespaceMembers()) stack.Push(child);
        }
    }

    public async Task<string> AddUsingAsync(string filePath, string namespaceName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(namespaceName) || namespaceName.Split('.').Any(part => !Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(part)))
            throw new ArgumentException("Espace de noms invalide.", nameof(namespaceName));
        var document = FindDocument(filePath);
        var root = await document.GetSyntaxRootAsync(cancellationToken) as Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax
            ?? throw new InvalidOperationException("Fichier C# non analysable.");
        if (root.Usings.Any(u => string.Equals(u.Name?.ToString(), namespaceName, StringComparison.Ordinal))) return root.ToFullString();
        var usingDirective = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.UsingDirective(Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseName(namespaceName));
        var usings = root.Usings.Add(usingDirective).OrderBy(u => u.Name?.ToString().StartsWith("System", StringComparison.Ordinal) == true ? 0 : 1)
            .ThenBy(u => u.Name?.ToString(), StringComparer.Ordinal);
        return root.WithUsings(Microsoft.CodeAnalysis.CSharp.SyntaxFactory.List(usings)).ToFullString();
    }

    public async Task<IReadOnlyList<CodeActionSuggestion>> GetCodeActionSuggestionsAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        var document = FindDocument(filePath);
        var root = await document.GetSyntaxRootAsync(cancellationToken) as Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax;
        var model = await document.GetSemanticModelAsync(cancellationToken);
        if (root is null || model is null) return Array.Empty<CodeActionSuggestion>();
        var actions = new List<CodeActionSuggestion>
        {
            new("format-document", "Formater le document avec Roslyn", "Formatage", "Formate le document entier sans changer son intention."),
            new("sort-usings", "Trier les directives using", "Organisation", "Trie les imports System puis les autres espaces de noms.")
        };
        var candidates = await GetAutomaticUsingCandidatesAsync(filePath, cancellationToken);
        actions.AddRange(candidates.Select(ns => new CodeActionSuggestion("using:" + ns, "Ajouter using " + ns, "Import", "Ajoute une directive using au fichier courant.")));
        var unused = new List<string>();
        foreach (var directive in root.Usings)
        {
            if (directive.Alias is not null || directive.Name is null || !directive.GlobalKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.None)) continue;
            var nsSymbol = model.GetSymbolInfo(directive.Name, cancellationToken).Symbol as INamespaceSymbol;
            if (nsSymbol is null) continue;
            var used = root.DescendantNodes().Where(n => !directive.Span.Contains(n.Span)).OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax>()
                .Any(id => model.GetSymbolInfo(id, cancellationToken).Symbol is ISymbol symbol && IsWithinNamespace(symbol.ContainingNamespace, nsSymbol));
            if (!used) unused.Add(directive.Name.ToString());
        }
        if (unused.Count > 0) actions.Add(new CodeActionSuggestion("remove-unused-usings", "Supprimer les using inutilisÃ©s", "Nettoyage", string.Join(", ", unused)));
        return actions;
    }

    public async Task<string> ApplyCodeActionAsync(string filePath, string actionId, CancellationToken cancellationToken = default)
    {
        if (actionId == "format-document") return await FormatDocumentAsync(filePath, cancellationToken);
        if (actionId == "sort-usings") return await SortUsingsAsync(filePath, cancellationToken);
        if (actionId.StartsWith("using:", StringComparison.Ordinal)) return await AddUsingAsync(filePath, actionId[6..], cancellationToken);
        if (actionId == "remove-unused-usings")
        {
            var document = FindDocument(filePath);
            var root = await document.GetSyntaxRootAsync(cancellationToken) as Microsoft.CodeAnalysis.CSharp.Syntax.CompilationUnitSyntax;
            var model = await document.GetSemanticModelAsync(cancellationToken);
            if (root is null || model is null) throw new InvalidOperationException("Fichier C# non analysable.");
            var remove = new HashSet<Microsoft.CodeAnalysis.CSharp.Syntax.UsingDirectiveSyntax>();
            foreach (var directive in root.Usings)
            {
                if (directive.Alias is not null || directive.Name is null || !directive.GlobalKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.None)) continue;
                var ns = model.GetSymbolInfo(directive.Name, cancellationToken).Symbol as INamespaceSymbol;
                if (ns is null) continue;
                var used = root.DescendantNodes().Where(n => !directive.Span.Contains(n.Span)).OfType<Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax>()
                    .Any(id => model.GetSymbolInfo(id, cancellationToken).Symbol is ISymbol symbol && IsWithinNamespace(symbol.ContainingNamespace, ns));
                if (!used) remove.Add(directive);
            }
            return root.RemoveNodes(remove, Microsoft.CodeAnalysis.SyntaxRemoveOptions.KeepNoTrivia)?.ToFullString() ?? root.ToFullString();
        }
        throw new ArgumentOutOfRangeException(nameof(actionId), "Action de code inconnue.");
    }

    private static bool IsWithinNamespace(INamespaceSymbol? symbol, INamespaceSymbol candidate)
    {
        for (var current = symbol; current is not null; current = current.ContainingNamespace)
            if (SymbolEqualityComparer.Default.Equals(current, candidate)) return true;
        return false;
    }

    private Document FindDocument(string filePath)
    {
        if (Solution is null) throw new InvalidOperationException("Chargez une solution ou un projet .NET d'abord.");
        var full = Path.GetFullPath(filePath);
        return Solution.Projects.SelectMany(p => p.Documents)
            .FirstOrDefault(d => d.FilePath is not null && Path.GetFullPath(d.FilePath).Equals(full, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException("Le fichier n'appartient pas Ã  la solution Roslyn chargÃ©e : " + full);
    }

    public ValueTask DisposeAsync()
    {
        if (_workspace is not null) _workspace.Dispose();
        _workspace = null; Solution = null;
        return ValueTask.CompletedTask;
    }
}

public sealed record SemanticDiagnostic(string Project, string Id, string Severity, string Message, string File, int Line, int Column)
{
    public override string ToString() => $"{Severity} {Id} [{Project}] {File}({Line},{Column}) : {Message}";
}
public sealed record RenameResult(string OldSymbol, string NewSymbol, IReadOnlyList<string> ChangedFiles, IReadOnlyDictionary<string, string> ChangedContents);

public sealed record SymbolLocation(string File, int Line, int Column);
public sealed record SymbolNavigationResult(string Symbol, IReadOnlyList<SymbolLocation> Locations);
public sealed record SymbolReference(string File, int Line, int Column, bool IsAlias);

public sealed record SignatureHelpItem(string Signature, IReadOnlyList<string> Parameters, int ParameterCount, bool IsGeneric);
public sealed record CodeActionSuggestion(string Id, string Title, string Category, string Description);
