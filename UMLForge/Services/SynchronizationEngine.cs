using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace UMLForge.Services;

public enum SyncConflictKind { BothSidesChanged, CodeDeleted, GeneratedDeletion, ParseError }
public enum SyncDecision { KeepCurrentCode, AcceptGeneratedCode, AcceptSemanticMerge, AcceptGeneratedDeletion, MergeManually, Skip }

public sealed record SyncConflict(string RelativePath, SyncConflictKind Kind, string Message, string? Baseline, string? CurrentCode, string? GeneratedCode);
public sealed record SyncFilePlan(string RelativePath, string? Baseline, string? CurrentCode, string? GeneratedCode, bool HasConflict, string? CurrentHash, string? GeneratedHash, string? SuggestedMerge = null, string? PotentialRenameFrom = null, string? PotentialRenameTo = null, SyncDecision RecommendedDecision = SyncDecision.AcceptGeneratedCode);
public sealed class SyncPlan
{
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public string ProjectRoot { get; init; } = "";
    public List<SyncFilePlan> Files { get; init; } = new();
    public List<SyncConflict> Conflicts { get; init; } = new();
    public bool RequiresReview => Conflicts.Count > 0;
}

/// <summary>
/// Safe, preview-first UML-to-code synchronization. It never writes files during planning.
/// A baseline is required to distinguish user edits from model-generated edits.
/// </summary>
public static class SynchronizationEngine
{
    public static SyncPlan CreatePlan(string projectRoot, IReadOnlyDictionary<string, string> generatedFiles,
        IReadOnlyDictionary<string, string> baselineSnapshots)
    {
        var root = Path.GetFullPath(projectRoot);
        var plan = new SyncPlan { ProjectRoot = root };
        var normalizedGeneratedPaths = generatedFiles.Keys.Select(NormalizeRelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removedBaselinePaths = baselineSnapshots.Keys.Select(NormalizeRelativePath).Where(p => !normalizedGeneratedPaths.Contains(p)).ToArray();
        var renameCandidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in generatedFiles)
        {
            var newPath = NormalizeRelativePath(item.Key);
            if (baselineSnapshots.ContainsKey(newPath) || !newPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
            var newTypes = ExtractTypeNames(item.Value);
            var candidates = removedBaselinePaths.Where(old => old.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && baselineSnapshots.TryGetValue(old, out var oldText) && newTypes.Overlaps(ExtractTypeNames(oldText))).ToArray();
            if (candidates.Length == 1) renameCandidates[newPath] = candidates[0];
        }
        foreach (var item in generatedFiles.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var relative = NormalizeRelativePath(item.Key);
            var fullPath = ResolveInsideRoot(root, relative);
            var generated = item.Value;
            var current = File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
            baselineSnapshots.TryGetValue(relative, out var baseline);
            var conflict = false;
            SyncConflictKind? kind = null;
            var message = "";
            var recommendedDecision = SyncDecision.AcceptGeneratedCode;
            if (current is not null && baseline is not null && generated is not null &&
                !Equivalent(current, baseline) && Equivalent(generated, baseline) && !Equivalent(current, generated))
                recommendedDecision = SyncDecision.KeepCurrentCode;
            string? potentialRenameFrom = null;
            if (baseline is null && renameCandidates.TryGetValue(relative, out var oldPath))
            {
                conflict = true; kind = SyncConflictKind.BothSidesChanged; potentialRenameFrom = oldPath;
                message = $"Le type ressemble Ã  un renommage/dÃ©placement depuis Â« {oldPath} Â». VÃ©rifiez les rÃ©fÃ©rences et le fichier source avant validation.";
            }

            if (generated is not null && !LooksLikeCSharp(generated))
            {
                conflict = true; kind = SyncConflictKind.ParseError; message = "Le code gÃ©nÃ©rÃ© contient des erreurs de syntaxe C#; aucune Ã©criture automatique n'est autorisÃ©e.";
            }
            else if (current is null && baseline is not null)
            {
                conflict = true; kind = SyncConflictKind.CodeDeleted; message = "Le fichier existait dans la rÃ©fÃ©rence, mais il a Ã©tÃ© supprimÃ© du disque.";
            }
            else if (current is not null && baseline is not null && generated is not null &&
                     !Equivalent(current, baseline) && !Equivalent(generated, baseline) && !Equivalent(current, generated))
            {
                conflict = true; kind = SyncConflictKind.BothSidesChanged; message = "Le code manuel et le code gÃ©nÃ©rÃ© ont tous deux changÃ© depuis la derniÃ¨re synchronisation.";
            }
            else if (current is not null && baseline is null && generated is not null && !Equivalent(current, generated))
            {
                conflict = true; kind = SyncConflictKind.BothSidesChanged; message = "Le fichier existe dÃ©jÃ  sans rÃ©fÃ©rence de synchronisation. VÃ©rification manuelle requise.";
            }

            string? suggestedMerge = null;
            if (conflict && baseline is not null && current is not null && generated is not null && relative.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                var merge = CSharpThreeWayMergeService.Merge(baseline, current, generated);
                if (merge.Success) suggestedMerge = merge.MergedCode;
                else if (merge.Conflicts.Count > 0)
                    message += $" Fusion sÃ©mantique : {merge.Conflicts.Count} conflit(s) de membre; aucune fusion automatique sÃ»re.";
            }
            plan.Files.Add(new SyncFilePlan(relative, baseline, current, generated, conflict, Hash(current), Hash(generated), suggestedMerge, potentialRenameFrom, null, recommendedDecision));
            if (conflict) plan.Conflicts.Add(new SyncConflict(relative, kind!.Value, message, baseline, current, generated));
        }
        foreach (var removedPath in removedBaselinePaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var fullPath = ResolveInsideRoot(root, removedPath);
            var current = File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
            var baseline = baselineSnapshots.TryGetValue(removedPath, out var oldSnapshot) ? oldSnapshot : null;
            var renameTo = renameCandidates.FirstOrDefault(kv => kv.Value.Equals(removedPath, StringComparison.OrdinalIgnoreCase)).Key;
            var message = renameTo is null ? "Le fichier a disparu du code gÃ©nÃ©rÃ©. La suppression doit Ãªtre confirmÃ©e explicitement." : $"Suppression possiblement liÃ©e Ã  un dÃ©placement vers Â« {renameTo} Â». Le dÃ©placement automatique n'est pas supposÃ©.";
            if (current is not null && baseline is not null && !Equivalent(current, baseline)) message += " Le fichier courant contient des modifications manuelles.";
            plan.Files.Add(new SyncFilePlan(removedPath, baseline, current, null, true, Hash(current), null, null, null, renameTo, SyncDecision.Skip));
            plan.Conflicts.Add(new SyncConflict(removedPath, SyncConflictKind.GeneratedDeletion, message, baseline, current, null));
        }
        return plan;
    }

    /// <summary>Apply an explicitly reviewed plan. Each overwritten file is backed up and the old/new snapshots are recorded.</summary>
    public static async Task ApplyPlanAsync(SyncPlan plan, IReadOnlyDictionary<string, SyncDecision> decisions,
        string historyDirectory, CancellationToken cancellationToken = default)
    {
        var root = Path.GetFullPath(plan.ProjectRoot);
        var historyRoot = Path.GetFullPath(historyDirectory);
        Directory.CreateDirectory(historyRoot);
        var batch = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var batchDir = Path.Combine(historyRoot, batch);
        Directory.CreateDirectory(batchDir);
        var manifest = new List<object>();
        var operations = new List<(SyncFilePlan File, string FullPath, SyncDecision Decision, string? Content, string? Original, string? BackupPath, string? TempPath)>();

        // Phase 1: resolve decisions and validate every optimistic-concurrency hash before touching any target.
        foreach (var file in plan.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decision = decisions.TryGetValue(file.RelativePath, out var d) ? d : SyncDecision.Skip;
            if (decision is SyncDecision.Skip or SyncDecision.KeepCurrentCode or SyncDecision.MergeManually) continue;
            var isDelete = decision == SyncDecision.AcceptGeneratedDeletion;
            if (isDelete && file.GeneratedCode is not null)
                throw new InvalidOperationException($"'{file.RelativePath}' n'est pas une suppression gÃ©nÃ©rÃ©e.");
            if (!isDelete && file.GeneratedCode is null && decision != SyncDecision.AcceptSemanticMerge)
                throw new InvalidOperationException($"'{file.RelativePath}' n'a pas de code gÃ©nÃ©rÃ© Ã  Ã©crire.");
            if (decision == SyncDecision.AcceptSemanticMerge && file.SuggestedMerge is null)
                throw new InvalidOperationException($"Aucune fusion sÃ©mantique sans conflit n'est disponible pour '{file.RelativePath}'.");
            if (file.HasConflict && decision is not (SyncDecision.AcceptGeneratedCode or SyncDecision.AcceptSemanticMerge or SyncDecision.AcceptGeneratedDeletion))
                throw new InvalidOperationException($"Le conflit '{file.RelativePath}' n'a pas de dÃ©cision explicite.");

            var fullPath = ResolveInsideRoot(root, file.RelativePath);
            var original = File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath, cancellationToken) : null;
            if (!string.Equals(Hash(original), file.CurrentHash, StringComparison.Ordinal))
                throw new IOException($"'{file.RelativePath}' a changÃ© depuis l'aperÃ§u. RecrÃ©ez le plan de synchronisation. Aucune Ã©criture n'a Ã©tÃ© effectuÃ©e.");
            var backupPath = original is null ? null : ResolveInsideRoot(batchDir, file.RelativePath);
            var content = isDelete ? null : decision == SyncDecision.AcceptSemanticMerge ? file.SuggestedMerge : file.GeneratedCode;
            var tempPath = content is null ? null : fullPath + ".umlforge-" + batch + ".tmp";
            operations.Add((file, fullPath, decision, content, original, backupPath, tempPath));
        }

        // Phase 2: stage backups and new content. No target has been changed yet.
        try
        {
            foreach (var op in operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (op.BackupPath is not null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(op.BackupPath)!);
                    await File.WriteAllTextAsync(op.BackupPath, op.Original!, new UTF8Encoding(false), cancellationToken);
                }
                if (op.TempPath is not null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(op.FullPath)!);
                    if (File.Exists(op.TempPath)) File.Delete(op.TempPath);
                    await File.WriteAllTextAsync(op.TempPath, op.Content!, new UTF8Encoding(false), cancellationToken);
                }
            }
        }
        catch
        {
            foreach (var op in operations) if (op.TempPath is not null && File.Exists(op.TempPath)) File.Delete(op.TempPath);
            throw;
        }

        // Phase 3: commit. If any move/delete fails, restore every touched path from its staged backup.
        var applied = new List<(SyncFilePlan File, string FullPath, SyncDecision Decision, string? Content, string? Original, string? BackupPath, string? TempPath)>();
        try
        {
            foreach (var op in operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentImmediatelyBeforeCommit = File.Exists(op.FullPath) ? await File.ReadAllTextAsync(op.FullPath, cancellationToken) : null;
                if (!string.Equals(Hash(currentImmediatelyBeforeCommit), op.File.CurrentHash, StringComparison.Ordinal))
                    throw new IOException($"'{op.File.RelativePath}' a Ã©tÃ© modifiÃ© pendant la synchronisation. Annulation et restauration des fichiers dÃ©jÃ  touchÃ©s.");
                if (op.Decision == SyncDecision.AcceptGeneratedDeletion)
                {
                    if (File.Exists(op.FullPath)) File.Delete(op.FullPath);
                    applied.Add(op);
                    manifest.Add(new { op.File.RelativePath, decision = op.Decision.ToString(), oldHash = op.File.CurrentHash, newHash = (string?)null, deleted = op.Original is not null, backup = op.Original is null ? null : op.File.RelativePath });
                }
                else
                {
                    File.Move(op.TempPath!, op.FullPath, true);
                    applied.Add(op);
                    manifest.Add(new { op.File.RelativePath, decision = op.Decision.ToString(), oldHash = op.File.CurrentHash, newHash = Hash(op.Content), deleted = false, backup = op.Original is null ? null : op.File.RelativePath });
                }
            }
            var manifestTemp = Path.Combine(batchDir, "manifest.json.tmp");
            await File.WriteAllTextAsync(manifestTemp, System.Text.Json.JsonSerializer.Serialize(new { plan.CreatedAtUtc, appliedAtUtc = DateTime.UtcNow, files = manifest }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false), cancellationToken);
            File.Move(manifestTemp, Path.Combine(batchDir, "manifest.json"), true);
        }
        catch
        {
            foreach (var op in applied.AsEnumerable().Reverse())
            {
                try
                {
                    if (op.Original is null)
                    {
                        if (File.Exists(op.FullPath)) File.Delete(op.FullPath);
                    }
                    else if (op.BackupPath is not null && File.Exists(op.BackupPath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(op.FullPath)!);
                        File.Copy(op.BackupPath, op.FullPath, true);
                    }
                }
                catch { /* preserve the original exception; backups remain in the history batch */ }
                if (op.TempPath is not null && File.Exists(op.TempPath)) try { File.Delete(op.TempPath); } catch { }
            }
            throw;
        }
    }

    public static bool LooksLikeCSharp(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        return !tree.GetDiagnostics().Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
    }

    private static HashSet<string> ExtractTypeNames(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        return root.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax>()
            .Select(n => n.Identifier.ValueText).Where(n => !string.IsNullOrWhiteSpace(n)).ToHashSet(StringComparer.Ordinal);
    }

    private static bool Equivalent(string? a, string? b) => Normalize(a) == Normalize(b);
    private static string Normalize(string? text) => (text ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    private static string? Hash(string? text) => text is null ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Split('/').Any(p => p == "..")) throw new InvalidOperationException("Le chemin relatif ne peut pas sortir du projet.");
        return normalized;
    }
    private static string ResolveInsideRoot(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.Equals(root, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chemin hors du dossier autorisÃ©.");
        return full;
    }
}
