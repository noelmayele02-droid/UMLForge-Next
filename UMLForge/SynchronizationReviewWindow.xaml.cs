using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using UMLForge.Services;

namespace UMLForge;

public sealed class SyncReviewRow
{
    public string RelativePath { get; init; } = "";
    public string State { get; init; } = "PrÃªt";
    public SyncDecision[] Decisions { get; init; } = Enum.GetValues<SyncDecision>();
    public SyncDecision Decision { get; set; } = SyncDecision.AcceptGeneratedCode;
    public SyncFilePlan Plan { get; init; } = null!;
}

public partial class SynchronizationReviewWindow : Window
{
    private readonly SyncPlan _plan;
    private readonly ObservableCollection<SyncReviewRow> _rows = new();

    public SynchronizationReviewWindow(SyncPlan plan)
    {
        InitializeComponent(); _plan = plan;
        foreach (var file in plan.Files)
        {
            var conflict = plan.Conflicts.FirstOrDefault(c => c.RelativePath == file.RelativePath);
            _rows.Add(new SyncReviewRow
            {
                RelativePath = file.RelativePath,
                State = conflict is null ? (file.CurrentCode is null ? "Nouveau fichier" : "Modification") : "CONFLIT â€” " + conflict.Kind + (file.PotentialRenameFrom is not null ? " Â· possible renommage depuis " + file.PotentialRenameFrom : file.PotentialRenameTo is not null ? " Â· possible renommage vers " + file.PotentialRenameTo : ""),
                Decision = conflict is null ? file.RecommendedDecision : SyncDecision.Skip,
                Decisions = file.GeneratedCode is null ? new[] { SyncDecision.Skip, SyncDecision.KeepCurrentCode, SyncDecision.AcceptGeneratedDeletion, SyncDecision.MergeManually } : file.SuggestedMerge is null ? Enum.GetValues<SyncDecision>().Where(d => d is not (SyncDecision.AcceptSemanticMerge or SyncDecision.AcceptGeneratedDeletion)).ToArray() : Enum.GetValues<SyncDecision>().Where(d => d != SyncDecision.AcceptGeneratedDeletion).ToArray(),
                Plan = file
            });
        }
        PlanGrid.ItemsSource = _rows;
        SummaryText.Text = $"{_rows.Count} fichier(s) Â· {plan.Conflicts.Count} conflit(s). Les conflits sont ignorÃ©s par dÃ©faut; aucune Ã©criture sans dÃ©cision.";
        if (_rows.Count > 0) PlanGrid.SelectedIndex = 0;
    }

    private void PlanGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PlanGrid.SelectedItem is not SyncReviewRow row) return;
        CurrentText.Text = row.Plan.CurrentCode ?? "// Le fichier n'existe pas encore.";
        GeneratedText.Text = row.Plan.SuggestedMerge is not null
            ? "// FUSION SEMANTIQUE PROPOSEE - aperçu uniquement" + Environment.NewLine + row.Plan.SuggestedMerge
            : row.Plan.GeneratedCode ?? "// Aucun code généré.";
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var decisions = _rows.ToDictionary(r => r.RelativePath, r => r.Decision, StringComparer.OrdinalIgnoreCase);
            var history = Path.Combine(_plan.ProjectRoot, ".umlforge", "history");
            await SynchronizationEngine.ApplyPlanAsync(_plan, decisions, history);
            var statePath = Path.Combine(_plan.ProjectRoot, ".umlforge", "baseline.json");
            var baseline = File.Exists(statePath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(await File.ReadAllTextAsync(statePath)) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in _rows)
            {
                if (row.Decision == SyncDecision.AcceptGeneratedDeletion)
                {
                    baseline.Remove(row.RelativePath);
                    continue;
                }
                if (row.Decision == SyncDecision.AcceptSemanticMerge)
                {
                    // Keep the generated snapshot as the baseline, not the merged file; this lets the next sync recognize preserved manual edits.
                    if (row.Plan.GeneratedCode is not null) baseline[row.RelativePath] = row.Plan.GeneratedCode;
                    continue;
                }
                if (row.Decision != SyncDecision.AcceptGeneratedCode) continue;
                var full = Path.GetFullPath(Path.Combine(_plan.ProjectRoot, row.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (File.Exists(full)) baseline[row.RelativePath] = await File.ReadAllTextAsync(full);
                else baseline.Remove(row.RelativePath);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(baseline, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            MessageBox.Show($"Synchronisation terminÃ©e. Historique : {history}", "UMLForge", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true; Close();
        }
        catch (Exception ex) { MessageBox.Show("Aucune synchronisation confirmÃ©e. RecrÃ©ez l'aperÃ§u si les fichiers ont changÃ©.\n\n" + ex.Message, "Synchronisation interrompue", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
