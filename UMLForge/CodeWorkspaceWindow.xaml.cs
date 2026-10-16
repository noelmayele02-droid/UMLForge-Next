using Microsoft.CodeAnalysis;
using Microsoft.Win32;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json;
using UMLForge.Services;
using UMLForge.Models;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UMLForge;

public partial class CodeWorkspaceWindow : Window
{
    private string? _root;
    private string? _currentFile;
    private bool _loading;
    private bool _dirty;
    private readonly SolutionSemanticService _semantic = new();
    private int _completionStartOffset;
    private int _completionLength;

    public CodeWorkspaceWindow(string? initialFolder = null)
    {
        InitializeComponent();
        Closed += async (_, _) => await _semantic.DisposeAsync();
        CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        CodeEditor.Options.EnableHyperlinks = true;
        CodeEditor.Options.EnableEmailHyperlinks = false;
        if (!string.IsNullOrWhiteSpace(initialFolder) && Directory.Exists(initialFolder)) LoadFolder(initialFolder);
        else Output("Bienvenue dans UMLForge Studio. Ouvrez un dossier contenant une solution .sln ou un projet .csproj.");
    }

    private async void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewProjectDialog { Owner = this };
        if (dialog.ShowDialog() != true) return;
        var parent = new OpenFolderDialog { Title = "Choisir le dossier parent du nouveau projet" };
        if (parent.ShowDialog() != true) return;
        var safeName = new string(dialog.ProjectName.Trim().Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray());
        if (string.IsNullOrWhiteSpace(safeName)) { MessageBox.Show("Le nom du projet est invalide."); return; }
        var template = dialog.Template switch { "BibliothÃ¨que de classes" => "classlib", "Application web ASP.NET Core" => "webapi", "Application console" => "console", "Application de tests xUnit" => "xunit", _ => "console" };
        try
        {
            var psi = new ProcessStartInfo("dotnet", $"new {template} -n {safeName} -f net10.0") { WorkingDirectory = parent.FolderName, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var proc = new Process { StartInfo = psi }; proc.Start(); var stdout = proc.StandardOutput.ReadToEndAsync(); var stderr = proc.StandardError.ReadToEndAsync(); await proc.WaitForExitAsync();
            Output((await stdout) + "\n" + (await stderr));
            if (proc.ExitCode == 0) LoadFolder(Path.Combine(parent.FolderName, safeName));
            else MessageBox.Show("La crÃ©ation du projet a Ã©chouÃ©. VÃ©rifiez que le SDK .NET 10 est installÃ©.", "Nouveau projet", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex) { MessageBox.Show("Impossible de lancer dotnet new : " + ex.Message); }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Ouvrir un projet .NET" };
        if (dlg.ShowDialog() == true) LoadFolder(dlg.FolderName);
    }

    private void LoadFolder(string folder)
    {
        _root = Path.GetFullPath(folder);
        ProjectTree.Items.Clear();
        var root = new TreeViewItem { Header = Path.GetFileName(_root), Tag = _root, IsExpanded = true };
        ProjectTree.Items.Add(root);
        AddDirectory(root, _root, 0);
        Output($"Projet ouvert : {_root}");
    }

    private void AddDirectory(TreeViewItem parent, string path, int depth)
    {
        if (depth > 5) return;
        try
        {
            foreach (var dir in Directory.GetDirectories(path).Where(d => !new[] { "bin", "obj", ".git", ".vs", "node_modules" }.Contains(Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)).OrderBy(d => d))
            {
                var item = new TreeViewItem { Header = "ðŸ“ " + Path.GetFileName(dir), Tag = dir };
                parent.Items.Add(item); AddDirectory(item, dir, depth + 1);
            }
            foreach (var file in Directory.GetFiles(path).Where(f => Path.GetExtension(f) is ".cs" or ".csproj" or ".sln" or ".json" or ".xml" or ".md" or ".config" or ".props" or ".targets").OrderBy(f => f))
                parent.Items.Add(new TreeViewItem { Header = "ðŸ“„ " + Path.GetFileName(file), Tag = file });
        }
        catch (UnauthorizedAccessException) { }
    }

    private void ProjectTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (ProjectTree.SelectedItem is not TreeViewItem { Tag: string path } || !File.Exists(path)) return;
        if (_dirty && MessageBox.Show("Enregistrer les modifications du fichier actuel ?", "UMLForge", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) SaveCurrent();
        _currentFile = path; _loading = true;
        try { CodeEditor.Text = File.ReadAllText(path); _dirty = false; Title = "UMLForge Studio â€” " + Path.GetFileName(path); ApplyHighlighting(path); }
        catch (Exception ex) { Output("Lecture impossible : " + ex.Message); }
        finally { _loading = false; }
    }

    private void ApplyHighlighting(string path)
    {
        var definition = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".cs" => "C#", ".xml" or ".csproj" or ".props" or ".targets" => "XML",
            ".json" => "Json", ".md" => "MarkDown", _ => null
        };
        CodeEditor.SyntaxHighlighting = definition is null ? null : HighlightingManager.Instance.GetDefinition(definition);
    }

    private void CodeEditor_TextChanged(object? sender, EventArgs e)
    {
        if (!_loading) _dirty = true;
    }

    private void CodeEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (CompletionPopup.IsOpen)
        {
            if (e.Key == Key.Escape) { CompletionPopup.IsOpen = false; e.Handled = true; return; }
            if (e.Key == Key.Down) { if (CompletionList.Items.Count > 0) CompletionList.SelectedIndex = Math.Min(CompletionList.SelectedIndex + 1, CompletionList.Items.Count - 1); e.Handled = true; return; }
            if (e.Key == Key.Up) { if (CompletionList.Items.Count > 0) CompletionList.SelectedIndex = Math.Max(0, CompletionList.SelectedIndex - 1); e.Handled = true; return; }
            if (e.Key is Key.Enter or Key.Tab) { ApplySelectedCompletion(); e.Handled = true; return; }
        }
        if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Find_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.H && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Replace_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { SaveCurrent(); e.Handled = true; }
        else if (e.Key == Key.F12) { GoToDefinition_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.Space && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (_semantic.IsLoaded && _currentFile is not null && Path.GetExtension(_currentFile).Equals(".cs", StringComparison.OrdinalIgnoreCase))
                _ = ShowSemanticCompletionsAsync();
            else
            {
                var word = GetCurrentWord();
                var suggestions = GetSuggestions(word);
                IntelliSenseHelp.Text = "SUGGESTIONS CONTEXTUELLES (Ctrl+Espace)\n\n" + string.Join(Environment.NewLine, suggestions) + "\n\nChargez d'abord la solution avec Â« Analyser solution Â» pour activer la complÃ©tion Roslyn.";
                EditorTabs.SelectedIndex = 1;
            }
            e.Handled = true;
        }
    }

    private string GetCurrentWord()
    {
        var offset = CodeEditor.CaretOffset;
        var text = CodeEditor.Text;
        var start = Math.Clamp(offset, 0, text.Length);
        while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_')) start--;
        var end = Math.Clamp(offset, 0, text.Length);
        while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
        return text[start..end];
    }

    private static string[] GetSuggestions(string prefix)
    {
        var keywords = new[] { "abstract", "async", "await", "bool", "class", "const", "decimal", "delegate", "enum", "event", "file", "int", "interface", "internal", "namespace", "new", "object", "override", "partial", "private", "protected", "public", "record", "return", "sealed", "static", "string", "Task", "this", "using", "var", "virtual", "void", "CancellationToken", "IEnumerable", "List", "Dictionary", "Console", "DateTime", "Guid", "HttpClient" };
        return keywords.Where(k => string.IsNullOrEmpty(prefix) || k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(k => k).Take(40).ToArray();
    }

    private async Task ShowSemanticCompletionsAsync()
    {
        try
        {
            if (_currentFile is null) return;
            await _semantic.UpdateDocumentTextAsync(_currentFile, CodeEditor.Text);
            var items = await _semantic.GetCompletionsAsync(_currentFile, CodeEditor.CaretOffset);
            var offset = CodeEditor.CaretOffset;
            _completionStartOffset = offset;
            while (_completionStartOffset > 0 && (char.IsLetterOrDigit(CodeEditor.Text[_completionStartOffset - 1]) || CodeEditor.Text[_completionStartOffset - 1] == '_')) _completionStartOffset--;
            var end = offset;
            while (end < CodeEditor.Text.Length && (char.IsLetterOrDigit(CodeEditor.Text[end]) || CodeEditor.Text[end] == '_')) end++;
            _completionLength = end - _completionStartOffset;
            var prefix = CodeEditor.Text[_completionStartOffset..offset];
            CompletionList.ItemsSource = items.Where(i => string.IsNullOrEmpty(prefix) || i.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (CompletionList.Items.Count == 0) CompletionList.ItemsSource = items;
            CompletionList.SelectedIndex = CompletionList.Items.Count > 0 ? 0 : -1;
            CompletionPopup.PlacementTarget = CodeEditor;
            CompletionPopup.IsOpen = CompletionList.Items.Count > 0;
            IntelliSenseHelp.Text = "INTELLISENSE ROSLYN\n\n" + (items.Count == 0 ? "Aucune proposition Ã  cette position." : $"{items.Count} symboles proposÃ©s. SÃ©lectionnez avec â†‘/â†“, EntrÃ©e pour insÃ©rer, Ã‰chap pour fermer.") + "\n\nLes suggestions proviennent du workspace sÃ©mantique chargÃ©.";
        }
        catch (Exception ex) { Output("IntelliSense Roslyn indisponible : " + ex.Message); }
    }

    private void ApplySelectedCompletion()
    {
        if (CompletionList.SelectedItem is not string selected) { CompletionPopup.IsOpen = false; return; }
        var start = Math.Clamp(_completionStartOffset, 0, CodeEditor.Text.Length);
        var length = Math.Clamp(_completionLength, 0, CodeEditor.Text.Length - start);
        CodeEditor.Document.Replace(start, length, selected);
        CodeEditor.CaretOffset = start + selected.Length;
        CompletionPopup.IsOpen = false;
        CodeEditor.Focus();
    }

    private void CompletionList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ApplySelectedCompletion();

    private async void GoToDefinition_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null || !Path.GetExtension(_currentFile).Equals(".cs", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            if (!_semantic.IsLoaded) await _semantic.OpenSolutionAsync(_root ?? Path.GetDirectoryName(_currentFile)!);
            await _semantic.UpdateDocumentTextAsync(_currentFile, CodeEditor.Text);
            var result = await _semantic.GetDefinitionAsync(_currentFile, CodeEditor.CaretOffset);
            var location = result?.Locations.FirstOrDefault();
            if (location is null) { Output("Aucune dÃ©finition source trouvÃ©e pour le symbole sous le curseur."); return; }
            OpenFileAt(location.File, location.Line, location.Column);
            Output($"DÃ©finition : {result!.Symbol} â€” {location.File}({location.Line},{location.Column})");
        }
        catch (Exception ex) { Output("Navigation vers la dÃ©finition impossible : " + ex.Message); }
    }

    private async void FindReferences_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null || !Path.GetExtension(_currentFile).Equals(".cs", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            if (!_semantic.IsLoaded) await _semantic.OpenSolutionAsync(_root ?? Path.GetDirectoryName(_currentFile)!);
            await _semantic.UpdateDocumentTextAsync(_currentFile, CodeEditor.Text);
            var refs = await _semantic.FindReferencesAsync(_currentFile, CodeEditor.CaretOffset);
            IntelliSenseHelp.Text = $"RÃ‰FÃ‰RENCES ROSLYN â€” {refs.Count} occurrence(s)\n\n" + string.Join(Environment.NewLine, refs.Select(r => $"{r.File}({r.Line},{r.Column})"));
            EditorTabs.SelectedIndex = 1;
        }
        catch (Exception ex) { Output("Recherche de rÃ©fÃ©rences impossible : " + ex.Message); }
    }

    private async void SortUsings_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null || !Path.GetExtension(_currentFile).Equals(".cs", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            if (!_semantic.IsLoaded) await _semantic.OpenSolutionAsync(_root ?? Path.GetDirectoryName(_currentFile)!);
            await _semantic.UpdateDocumentTextAsync(_currentFile, CodeEditor.Text);
            var sorted = await _semantic.SortUsingsAsync(_currentFile);
            if (sorted != CodeEditor.Text && MessageBox.Show("Appliquer le tri des directives using dans le fichier ouvert ?", "Action de code", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes) CodeEditor.Text = sorted;
        }
        catch (Exception ex) { Output("Tri des using impossible : " + ex.Message); }
    }

    private void OpenFileAt(string file, int line, int column)
    {
        if (!File.Exists(file)) return;
        var sameFile = _currentFile is not null && Path.GetFullPath(_currentFile).Equals(Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase);
        if (!sameFile)
        {
            if (_dirty)
            {
                var decision = MessageBox.Show("Enregistrer les modifications du fichier actuel avant de changer de fichier ?\n\nOui : enregistrer Â· Non : abandonner les modifications Â· Annuler : rester ici", "UMLForge", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (decision == MessageBoxResult.Cancel) return;
                if (decision == MessageBoxResult.Yes) { SaveCurrent(); if (_dirty) return; }
            }
            _currentFile = file; _loading = true;
            try { CodeEditor.Text = File.ReadAllText(file); _dirty = false; Title = "UMLForge Studio â€” " + Path.GetFileName(file); ApplyHighlighting(file); }
            finally { _loading = false; }
        }
        var safeLine = Math.Clamp(line, 1, CodeEditor.Document.LineCount);
        var lineInfo = CodeEditor.Document.GetLineByNumber(safeLine);
        var safeColumn = Math.Clamp(column, 1, lineInfo.Length + 1);
        var offset = CodeEditor.Document.GetOffset(safeLine, safeColumn);
        CodeEditor.CaretOffset = Math.Clamp(offset, 0, CodeEditor.Text.Length); CodeEditor.Select(CodeEditor.CaretOffset, 0); CodeEditor.ScrollToLine(Math.Clamp(line, 1, CodeEditor.Document.LineCount)); CodeEditor.Focus();
    }

    private async void AnalyzeSolution_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_root) || !Directory.Exists(_root)) { MessageBox.Show("Ouvrez un dossier .NET avant l'analyse."); return; }
        try
        {
            SaveCurrentIfInsideProject();
            Output("Chargement du workspace Roslyn et analyse de la solutionâ€¦");
            await _semantic.OpenSolutionAsync(_root);
            var diagnostics = await _semantic.GetDiagnosticsAsync();
            var errors = diagnostics.Count(d => d.Severity == "Error");
            var warnings = diagnostics.Count(d => d.Severity == "Warning");
            Output($"Analyse Roslyn terminÃ©e : {errors} erreur(s), {warnings} avertissement(s), {diagnostics.Count} diagnostic(s).\n" + string.Join(Environment.NewLine, diagnostics.Take(250)));
            IntelliSenseHelp.Text = "ANALYSE SÃ‰MANTIQUE DE SOLUTION\n\n" + $"Solution : {_semantic.SolutionPath}\nProjets : {_semantic.Solution?.Projects.Count() ?? 0}\nDiagnostics : {errors} erreur(s), {warnings} avertissement(s).\n\n" + string.Join(Environment.NewLine, diagnostics.Take(150));
            EditorTabs.SelectedIndex = 1;
        }
        catch (Exception ex) { Output("Chargement/analyse Roslyn impossible : " + ex.Message + "\nVÃ©rifiez le SDK, les rÃ©fÃ©rences de projet et les workloads nÃ©cessaires."); }
    }

    private async void SemanticRename_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null || !Path.GetExtension(_currentFile).Equals(".cs", StringComparison.OrdinalIgnoreCase))
        { MessageBox.Show("Ouvrez un fichier C# et placez le curseur sur le symbole Ã  renommer."); return; }
        if (string.IsNullOrWhiteSpace(_root)) { MessageBox.Show("Ouvrez d'abord la solution .NET."); return; }
        try
        {
            SaveCurrentIfInsideProject();
            if (!_semantic.IsLoaded) await _semantic.OpenSolutionAsync(_root);
            await _semantic.UpdateDocumentTextAsync(_currentFile, CodeEditor.Text);
            var newName = PromptDialog.Ask(this, "Renommage Roslyn", "Nouveau nom du symbole sous le curseur :");
            if (string.IsNullOrWhiteSpace(newName)) return;
            var result = await _semantic.RenameSymbolAsync(_currentFile, CodeEditor.CaretOffset, newName);
            if (result.ChangedContents.Count == 0) { Output("Roslyn n'a trouvÃ© aucun changement Ã  appliquer."); return; }
            var listing = string.Join(Environment.NewLine, result.ChangedFiles);
            var answer = MessageBox.Show($"Symbole : {result.OldSymbol} â†’ {result.NewSymbol}\nFichiers impactÃ©s : {result.ChangedFiles.Count}\n\n{listing}\n\nCrÃ©er une sauvegarde puis Ã©crire les changements dans tous ces fichiers ?", "Renommage sÃ©mantique multi-fichiers", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                await _semantic.OpenSolutionAsync(_root);
                Output("Renommage annulÃ© : aucun fichier n'a Ã©tÃ© Ã©crit."); return;
            }
            var backupRoot = Path.Combine(_root, ".umlforge", "semantic-renames", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            var prefix = Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var prepared = result.ChangedContents.Select(pair =>
            {
                var full = Path.GetFullPath(pair.Key);
                if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("Roslyn a proposÃ© une modification hors du dossier ouvert : " + full);
                return (FullPath: full, Content: pair.Value, Relative: Path.GetRelativePath(_root, full), Original: File.Exists(full) ? File.ReadAllText(full) : null, Temp: full + ".umlforge-rename.tmp");
            }).ToArray();
            foreach (var item in prepared)
            {
                if (item.Original is not null)
                {
                    var backup = Path.Combine(backupRoot, item.Relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    await File.WriteAllTextAsync(backup, item.Original, new UTF8Encoding(false));
                }
                Directory.CreateDirectory(Path.GetDirectoryName(item.FullPath)!);
                await File.WriteAllTextAsync(item.Temp, item.Content, new UTF8Encoding(false));
            }
            try
            {
                foreach (var item in prepared) File.Move(item.Temp, item.FullPath, true);
            }
            catch
            {
                foreach (var item in prepared.Reverse())
                {
                    try
                    {
                        if (item.Original is null) { if (File.Exists(item.FullPath)) File.Delete(item.FullPath); }
                        else { var backup = Path.Combine(backupRoot, item.Relative); if (File.Exists(backup)) File.Copy(backup, item.FullPath, true); }
                    }
                    catch { }
                    try { if (File.Exists(item.Temp)) File.Delete(item.Temp); } catch { }
                }
                throw;
            }
            if (_currentFile is not null && result.ChangedContents.TryGetValue(_currentFile, out var currentText))
            { _loading = true; CodeEditor.Text = currentText; _loading = false; _dirty = false; }
            LoadFolder(_root);
            Output($"Renommage Roslyn appliquÃ© Ã  {result.ChangedFiles.Count} fichier(s). Sauvegardes : {backupRoot}");
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrWhiteSpace(_root) && Directory.Exists(_root)) try { await _semantic.OpenSolutionAsync(_root); } catch { }
            MessageBox.Show("Le renommage sÃ©mantique n'a pas Ã©tÃ© appliquÃ© :\n" + ex.Message, "Roslyn", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenDebugger_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_root)) { MessageBox.Show("Ouvrez un projet .NET avant de dÃ©marrer le dÃ©bogueur."); return; }
        var window = new GraphicalDebuggerWindow(_root) { Owner = this };
        window.ShowDialog();
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        var query = PromptDialog.Ask(this, "Rechercher", "Texte Ã  rechercher :");
        if (string.IsNullOrEmpty(query)) return;
        var start = Math.Clamp(CodeEditor.CaretOffset, 0, CodeEditor.Text.Length);
        var index = CodeEditor.Text.IndexOf(query, start, StringComparison.OrdinalIgnoreCase);
        if (index < 0) index = CodeEditor.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0) { Output("Texte introuvable : " + query); return; }
        CodeEditor.Select(index, query.Length); CodeEditor.Focus();
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        var query = PromptDialog.Ask(this, "Remplacer", "Texte Ã  rechercher :");
        if (string.IsNullOrEmpty(query)) return;
        var replacement = PromptDialog.Ask(this, "Remplacer", "Remplacer par :") ?? string.Empty;
        var count = System.Text.RegularExpressions.Regex.Matches(CodeEditor.Text, System.Text.RegularExpressions.Regex.Escape(query), System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
        if (count == 0) { Output("Aucune occurrence trouvÃ©e."); return; }
        CodeEditor.Text = System.Text.RegularExpressions.Regex.Replace(CodeEditor.Text, System.Text.RegularExpressions.Regex.Escape(query), _ => replacement, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Output($"{count} occurrence(s) remplacÃ©e(s). VÃ©rifiez les changements avant d'enregistrer.");
    }

    private void RenameIdentifier_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null || !Path.GetExtension(_currentFile).Equals(".cs", StringComparison.OrdinalIgnoreCase))
        { MessageBox.Show("Ouvrez un fichier C# avant de renommer un identifiant."); return; }
        var oldName = PromptDialog.Ask(this, "Renommer l'identifiant", "Identifiant actuel (nom exact) :");
        if (string.IsNullOrWhiteSpace(oldName)) return;
        var newName = PromptDialog.Ask(this, "Renommer l'identifiant", "Nouvel identifiant C# :");
        if (string.IsNullOrWhiteSpace(newName) || !SyntaxFacts.IsValidIdentifier(newName))
        { MessageBox.Show("Le nouvel identifiant n'est pas un identifiant C# valide."); return; }
        try
        {
            var root = CSharpSyntaxTree.ParseText(CodeEditor.Text).GetRoot();
            var tokens = root.DescendantTokens().Where(t => t.IsKind(SyntaxKind.IdentifierToken) && t.ValueText == oldName).ToArray();
            if (tokens.Length == 0) { Output("Identifiant introuvable : " + oldName); return; }
            var renamed = root.ReplaceTokens(tokens, (original, _) => SyntaxFactory.Identifier(original.LeadingTrivia, newName, original.TrailingTrivia));
            CodeEditor.Text = renamed.ToFullString();
            Output($"{tokens.Length} occurrence(s) textuelle(s) renommÃ©e(s) dans le fichier ouvert. VÃ©rifiez chaque occurrence : cette commande n'est pas une refactorisation sÃ©mantique multi-projets.");
        }
        catch (Exception ex) { MessageBox.Show("Renommage impossible : " + ex.Message); }
    }

    private void Format_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null || Path.GetExtension(_currentFile).Equals(".cs", StringComparison.OrdinalIgnoreCase) == false)
        { MessageBox.Show("Le formatage Roslyn est disponible pour les fichiers C#."); return; }
        try
        {
            var root = CSharpSyntaxTree.ParseText(CodeEditor.Text).GetRoot();
            CodeEditor.Text = root.NormalizeWhitespace().ToFullString() + Environment.NewLine;
            Output("Formatage syntaxique Roslyn appliquÃ©. VÃ©rifiez le diff avant de valider.");
        }
        catch (Exception ex) { Output("Formatage impossible : " + ex.Message); }
    }

    private void SyncPreview_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_root) || !Directory.Exists(_root))
        { MessageBox.Show("Ouvrez d'abord le dossier du projet cible."); return; }
        var dialog = new OpenFolderDialog { Title = "Choisir le dossier contenant le code C# gÃ©nÃ©rÃ© depuis UMLForge" };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var generatedRoot = Path.GetFullPath(dialog.FolderName);
            var generated = Directory.GetFiles(generatedRoot, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".git" or ".vs" or ".umlforge"))
                .ToDictionary(f => Path.GetRelativePath(generatedRoot, f).Replace('\\', '/'), File.ReadAllText, StringComparer.OrdinalIgnoreCase);
            var baselinePath = Path.Combine(_root, ".umlforge", "baseline.json");
            IReadOnlyDictionary<string, string> baseline = File.Exists(baselinePath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(baselinePath)) ?? new Dictionary<string, string>()
                : new Dictionary<string, string>();
            var plan = SynchronizationEngine.CreatePlan(_root, generated, baseline);
            if (plan.Files.Count == 0) { MessageBox.Show("Aucun fichier C# trouvÃ© dans le dossier gÃ©nÃ©rÃ©."); return; }
            var review = new SynchronizationReviewWindow(plan) { Owner = this };
            if (review.ShowDialog() == true) Output("Synchronisation terminÃ©e. Les sauvegardes sont disponibles dans .umlforge/history.");
        }
        catch (Exception ex) { MessageBox.Show("Impossible de prÃ©parer la synchronisation :\n" + ex.Message, "UMLForge", MessageBoxButton.OK, MessageBoxImage.Error); }
    }



    private async void MapUml_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_root) || !Directory.Exists(_root))
        {
            MessageBox.Show("Ouvrez d'abord le dossier d'une solution .NET.", "Mapping UML â†” C#", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = new OpenFileDialog { Title = "Choisir le modÃ¨le UMLForge Ã  relier au code", Filter = "ModÃ¨le UMLForge (*.umlforge.json)|*.umlforge.json|JSON (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog() != true) return;
        try
        {
            if (!_semantic.IsLoaded) await _semantic.OpenSolutionAsync(_root);
            var model = JsonSerializer.Deserialize<UmlModel>(File.ReadAllText(dialog.FileName)) ?? throw new InvalidDataException("Le fichier ne contient pas un modÃ¨le UMLForge valide.");
            var report = await UmlCodeMappingService.RefreshMappingsAsync(model, _semantic.Solution!);
            var message = $"Mapping UML â†” C# terminÃ©.\nNouvelles associations : {report.Added}\nAssociations actualisÃ©es : {report.Updated}\nÃ‰lÃ©ments reliÃ©s : {report.MappedTotal}\nÃ‰lÃ©ments non rÃ©solus/ambigus : {report.Unresolved}\nCorrespondances ambiguÃ«s : {report.Ambiguous}\nRenommages reconnus par forme : {report.Renamed}\nPropriÃ©tÃ©s examinÃ©es : {report.PropertiesConsidered}\nOpÃ©rations examinÃ©es : {report.OperationsConsidered}\nInterfaces examinÃ©es : {report.InterfacesConsidered}\nÃ‰numÃ©rations/littÃ©raux examinÃ©s : {report.EnumerationsAndLiteralsConsidered}.\n\nEnregistrer ces associations dans le modÃ¨le UML ?\nLes correspondances ambiguÃ«s ne sont jamais choisies automatiquement.";
            if (MessageBox.Show(message, "Mapping UML â†” C#", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            var backup = dialog.FileName + ".bak-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Copy(dialog.FileName, backup, false);
            var temp = dialog.FileName + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            File.Move(temp, dialog.FileName, true);
            Output($"Mapping UML â†” C# sauvegardÃ©. Ã‰lÃ©ments reliÃ©s : {report.MappedTotal}; non rÃ©solus/ambigus : {report.Unresolved}; renommages reconnus : {report.Renamed}. Sauvegarde : {Path.GetFileName(backup)}");
        }
        catch (Exception ex) { MessageBox.Show("Le mapping UML â†” C# a Ã©chouÃ© :\n" + ex.Message, "Mapping UML â†” C#", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void SignatureHelp_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null || !Path.GetExtension(_currentFile).Equals(".cs", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            await EnsureSemanticWorkspaceAsync();
            var signatures = await _semantic.GetSignatureHelpAsync(_currentFile, CodeEditor.CaretOffset);
            IntelliSenseHelp.Text = signatures.Count == 0
                ? "SIGNATURE HELP\n\nAucune surcharge dÃ©tectÃ©e Ã  la position du curseur. Placez le curseur dans les parenthÃ¨ses d'un appel ou d'un constructeur."
                : "SIGNATURE HELP â€” " + signatures.Count + " surcharge(s)\n\n" + string.Join("\n\n", signatures.Select((s, i) => $"{i + 1}. {s.Signature}\n    " + string.Join("\n    ", s.Parameters)));
            EditorTabs.SelectedIndex = 1;
        }
        catch (Exception ex) { Output("Signature help indisponible : " + ex.Message); }
    }

    private async void AutoImports_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null || !Path.GetExtension(_currentFile).Equals(".cs", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            await EnsureSemanticWorkspaceAsync();
            var candidates = await _semantic.GetAutomaticUsingCandidatesAsync(_currentFile);
            if (candidates.Count == 0) { Output("Aucun import automatique proposÃ©. VÃ©rifiez les erreurs CS0246 et les rÃ©fÃ©rences du projet."); return; }
            var chosen = ChoiceDialog.Choose(this, "Imports automatiques", "Choisissez l'espace de noms Ã  importer :", candidates);
            if (chosen is null) return;
            var updated = await _semantic.AddUsingAsync(_currentFile, chosen);
            CodeEditor.Text = updated; _dirty = true; SaveCurrent();
            Output("Directive using ajoutÃ©e : " + chosen);
        }
        catch (Exception ex) { Output("Imports automatiques impossibles : " + ex.Message); }
    }

    private async void CodeActions_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null || !Path.GetExtension(_currentFile).Equals(".cs", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            await EnsureSemanticWorkspaceAsync();
            var actions = await _semantic.GetCodeActionSuggestionsAsync(_currentFile, CodeEditor.CaretOffset);
            if (actions.Count == 0) { Output("Aucune action de code disponible."); return; }
            var chosen = ChoiceDialog.Choose(this, "Actions de code Roslyn", "SÃ©lectionnez une action Ã  appliquer au fichier courant :", actions.Select(a => a.Title + "  â€”  " + a.Description).ToArray());
            if (chosen is null) return;
            var action = actions.First(a => (a.Title + "  â€”  " + a.Description) == chosen);
            if (MessageBox.Show("Appliquer cette action au fichier ouvert ?\n\n" + action.Title, "Confirmer l'action", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            var updated = await _semantic.ApplyCodeActionAsync(_currentFile, action.Id);
            CodeEditor.Text = updated; _dirty = true;
            Output("Action appliquÃ©e : " + action.Title + ". Enregistrez ou vÃ©rifiez le diff avant de poursuivre.");
        }
        catch (Exception ex) { Output("Action de code impossible : " + ex.Message); }
    }

    private async void AnalyzeFile_Click(object sender, RoutedEventArgs e)
    {
        if (_currentFile is null || !Path.GetExtension(_currentFile).Equals(".cs", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            await EnsureSemanticWorkspaceAsync();
            var all = await _semantic.GetDiagnosticsAsync();
            var full = Path.GetFullPath(_currentFile);
            var diagnostics = all.Where(d => !string.IsNullOrWhiteSpace(d.File) && Path.GetFullPath(d.File).Equals(full, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Line).ThenBy(d => d.Column).ToArray();
            FileDiagnosticsList.ItemsSource = diagnostics;
            Output($"Diagnostics du fichier : {diagnostics.Length} erreur(s)/avertissement(s). SÃ©lectionnez une ligne dans le panneau Diagnostics du fichier pour naviguer.");
        }
        catch (Exception ex) { Output("Analyse du fichier impossible : " + ex.Message); }
    }

    private void FileDiagnosticsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FileDiagnosticsList.SelectedItem is not SemanticDiagnostic diagnostic || string.IsNullOrWhiteSpace(diagnostic.File) || diagnostic.Line < 1) return;
        OpenFileAt(diagnostic.File, diagnostic.Line, Math.Max(1, diagnostic.Column));
    }

    private async Task EnsureSemanticWorkspaceAsync()
    {
        if (_currentFile is null) throw new InvalidOperationException("Aucun fichier ouvert.");
        if (!_semantic.IsLoaded) await _semantic.OpenSolutionAsync(_root ?? Path.GetDirectoryName(_currentFile)!);
        await _semantic.UpdateDocumentTextAsync(_currentFile, CodeEditor.Text);
    }

    private void SaveFile_Click(object sender, RoutedEventArgs e) => SaveCurrent();
    private void SaveCurrent()
    {
        if (_currentFile == null)
        {
            var dlg = new SaveFileDialog { Filter = "C# (*.cs)|*.cs|Tous les fichiers (*.*)|*.*", FileName = "Program.cs" };
            if (dlg.ShowDialog() != true) return; _currentFile = dlg.FileName;
        }
        try { File.WriteAllText(_currentFile, CodeEditor.Text, new UTF8Encoding(false)); _dirty = false; Output("EnregistrÃ© : " + _currentFile); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Enregistrement", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void Restore_Click(object sender, RoutedEventArgs e) => await RunDotNet("restore");
    private async void Build_Click(object sender, RoutedEventArgs e) => await RunDotNet("build --nologo");
    private async void Test_Click(object sender, RoutedEventArgs e) => await RunDotNet("test --nologo");
    private async void Run_Click(object sender, RoutedEventArgs e) => await RunDotNet("run");

    private async Task RunDotNet(string args)
    {
        if (string.IsNullOrWhiteSpace(_root)) { MessageBox.Show("Ouvrez d'abord le dossier d'un projet .NET."); return; }
        SaveCurrentIfInsideProject(); Output($"> dotnet {args}\nExÃ©cution en coursâ€¦");
        try
        {
            var psi = new ProcessStartInfo("dotnet", args) { WorkingDirectory = _root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            psi.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            using var process = new Process { StartInfo = psi };
            process.Start(); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
            Output($"dotnet {args} â€” code retour {process.ExitCode}\n{await stdout}\n{await stderr}");
        }
        catch (Exception ex) { Output("Impossible de lancer le SDK .NET. Installez .NET 10 SDK et vÃ©rifiez le PATH.\n" + ex.Message); }
    }

    private void SaveCurrentIfInsideProject()
    {
        if (_dirty && _currentFile != null && _root != null && _currentFile.StartsWith(_root, StringComparison.OrdinalIgnoreCase)) SaveCurrent();
    }

    private async void AddPackage_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_root)) { MessageBox.Show("Ouvrez d'abord un projet .NET."); return; }
        var dialog = new PackageDialog(); dialog.Owner = this;
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.PackageName)) return;
        var version = string.IsNullOrWhiteSpace(dialog.Version) ? "" : " --version " + dialog.Version.Trim();
        await RunDotNet("add package " + QuoteArg(dialog.PackageName.Trim()) + version);
        LoadFolder(_root);
    }

    private static string QuoteArg(string s) => s.Any(char.IsWhiteSpace) ? "\"" + s.Replace("\"", "") + "\"" : s;
    private void Output(string text) { OutputBox.Text = $"[{DateTime.Now:HH:mm:ss}] {text}\n\n" + OutputBox.Text; }
}

public sealed class PromptDialog : Window
{
    private readonly TextBox _value = new() { Margin = new Thickness(0, 8, 0, 14), Padding = new Thickness(8), MinWidth = 320 };
    public string Value => _value.Text;
    private PromptDialog(Window owner, string title, string label)
    {
        Owner = owner; Title = title; Width = 430; Height = 170; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(16) }; panel.Children.Add(new TextBlock { Text = label }); panel.Children.Add(_value);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "OK", IsDefault = true, Padding = new Thickness(16, 5, 16, 5), Margin = new Thickness(4) }; ok.Click += (_, _) => { DialogResult = true; Close(); };
        var cancel = new Button { Content = "Annuler", IsCancel = true, Padding = new Thickness(16, 5, 16, 5), Margin = new Thickness(4) }; buttons.Children.Add(ok); buttons.Children.Add(cancel); panel.Children.Add(buttons); Content = panel;
    }
    public static string? Ask(Window owner, string title, string label) { var dialog = new PromptDialog(owner, title, label); return dialog.ShowDialog() == true ? dialog.Value : null; }
}

public sealed class PackageDialog : Window
{
    private readonly TextBox _name = new() { Margin = new Thickness(0, 4, 0, 12), Padding = new Thickness(7) };
    private readonly TextBox _version = new() { Margin = new Thickness(0, 4, 0, 12), Padding = new Thickness(7) };
    public string PackageName => _name.Text;
    public string Version => _version.Text;
    public PackageDialog()
    {
        Title = "Ajouter un package NuGet"; Width = 390; Height = 245; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; Background = System.Windows.Media.Brushes.White;
        var panel = new StackPanel { Margin = new Thickness(18) }; panel.Children.Add(new TextBlock { Text = "Nom du package NuGet" }); panel.Children.Add(_name); panel.Children.Add(new TextBlock { Text = "Version (facultatif)" }); panel.Children.Add(_version);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "Ajouter", IsDefault = true, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(4) }; ok.Click += (_, _) => { DialogResult = true; Close(); };
        var cancel = new Button { Content = "Annuler", IsCancel = true, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(4) }; buttons.Children.Add(ok); buttons.Children.Add(cancel); panel.Children.Add(buttons); Content = panel;
    }
}

public sealed class NewProjectDialog : Window
{
    private readonly TextBox _name = new() { Text = "MonProjet", Margin = new Thickness(0, 4, 0, 12), Padding = new Thickness(7) };
    private readonly ComboBox _template = new() { Margin = new Thickness(0, 4, 0, 12), Padding = new Thickness(5), ItemsSource = new[] { "Application console", "BibliothÃ¨que de classes", "Application web ASP.NET Core", "Application de tests xUnit" }, SelectedIndex = 0 };
    public string ProjectName => _name.Text;
    public string Template => _template.SelectedItem as string ?? "Application console";
    public NewProjectDialog()
    {
        Title = "CrÃ©er un projet .NET"; Width = 410; Height = 250; WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize; Background = System.Windows.Media.Brushes.White;
        var panel = new StackPanel { Margin = new Thickness(18) }; panel.Children.Add(new TextBlock { Text = "Nom du projet" }); panel.Children.Add(_name); panel.Children.Add(new TextBlock { Text = "ModÃ¨le" }); panel.Children.Add(_template);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "CrÃ©er", IsDefault = true, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(4) }; ok.Click += (_, _) => { DialogResult = true; Close(); };
        var cancel = new Button { Content = "Annuler", IsCancel = true, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(4) }; buttons.Children.Add(ok); buttons.Children.Add(cancel); panel.Children.Add(buttons); Content = panel;
    }
}


public sealed class ChoiceDialog : Window
{
    private readonly ListBox _list = new() { Margin = new Thickness(0, 10, 0, 12), MinWidth = 520, MinHeight = 220, MaxHeight = 420 };
    private ChoiceDialog(Window owner, string title, string prompt, IReadOnlyList<string> choices)
    {
        Owner = owner; Title = title; Width = 680; Height = 420; MinWidth = 560; MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(11, 18, 32));
        _list.ItemsSource = choices; _list.SelectedIndex = choices.Count > 0 ? 0 : -1;
        _list.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(17, 28, 49));
        _list.Foreground = System.Windows.Media.Brushes.White;
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = prompt, Foreground = System.Windows.Media.Brushes.White, TextWrapping = TextWrapping.Wrap }); panel.Children.Add(_list);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "Choisir", IsDefault = true, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(4) };
        ok.Click += (_, _) => { if (_list.SelectedItem is not null) DialogResult = true; };
        var cancel = new Button { Content = "Annuler", IsCancel = true, Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(4) };
        buttons.Children.Add(ok); buttons.Children.Add(cancel); panel.Children.Add(buttons); Content = panel;
        _list.MouseDoubleClick += (_, _) => { if (_list.SelectedItem is not null) DialogResult = true; };
    }
    public static string? Choose(Window owner, string title, string prompt, IReadOnlyList<string> choices)
    {
        var dialog = new ChoiceDialog(owner, title, prompt, choices);
        return dialog.ShowDialog() == true ? dialog._list.SelectedItem as string : null;
    }
}
