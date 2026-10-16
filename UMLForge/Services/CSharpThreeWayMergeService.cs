using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UMLForge.Services;

public sealed record SemanticMergeConflict(string Symbol, string Reason);
public sealed record SemanticMergeResult(bool Success, string? MergedCode, IReadOnlyList<SemanticMergeConflict> Conflicts, IReadOnlyList<string> MergedSymbols, IReadOnlyList<string> DeletedSymbols);

/// <summary>
/// Conservative syntax-aware three-way merge for top-level C# types and their direct members.
/// It merges independent member edits, honors one-sided deletions, and blocks same-member edits.
/// It intentionally refuses ambiguous structural edits rather than guessing.
/// </summary>
public static class CSharpThreeWayMergeService
{
    public static SemanticMergeResult Merge(string baselineText, string currentText, string generatedText)
    {
        var baselineTree = CSharpSyntaxTree.ParseText(baselineText);
        var currentTree = CSharpSyntaxTree.ParseText(currentText);
        var generatedTree = CSharpSyntaxTree.ParseText(generatedText);
        var errors = new[] { baselineTree, currentTree, generatedTree }
            .SelectMany(t => t.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
            .Select(d => new SemanticMergeConflict("<syntax>", d.ToString())).ToArray();
        if (errors.Length > 0) return new(false, null, errors, Array.Empty<string>(), Array.Empty<string>());

        var baseRoot = baselineTree.GetCompilationUnitRoot();
        var currentRoot = currentTree.GetCompilationUnitRoot();
        var generatedRoot = generatedTree.GetCompilationUnitRoot();
        var structuralConflicts = CheckUnsupportedStructuralEdits(baseRoot, currentRoot, generatedRoot);
        if (structuralConflicts.Count > 0) return new(false, null, structuralConflicts, Array.Empty<string>(), Array.Empty<string>());
        var baseTypes = IndexTypes(baseRoot);
        var currentTypes = IndexTypes(currentRoot);
        var generatedTypes = IndexTypes(generatedRoot);
        var conflicts = new List<SemanticMergeConflict>();
        var mergedSymbols = new List<string>();
        var deletedSymbols = new List<string>();
        var rootExterns = ChooseList(baseRoot.Externs, currentRoot.Externs, generatedRoot.Externs, "extern aliases", conflicts);
        var rootUsings = ChooseList(baseRoot.Usings, currentRoot.Usings, generatedRoot.Usings, "using directives", conflicts);
        var rootAttributes = ChooseList(baseRoot.AttributeLists, currentRoot.AttributeLists, generatedRoot.AttributeLists, "assembly/module attributes", conflicts);
        if (conflicts.Count > 0) return new(false, null, conflicts, mergedSymbols, deletedSymbols);
        var resultRoot = currentRoot.WithExterns(rootExterns).WithUsings(rootUsings).WithAttributeLists(rootAttributes);

        foreach (var key in baseTypes.Keys.Union(currentTypes.Keys).Union(generatedTypes.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            baseTypes.TryGetValue(key, out var b);
            currentTypes.TryGetValue(key, out var c);
            generatedTypes.TryGetValue(key, out var g);
            if (b is null)
            {
                if (c is not null && g is not null && c.ToFullString() != g.ToFullString()) conflicts.Add(new(key, "Type ajouté différemment des deux côtés."));
                else if (c is null && g is not null)
                {
                    var before = conflicts.Count;
                    resultRoot = InsertType(resultRoot, g, key, conflicts);
                    if (conflicts.Count == before) mergedSymbols.Add(key);
                }
                continue;
            }
            if (c is null || g is null)
            {
                var surviving = c ?? g;
                if (surviving is null) { deletedSymbols.Add(key); continue; }
                if (EquivalentTypeShell(b, surviving) && EquivalentMembers(b, surviving))
                {
                    if (c is not null) resultRoot = resultRoot.RemoveNode(c, SyntaxRemoveOptions.KeepExteriorTrivia) as CompilationUnitSyntax ?? resultRoot;
                    deletedSymbols.Add(key);
                }
                else conflicts.Add(new(key, "Suppression d'un type en concurrence avec des modifications de ce type."));
                continue;
            }

            var bShell = Shell(b); var cShell = Shell(c); var gShell = Shell(g);
            TypeDeclarationSyntax chosenShell;
            if (cShell.ToFullString() == gShell.ToFullString()) chosenShell = c;
            else if (cShell.ToFullString() == bShell.ToFullString()) chosenShell = g;
            else if (gShell.ToFullString() == bShell.ToFullString()) chosenShell = c;
            else { conflicts.Add(new(key, "La déclaration du type (base, attributs ou modificateurs) a changé des deux côtés.")); continue; }

            var bMembers = IndexMembers(b);
            var cMembers = IndexMembers(c);
            var gMembers = IndexMembers(g);
            var chosen = new Dictionary<string, MemberDeclarationSyntax>(StringComparer.Ordinal);
            var typeHasConflict = false;
            foreach (var memberKey in bMembers.Keys.Union(cMembers.Keys).Union(gMembers.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                bMembers.TryGetValue(memberKey, out var bm); cMembers.TryGetValue(memberKey, out var cm); gMembers.TryGetValue(memberKey, out var gm);
                if (bm is null)
                {
                    if (cm is not null && gm is not null && cm.ToFullString() != gm.ToFullString())
                    { conflicts.Add(new(key + "." + memberKey, "Membre ajouté différemment des deux côtés.")); typeHasConflict = true; }
                    else if (cm is not null || gm is not null) chosen[memberKey] = cm ?? gm!;
                    continue;
                }
                if (cm is null && gm is null)
                {
                    var currentRename = FindRenameCandidate(bm, cMembers);
                    var generatedRename = FindRenameCandidate(bm, gMembers);
                    if (currentRename is null && generatedRename is null) { deletedSymbols.Add(key + "." + memberKey); continue; }
                    if (currentRename is not null && generatedRename is not null && !currentRename.Value.Key.StartsWith("\0", StringComparison.Ordinal) && !generatedRename.Value.Key.StartsWith("\0", StringComparison.Ordinal) && currentRename.Value.Key == generatedRename.Value.Key && currentRename.Value.Value.ToFullString() == generatedRename.Value.Value.ToFullString())
                    { deletedSymbols.Add(key + "." + memberKey); continue; }
                    conflicts.Add(new(key + "." + memberKey, "Renommage concurrent détecté ou renommage en conflit avec une suppression. Résolution manuelle requise."));
                    typeHasConflict = true; continue;
                }
                if (cm is null)
                {
                    if (gm!.ToFullString() == bm.ToFullString()) { deletedSymbols.Add(key + "." + memberKey); continue; }
                    conflicts.Add(new(key + "." + memberKey, "Membre supprimé côté code mais modifié côté génération.")); typeHasConflict = true; continue;
                }
                if (gm is null)
                {
                    if (cm.ToFullString() == bm.ToFullString()) { deletedSymbols.Add(key + "." + memberKey); continue; }
                    conflicts.Add(new(key + "." + memberKey, "Membre supprimé côté génération mais modifié côté code.")); typeHasConflict = true; continue;
                }
                var bt = bm.ToFullString(); var ct = cm.ToFullString(); var gt = gm.ToFullString();
                if (ct == gt) chosen[memberKey] = cm;
                else if (ct == bt) { chosen[memberKey] = gm; mergedSymbols.Add(key + "." + memberKey); }
                else if (gt == bt) chosen[memberKey] = cm;
                else { conflicts.Add(new(key + "." + memberKey, "Même membre modifié différemment des deux côtés.")); typeHasConflict = true; }
            }
            if (typeHasConflict) continue;
            var mergedType = chosenShell.WithMembers(SyntaxFactory.List(chosen.OrderBy(kv => MemberOrder(kv.Value)).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Value)));
            resultRoot = resultRoot.ReplaceNode(c, mergedType);
        }
        if (conflicts.Count > 0) return new(false, null, conflicts, mergedSymbols, deletedSymbols);
        return new(true, resultRoot.NormalizeWhitespace().ToFullString() + Environment.NewLine, conflicts, mergedSymbols, deletedSymbols);
    }

    private static List<SemanticMergeConflict> CheckUnsupportedStructuralEdits(CompilationUnitSyntax baseline, CompilationUnitSyntax current, CompilationUnitSyntax generated)
    {
        var conflicts = new List<SemanticMergeConflict>();
        var baseUnsupported = UnsupportedStructure(baseline);
        var currentUnsupported = UnsupportedStructure(current);
        var generatedUnsupported = UnsupportedStructure(generated);
        if (baseUnsupported != currentUnsupported || baseUnsupported != generatedUnsupported)
            conflicts.Add(new("<structure>", "Un changement porte sur une structure non fusionnée automatiquement (enum, delegate, instructions top-level ou déclaration hors type). Résolution manuelle requise."));
        var baseNestedUsings = NestedUsingSignature(baseline);
        var currentNestedUsings = NestedUsingSignature(current);
        var generatedNestedUsings = NestedUsingSignature(generated);
        if (baseNestedUsings != currentNestedUsings || baseNestedUsings != generatedNestedUsings)
            conflicts.Add(new("<using>", "Les directives using internes aux espaces de noms ont changé ; la fusion automatique de ces directives n'est pas encore prise en charge."));
        return conflicts;
    }

    private static string UnsupportedStructure(CompilationUnitSyntax root)
    {
        var unsupported = root.DescendantNodes().Where(n => n is EnumDeclarationSyntax or DelegateDeclarationSyntax or GlobalStatementSyntax)
            .Select(n => n.ToFullString()).OrderBy(s => s, StringComparer.Ordinal).ToList();
        unsupported.AddRange(root.Members.Where(m => m is not TypeDeclarationSyntax and not BaseNamespaceDeclarationSyntax).Select(m => m.ToFullString()).OrderBy(s => s, StringComparer.Ordinal));
        return string.Join("\n--\n", unsupported);
    }

    private static string NestedUsingSignature(CompilationUnitSyntax root) => string.Join("\n", root.DescendantNodes().OfType<UsingDirectiveSyntax>()
        .Where(u => u.Parent is not CompilationUnitSyntax).Select(u => u.ToFullString()).OrderBy(s => s, StringComparer.Ordinal));

    private static SyntaxList<T> ChooseList<T>(SyntaxList<T> baseline, SyntaxList<T> current, SyntaxList<T> generated, string label, List<SemanticMergeConflict> conflicts) where T : SyntaxNode
    {
        static string Signature(SyntaxList<T> list) => string.Join("", list.Select(n => n.ToFullString()));
        var b = Signature(baseline); var c = Signature(current); var g = Signature(generated);
        if (c == g) return current;
        if (c == b) return generated;
        if (g == b) return current;
        conflicts.Add(new("<" + label + ">", "Les " + label + " ont été modifiés différemment des deux côtés."));
        return current;
    }

    private static CompilationUnitSyntax InsertType(CompilationUnitSyntax root, TypeDeclarationSyntax type, string key, List<SemanticMergeConflict> conflicts)
    {
        var namespaceName = string.Join('.', type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));
        if (string.IsNullOrWhiteSpace(namespaceName)) return root.AddMembers(type);
        var existing = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault(n => n.Name.ToString().Equals(namespaceName, StringComparison.Ordinal));
        if (existing is not null) return root.ReplaceNode(existing, existing.WithMembers(existing.Members.Add(type)));
        conflicts.Add(new(key, $"Nouveau type dans l'espace de noms « {namespaceName} » : fusion automatique refusée car la structure de namespace n'est pas présente côté code."));
        return root;
    }

    private static Dictionary<string, TypeDeclarationSyntax> IndexTypes(CompilationUnitSyntax root)
    {
        var map = new Dictionary<string, TypeDeclarationSyntax>(StringComparer.Ordinal);
        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>().Where(t => !t.Ancestors().OfType<TypeDeclarationSyntax>().Any()))
        {
            var ns = string.Join('.', type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));
            map[(ns.Length == 0 ? "" : ns + ".") + type.Identifier.ValueText] = type;
        }
        return map;
    }

    private static Dictionary<string, MemberDeclarationSyntax> IndexMembers(TypeDeclarationSyntax type)
    {
        var map = new Dictionary<string, MemberDeclarationSyntax>(StringComparer.Ordinal);
        foreach (var member in type.Members)
        {
            var key = member switch
            {
                MethodDeclarationSyntax m => "method:" + m.Identifier.ValueText + m.ParameterList.ToString(),
                ConstructorDeclarationSyntax c => "ctor:" + c.Identifier.ValueText + c.ParameterList.ToString(),
                PropertyDeclarationSyntax p => "property:" + p.Identifier.ValueText,
                IndexerDeclarationSyntax i => "indexer:" + i.ParameterList.ToString(),
                EventDeclarationSyntax e => "event:" + e.Identifier.ValueText,
                FieldDeclarationSyntax f => "field:" + string.Join(",", f.Declaration.Variables.Select(v => v.Identifier.ValueText)),
                EventFieldDeclarationSyntax ef => "eventfield:" + string.Join(",", ef.Declaration.Variables.Select(v => v.Identifier.ValueText)),
                TypeDeclarationSyntax t => "nestedtype:" + t.Identifier.ValueText,
                EnumDeclarationSyntax en => "enum:" + en.Identifier.ValueText,
                _ => "kind:" + member.RawKind + ":" + member.ToString().Split('\n')[0].Trim()
            };
            if (map.ContainsKey(key)) key += "#" + map.Count;
            map[key] = member;
        }
        return map;
    }

    private static TypeDeclarationSyntax Shell(TypeDeclarationSyntax type) => type.WithMembers(default).WithOpenBraceToken(default).WithCloseBraceToken(default);
    private static bool EquivalentTypeShell(TypeDeclarationSyntax a, TypeDeclarationSyntax b) => Shell(a).ToFullString() == Shell(b).ToFullString();
    private static bool EquivalentMembers(TypeDeclarationSyntax a, TypeDeclarationSyntax b) => a.Members.Select(m => m.ToFullString()).SequenceEqual(b.Members.Select(m => m.ToFullString()));
    private static KeyValuePair<string, MemberDeclarationSyntax>? FindRenameCandidate(MemberDeclarationSyntax baseline, Dictionary<string, MemberDeclarationSyntax> candidates)
    {
        var fingerprint = RenameFingerprint(baseline);
        if (fingerprint is null) return null;
        var matches = candidates.Where(pair => RenameFingerprint(pair.Value) == fingerprint).ToArray();
        if (matches.Length == 1) return matches[0];
        if (matches.Length > 1) return new KeyValuePair<string, MemberDeclarationSyntax>("\0AMBIGUOUS_RENAME", matches[0].Value);
        return null;
    }

    private static string? RenameFingerprint(MemberDeclarationSyntax member)
    {
        Microsoft.CodeAnalysis.SyntaxToken token = member switch
        {
            MethodDeclarationSyntax m => m.Identifier,
            PropertyDeclarationSyntax p => p.Identifier,
            EventDeclarationSyntax e => e.Identifier,
            TypeDeclarationSyntax t => t.Identifier,
            FieldDeclarationSyntax f when f.Declaration.Variables.Count == 1 => f.Declaration.Variables[0].Identifier,
            EventFieldDeclarationSyntax e when e.Declaration.Variables.Count == 1 => e.Declaration.Variables[0].Identifier,
            _ => default
        };
        if (token == default) return null;
        var replacement = SyntaxFactory.Identifier("__UMLFORGE_RENAME__");
        return member.ReplaceToken(token, replacement).NormalizeWhitespace().ToFullString();
    }

    private static int MemberOrder(MemberDeclarationSyntax m) => m switch { FieldDeclarationSyntax => 0, ConstructorDeclarationSyntax => 1, PropertyDeclarationSyntax => 2, MethodDeclarationSyntax => 3, _ => 4 };
}
