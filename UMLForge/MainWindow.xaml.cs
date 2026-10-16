using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using UMLForge.Models;
using UMLForge.Services;

namespace UMLForge;

public partial class MainWindow : Window
{
    private UmlModel _model = new();
    private UmlClass? _selected;
    private readonly Dictionary<Guid, Border> _nodes = new();
    private Point _dragOffset;
    private bool _dragging;
    private bool _updatingInspector;
    private Guid? _relationStart;

    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public MainWindow()
    {
        InitializeComponent();
        _model.Classes.Add(new UmlClass { Name = "Utilisateur", X = 80, Y = 90, Attributes = new() { "Id: Guid", "Nom: string", "Email: string" }, Methods = new() { "SeConnecter(): bool", "ModifierProfil(): void" } });
        _model.Classes.Add(new UmlClass { Name = "Commande", X = 390, Y = 220, Attributes = new() { "Id: Guid", "Date: DateTime", "Total: decimal" }, Methods = new() { "CalculerTotal(): decimal" } });
        ProjectNameBox.Text = _model.Name;
        RenderModel();
        SelectClass(_model.Classes.FirstOrDefault());
    }

    private void AddClass_Click(object sender, RoutedEventArgs e)
    {
        var cls = new UmlClass { Name = "NouvelleClasse", X = 60 + (_model.Classes.Count * 28) % 500, Y = 60 + (_model.Classes.Count * 28) % 350 };
        _model.Classes.Add(cls);
        RenderModel();
        SelectClass(cls);
        StatusText.Text = "Classe ajoutée.";
    }

    private void AddRelation_Click(object sender, RoutedEventArgs e)
    {
        if (_model.Classes.Count < 2) { StatusText.Text = "Ajoutez au moins deux classes."; return; }
        _relationStart = null;
        StatusText.Text = "Mode association : cliquez sur la classe source, puis sur la classe cible.";
        _relationStart = Guid.Empty;
    }

    private void RenderModel()
    {
        DiagramCanvas.Children.Clear();
        _nodes.Clear();
        foreach (var relation in _model.Relations)
        {
            var from = _model.Classes.FirstOrDefault(c => c.Id == relation.FromId);
            var to = _model.Classes.FirstOrDefault(c => c.Id == relation.ToId);
            if (from == null || to == null) continue;
            var line = new Line { X1 = from.X + 105, Y1 = from.Y + 45, X2 = to.X + 105, Y2 = to.Y + 45, Stroke = new SolidColorBrush(Color.FromRgb(37, 99, 235)), StrokeThickness = 2 };
            DiagramCanvas.Children.Add(line);
            var label = new TextBlock { Text = relation.Kind, Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)), FontSize = 10, Background = Brushes.White };
            Canvas.SetLeft(label, (line.X1 + line.X2) / 2);
            Canvas.SetTop(label, (line.Y1 + line.Y2) / 2);
            DiagramCanvas.Children.Add(label);
        }
        foreach (var cls in _model.Classes)
        {
            var panel = new StackPanel();
            panel.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)), Padding = new Thickness(8, 6, 8, 6), Child = new TextBlock { Text = cls.Name, Foreground = Brushes.White, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center } });
            var attrs = new TextBlock { Text = string.Join("\n", cls.Attributes.Select(a => "+ " + a)), Padding = new Thickness(8), Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)), FontSize = 11 };
            panel.Children.Add(attrs);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromRgb(216, 225, 239)) });
            panel.Children.Add(new TextBlock { Text = string.Join("\n", cls.Methods.Select(m => "+ " + m)), Padding = new Thickness(8), Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), FontSize = 11 });
            var border = new Border { Width = 210, MinHeight = 95, Background = Brushes.White, BorderBrush = cls.Id == _selected?.Id ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) : new SolidColorBrush(Color.FromRgb(148, 163, 184)), BorderThickness = new Thickness(cls.Id == _selected?.Id ? 3 : 1), CornerRadius = new CornerRadius(5), Child = panel, Tag = cls.Id, Cursor = Cursors.SizeAll, Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = .12 } };
            border.MouseLeftButtonDown += Node_MouseLeftButtonDown;
            border.MouseMove += Node_MouseMove;
            border.MouseLeftButtonUp += Node_MouseLeftButtonUp;
            Canvas.SetLeft(border, cls.X);
            Canvas.SetTop(border, cls.Y);
            DiagramCanvas.Children.Add(border);
            _nodes[cls.Id] = border;
        }
    }

    private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not Guid id) return;
        var cls = _model.Classes.FirstOrDefault(c => c.Id == id);
        if (cls == null) return;
        if (_relationStart == Guid.Empty) { _relationStart = id; StatusText.Text = $"Source : {cls.Name}. Cliquez maintenant sur la classe cible."; e.Handled = true; return; }
        if (_relationStart.HasValue && _relationStart.Value != Guid.Empty && _relationStart.Value != id)
        {
            _model.Relations.Add(new UmlRelation { FromId = _relationStart.Value, ToId = id });
            _relationStart = null;
            RenderModel();
            StatusText.Text = "Association créée.";
            e.Handled = true;
            return;
        }
        SelectClass(cls);
        _dragging = true;
        _dragOffset = e.GetPosition(DiagramCanvas);
        _dragOffset.X -= cls.X;
        _dragOffset.Y -= cls.Y;
        border.CaptureMouse();
        e.Handled = true;
    }

    private void Node_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed || sender is not Border border || border.Tag is not Guid id) return;
        var p = e.GetPosition(DiagramCanvas);
        var cls = _model.Classes.FirstOrDefault(c => c.Id == id);
        if (cls == null) return;
        cls.X = Math.Max(0, p.X - _dragOffset.X);
        cls.Y = Math.Max(0, p.Y - _dragOffset.Y);
        Canvas.SetLeft(border, cls.X);
        Canvas.SetTop(border, cls.Y);
    }

    private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        if (sender is Border border) border.ReleaseMouseCapture();
        RenderModel();
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == DiagramCanvas) { _relationStart = null; SelectClass(null); }
    }

    private void SelectClass(UmlClass? cls)
    {
        _selected = cls;
        _updatingInspector = true;
        ClassNameBox.Text = cls?.Name ?? "";
        NamespaceBox.Text = cls?.Namespace ?? "";
        AttributesBox.Text = cls == null ? "" : string.Join(Environment.NewLine, cls.Attributes);
        MethodsBox.Text = cls == null ? "" : string.Join(Environment.NewLine, cls.Methods);
        ClassNameBox.IsEnabled = NamespaceBox.IsEnabled = AttributesBox.IsEnabled = MethodsBox.IsEnabled = cls != null;
        _updatingInspector = false;
        RenderModel();
    }

    private void ClassFields_Changed(object sender, TextChangedEventArgs e)
    {
        if (_updatingInspector || _selected == null) return;
        // Keep the canvas responsive; explicit Apply commits the parsed editor fields.
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) { StatusText.Text = "Sélectionnez une classe."; return; }
        _selected.Name = string.IsNullOrWhiteSpace(ClassNameBox.Text) ? "NouvelleClasse" : ClassNameBox.Text.Trim();
        _selected.Namespace = string.IsNullOrWhiteSpace(NamespaceBox.Text) ? "Generated" : NamespaceBox.Text.Trim();
        _selected.Attributes = AttributesBox.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        _selected.Methods = MethodsBox.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        _model.Name = ProjectNameBox.Text.Trim();
        RenderModel();
        StatusText.Text = "Modèle mis à jour.";
    }

    private void DeleteClass_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var id = _selected.Id;
        _model.Classes.Remove(_selected);
        for (int i = _model.Relations.Count - 1; i >= 0; i--)
            if (_model.Relations[i].FromId == id || _model.Relations[i].ToId == id) _model.Relations.RemoveAt(i);
        SelectClass(_model.Classes.FirstOrDefault());
        StatusText.Text = "Classe supprimée.";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ApplyCurrent();
        var dlg = new SaveFileDialog { Filter = "Modèle UMLForge (*.umlforge.json)|*.umlforge.json|JSON (*.json)|*.json", FileName = "Projet.umlforge.json" };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, JsonSerializer.Serialize(_model, _jsonOptions));
        StatusText.Text = "Projet enregistré : " + dlg.FileName;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Modèle UMLForge (*.umlforge.json;*.json)|*.umlforge.json;*.json|Tous les fichiers (*.*)|*.*" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<UmlModel>(File.ReadAllText(dlg.FileName));
            if (loaded == null) throw new InvalidDataException("Le fichier ne contient pas de modèle.");
            _model = loaded;
            ProjectNameBox.Text = _model.Name;
            SelectClass(_model.Classes.FirstOrDefault());
            RenderModel();
            StatusText.Text = "Projet ouvert.";
        }
        catch (Exception ex) { MessageBox.Show("Impossible d'ouvrir ce modèle : " + ex.Message, "UMLForge", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        ApplyCurrent();
        var dlg = new OpenFolderDialog { Title = "Choisir le dossier de génération du code C#" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            CodeGenerator.Generate(_model, dlg.FolderName);
            StatusText.Text = "Code C# généré/synchronisé dans : " + dlg.FolderName;
            MessageBox.Show("Génération / synchronisation terminée.\n\nLes corps de méthodes existants reconnus sont préservés.\nLe projet cible .NET 10 et C# 14. Lancez Compiler pour vérifier le résultat.", "UMLForge", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show("Échec de génération : " + ex.Message, "UMLForge", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ImportCSharp_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Fichiers C# (*.cs)|*.cs", Multiselect = true, Title = "Importer des fichiers C# existants" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var imported = CSharpImportService.ImportFiles(dlg.FileNames, System.IO.Path.GetFileNameWithoutExtension(dlg.FileNames[0]));
            if (imported.Classes.Count == 0) { MessageBox.Show("Aucune classe ou interface trouvée dans les fichiers sélectionnés.", "Import C#"); return; }
            _model = imported;
            ProjectNameBox.Text = _model.Name;
            SelectClass(_model.Classes.FirstOrDefault());
            RenderModel();
            StatusText.Text = $"Import Roslyn terminé : {_model.Classes.Count} type(s). Vérifiez le modèle avant synchronisation.";
        }
        catch (Exception ex) { MessageBox.Show("Échec de l'import C# : " + ex.Message, "UMLForge", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ExportPlantUml_Click(object sender, RoutedEventArgs e)
    {
        ApplyCurrent();
        var dlg = new SaveFileDialog { Filter = "PlantUML (*.puml)|*.puml|Texte (*.txt)|*.txt", FileName = "Diagramme.puml" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, PlantUmlExporter.ExportClassDiagram(_model), new System.Text.UTF8Encoding(false));
            StatusText.Text = "Export PlantUML terminé : " + dlg.FileName;
        }
        catch (Exception ex) { MessageBox.Show("Échec de l'export PlantUML : " + ex.Message, "UMLForge", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void Build_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Sélectionner le dossier contenant le fichier .csproj" };
        if (dlg.ShowDialog() != true) return;
        StatusText.Text = "Compilation .NET en cours…";
        try
        {
            var result = await DotNetCliService.RunAsync(dlg.FolderName, "build --nologo");
            var output = result.Output.Trim();
            StatusText.Text = $"Compilation {(result.Success ? "réussie" : "échouée")} ({result.Duration.TotalSeconds:F1}s).";
            MessageBox.Show(string.IsNullOrWhiteSpace(output) ? "Aucune sortie." : output, result.Success ? "Compilation réussie" : "Erreurs de compilation", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex) { MessageBox.Show("Impossible de lancer dotnet. Installez le SDK .NET 10 et vérifiez le PATH.\n\n" + ex.Message, "UMLForge", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void OpenStudio_Click(object sender, RoutedEventArgs e)
    {
        var win = new CodeWorkspaceWindow(_model.LastOpenedProjectPath) { Owner = this };
        win.Show();
    }

    private void ManageDiagrams_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DiagramManagerWindow(_model) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            StatusText.Text = $"Diagrammes UML : {_model.Diagrams.Count}. Dernière modification : {dialog.LastAction}";
        }
    }

    private void ApplyCurrent()
    {
        if (_selected != null)
        {
            _selected.Name = string.IsNullOrWhiteSpace(ClassNameBox.Text) ? "NouvelleClasse" : ClassNameBox.Text.Trim();
            _selected.Namespace = string.IsNullOrWhiteSpace(NamespaceBox.Text) ? "Generated" : NamespaceBox.Text.Trim();
            _selected.Attributes = AttributesBox.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            _selected.Methods = MethodsBox.Text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
        _model.Name = string.IsNullOrWhiteSpace(ProjectNameBox.Text) ? "NouveauProjet" : ProjectNameBox.Text.Trim();
    }
}
