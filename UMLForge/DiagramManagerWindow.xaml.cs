using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using UMLForge.Models;
using UMLForge.Services;

namespace UMLForge;

public partial class DiagramManagerWindow : Window
{
    private readonly UmlModel _model;
    private UmlDiagram? _selectedDiagram;
    private UmlDiagramNode? _selectedNode;
    private bool _loading;
    private Point _offset;
    private bool _dragging;
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly TranslateTransform _pan = new();
    private bool _panning;
    private Point _panStart;
    private double _panStartX;
    private double _panStartY;
    public string LastAction { get; private set; } = "Aucun";
    private static readonly string[] DiagramTypes = UmlNotationCatalog.All.Select(d => d.Type).ToArray();
    private static readonly string[] NodeKinds = { "Class", "Interface", "Enumeration", "Actor", "UseCase", "Lifeline", "Message", "Action", "Decision", "Merge", "Fork", "Join", "Start", "End", "Component", "Artifact", "Node", "Package", "State", "Object", "Note", "Port", "Database", "Boundary", "Control", "Entity", "Signal", "Timer", "Constraint", "Profile", "Subsystem" };

    public DiagramManagerWindow(UmlModel model)
    {
        InitializeComponent(); _model = model;
        var canvasTransform = new TransformGroup();
        canvasTransform.Children.Add(_zoom);
        canvasTransform.Children.Add(_pan);
        DiagramSurface.RenderTransform = canvasTransform;
        DiagramTypeBox.ItemsSource = DiagramTypes; NodeKindBox.ItemsSource = NodeKinds;
        VisibilityBox.ItemsSource = new[] { "public", "private", "protected", "package" };
        DiagramList.ItemsSource = _model.Diagrams;
        if (_model.Diagrams.Count == 0) _model.Diagrams.Add(new UmlDiagram { Name = "Cas d'utilisation", Type = "UseCase", Nodes = new() { new UmlDiagramNode { Name = "Utilisateur", Kind = "Actor", X = 50, Y = 90 }, new UmlDiagramNode { Name = "Se connecter", Kind = "UseCase", X = 240, Y = 100 } } });
        DiagramList.SelectedIndex = 0;
    }

    private void NewDiagram_Click(object sender, RoutedEventArgs e)
    {
        var type = DiagramTypeBox.SelectedItem as string ?? "UseCase";
        var diagram = new UmlDiagram { Name = "Nouveau " + type, Type = type };
        _model.Diagrams.Add(diagram); DiagramList.SelectedItem = diagram; LastAction = "Diagramme créé";
    }

    private void DiagramTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _selectedDiagram is null) return;
        var type = DiagramTypeBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(type)) return;
        NodeKindBox.ItemsSource = GetKindsFor(type);
        LinkLabelBox.Text = DefaultLinkKind(type);
    }

    private void DiagramList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DiagramList.SelectedItem is not UmlDiagram d) return;
        _selectedDiagram = d; _selectedNode = null; _loading = true;
        SnapToGridCheck.IsChecked = d.SnapToGrid;
        DiagramNameBox.Text = d.Name; DiagramTypeBox.SelectedItem = d.Type; NodeNameBox.Text = ""; NodeDetailsBox.Text = ""; NodeKindBox.ItemsSource = GetKindsFor(d.Type); NodeKindBox.SelectedIndex = -1;
        LinkSourceBox.ItemsSource = d.Nodes; LinkTargetBox.ItemsSource = d.Nodes; LinkLabelBox.Text = DefaultLinkKind(d.Type);
        _loading = false; Render();
    }

    private void AddNode_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDiagram == null) { InfoText.Text = "Créez ou sélectionnez un diagramme."; return; }
        var kind = NodeKindBox.SelectedItem as string ?? DefaultKind(_selectedDiagram.Type);
        var node = new UmlDiagramNode { Name = "Nouvel élément", Kind = kind, X = 30 + (_selectedDiagram.Nodes.Count % 3) * 170, Y = 40 + (_selectedDiagram.Nodes.Count / 3) * 110 };
        _selectedDiagram.Nodes.Add(node); LinkSourceBox.ItemsSource = _selectedDiagram.Nodes; LinkTargetBox.ItemsSource = _selectedDiagram.Nodes; LinkSourceBox.Items.Refresh(); LinkTargetBox.Items.Refresh(); _selectedNode = node; LoadNode(); Render(); LastAction = "Élément ajouté";
    }

    private static string DefaultKind(string type) => type switch
    {
        "Class" => "Class", "UseCase" => "UseCase", "Sequence" => "Lifeline", "Activity" => "Action",
        "Component" => "Component", "Deployment" => "Node", "State" => "State", "Package" => "Package",
        "Timing" => "Lifeline", "Object" => "Object", "Communication" => "Object", "CompositeStructure" => "Part",
        "InteractionOverview" => "Start", "Profile" => "Stereotype", _ => "Note"
    };

    private static string[] GetKindsFor(string type) => UmlNotationCatalog.GetNodeKinds(type);

    private static string DefaultLinkKind(string type) => type switch
    {
        "Sequence" or "Communication" => "Message", "Activity" or "InteractionOverview" => "ControlFlow",
        "UseCase" => "Association", "Class" => "Association", "Component" => "Dependency",
        "Deployment" => "CommunicationPath", "State" => "Transition", "Timing" => "TimeObservation",
        "Package" => "PackageImport", "Object" => "Link", "CompositeStructure" => "Connector", "Profile" => "Extension", _ => "Association"
    };

    private void Render()
    {
        DiagramSurface.Children.Clear(); if (_selectedDiagram == null) return;
        foreach (var link in _selectedDiagram.Links)
        {
            var from = _selectedDiagram.Nodes.FirstOrDefault(n => n.Id == link.FromId);
            var to = _selectedDiagram.Nodes.FirstOrDefault(n => n.Id == link.ToId);
            if (from is null || to is null) continue;
            var line = new Line { X1 = from.X + 72, Y1 = from.Y + 30, X2 = to.X + 72, Y2 = to.Y + 30, Stroke = new SolidColorBrush(Color.FromRgb(100, 116, 139)), StrokeThickness = 1.5 };
            if (link.Kind.Contains("Dependency", StringComparison.OrdinalIgnoreCase) || link.Kind.Contains("Message", StringComparison.OrdinalIgnoreCase)) line.StrokeDashArray = new DoubleCollection { 4, 2 };
            DiagramSurface.Children.Add(line);
            if (!string.IsNullOrWhiteSpace(link.Label))
            {
                var label = new TextBlock { Text = link.Label, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)), Background = Brushes.White };
                Canvas.SetLeft(label, (line.X1 + line.X2) / 2); Canvas.SetTop(label, (line.Y1 + line.Y2) / 2 - 12); DiagramSurface.Children.Add(label);
            }
        }
        foreach (var node in _selectedDiagram.Nodes)
        {
            var ellipse = node.Kind is "UseCase" or "Start" or "End";
            var radius = node.Kind is "State" or "Action" or "Boundary" or "Control" or "Entity" ? new CornerRadius(16) : ellipse ? new CornerRadius(32) : new CornerRadius(4);
            var fill = node.Id == _selectedNode?.Id ? Color.FromRgb(219, 234, 254) : node.Kind is "Decision" ? Color.FromRgb(254, 243, 199) : Color.FromRgb(248, 250, 252);
            var shape = new Border { Width = ellipse ? 132 : 145, MinHeight = 54, Padding = new Thickness(8), CornerRadius = radius, Background = new SolidColorBrush(fill), BorderBrush = node.Id == _selectedNode?.Id ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(100, 116, 139)), BorderThickness = new Thickness(node.Id == _selectedNode?.Id ? 2 : 1), Child = new StackPanel { Children = { new TextBlock { Text = NodeStereotype(node), FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)), HorizontalAlignment = HorizontalAlignment.Center }, new TextBlock { Text = node.Name, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center }, new TextBlock { Text = node.Details, FontSize = 9, Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center } } }, Tag = node.Id, Cursor = Cursors.SizeAll };
            shape.MouseLeftButtonDown += Node_MouseDown; shape.MouseMove += Node_MouseMove; shape.MouseLeftButtonUp += Node_MouseUp;
            Canvas.SetLeft(shape, node.X); Canvas.SetTop(shape, node.Y); DiagramSurface.Children.Add(shape);
            if ((_selectedDiagram.Type == "Sequence" || _selectedDiagram.Type == "Timing") && node.Kind == "Lifeline")
            {
                var lifeline = new Line { X1 = node.X + shape.Width / 2, Y1 = node.Y + 54, X2 = node.X + shape.Width / 2, Y2 = Math.Max(390, node.Y + 280), Stroke = new SolidColorBrush(Color.FromRgb(148, 163, 184)), StrokeDashArray = new DoubleCollection { 5, 3 }, StrokeThickness = 1 };
                DiagramSurface.Children.Add(lifeline); Panel.SetZIndex(lifeline, -1);
            }
        }
    }

    private void Node_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border b || b.Tag is not Guid id || _selectedDiagram == null) return;
        _selectedNode = _selectedDiagram.Nodes.FirstOrDefault(n => n.Id == id); LoadNode(); Render();
        _offset = e.GetPosition(DiagramSurface); _offset.X -= _selectedNode?.X ?? 0; _offset.Y -= _selectedNode?.Y ?? 0; _dragging = true; b.CaptureMouse(); e.Handled = true;
    }
    private void Node_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed || _selectedNode == null || sender is not Border b) return;
        var p = e.GetPosition(DiagramSurface); _selectedNode.X = Math.Max(0, p.X - _offset.X); _selectedNode.Y = Math.Max(0, p.Y - _offset.Y); Canvas.SetLeft(b, _selectedNode.X); Canvas.SetTop(b, _selectedNode.Y);
    }
    private void Node_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _dragging = false;
        if (sender is Border b) b.ReleaseMouseCapture();

        if (_selectedDiagram?.SnapToGrid == true && _selectedNode != null)
        {
            var grid = Math.Max(1, _selectedDiagram.GridSize);
            _selectedNode.X = Math.Round(_selectedNode.X / grid) * grid;
            _selectedNode.Y = Math.Round(_selectedNode.Y / grid) * grid;
        }

        Render();
        LastAction = "Élément déplacé";
    }

    private void SnapToGrid_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading || _selectedDiagram is null) return;
        _selectedDiagram.SnapToGrid = SnapToGridCheck.IsChecked == true;
        LastAction = _selectedDiagram.SnapToGrid ? "Grille aimantée activée" : "Grille aimantée désactivée";
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ChangeZoom(1.15);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ChangeZoom(1 / 1.15);

    private void ResetZoom_Click(object sender, RoutedEventArgs e)
    {
        _zoom.ScaleX = _zoom.ScaleY = 1;
        _zoom.CenterX = _zoom.CenterY = 0;
        _pan.X = _pan.Y = 0;
    }

    private void FitDiagram_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDiagram is null || _selectedDiagram.Nodes.Count == 0)
        {
            ResetZoom_Click(sender, e);
            return;
        }

        var minX = _selectedDiagram.Nodes.Min(n => n.X);
        var minY = _selectedDiagram.Nodes.Min(n => n.Y);
        var maxX = _selectedDiagram.Nodes.Max(n => n.X + 160);
        var maxY = _selectedDiagram.Nodes.Max(n => n.Y + 100);
        var availableWidth = Math.Max(100, DiagramSurface.ActualWidth - 40);
        var availableHeight = Math.Max(100, DiagramSurface.ActualHeight - 40);
        var contentWidth = Math.Max(1, maxX - minX);
        var contentHeight = Math.Max(1, maxY - minY);
        var scale = Math.Clamp(Math.Min(availableWidth / contentWidth, availableHeight / contentHeight), 0.25, 1.5);

        _zoom.CenterX = _zoom.CenterY = 0;
        _zoom.ScaleX = _zoom.ScaleY = scale;
        _pan.X = 20 - minX * scale;
        _pan.Y = 20 - minY * scale;
    }

    private void ChangeZoom(double factor, Point? anchor = null)
    {
        var oldScale = _zoom.ScaleX;
        var newScale = Math.Clamp(oldScale * factor, 0.25, 2.5);
        if (Math.Abs(newScale - oldScale) < 0.0001) return;

        var point = anchor ?? new Point(
            (DiagramSurface.ActualWidth / 2 - _pan.X) / oldScale,
            (DiagramSurface.ActualHeight / 2 - _pan.Y) / oldScale);

        _pan.X += (oldScale - newScale) * point.X;
        _pan.Y += (oldScale - newScale) * point.Y;
        _zoom.CenterX = _zoom.CenterY = 0;
        _zoom.ScaleX = _zoom.ScaleY = newScale;
    }

    private void DiagramSurface_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl)) return;
        ChangeZoom(e.Delta > 0 ? 1.1 : 1 / 1.1, e.GetPosition(DiagramSurface));
        e.Handled = true;
    }

    private void DiagramSurface_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle) return;
        _panning = true;
        _panStart = e.GetPosition(this);
        _panStartX = _pan.X;
        _panStartY = _pan.Y;
        DiagramSurface.CaptureMouse();
        Cursor = Cursors.Hand;
        e.Handled = true;
    }

    private void DiagramSurface_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_panning) return;
        var current = e.GetPosition(this);
        _pan.X = _panStartX + current.X - _panStart.X;
        _pan.Y = _panStartY + current.Y - _panStart.Y;
        e.Handled = true;
    }

    private void DiagramSurface_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || !_panning) return;
        _panning = false;
        DiagramSurface.ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
        e.Handled = true;
    }
    private static string NodeStereotype(UmlDiagramNode node) => node.Kind switch
    {
        "Class" => "«class»", "Interface" => "«interface»", "Enumeration" => "«enumeration»", "Actor" => "ACTEUR",
        "UseCase" => "CAS D'UTILISATION", "Lifeline" => "LIFELINE", "Component" => "«component»", "Artifact" => "«artifact»",
        "Node" => "«node»", "Database" => "«database»", "State" => "ÉTAT", "Decision" => "DÉCISION", "Action" => "ACTION",
        "Package" => "«package»", "Port" => "PORT", "Profile" => "«profile»", _ => node.Kind.ToUpperInvariant()
    };

    private void AddLink_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDiagram is null || LinkSourceBox.SelectedItem is not UmlDiagramNode from || LinkTargetBox.SelectedItem is not UmlDiagramNode to)
        { InfoText.Text = "Sélectionnez une source et une cible pour créer un lien."; return; }
        if (from.Id == to.Id) { InfoText.Text = "Un lien doit relier deux éléments différents."; return; }
        var kind = string.IsNullOrWhiteSpace(LinkLabelBox.Text) ? DefaultLinkKind(_selectedDiagram.Type) : LinkLabelBox.Text.Trim();
        var existing = _selectedDiagram.Links.FirstOrDefault(l => l.FromId == from.Id && l.ToId == to.Id && l.Kind == kind);
        if (existing is null) _selectedDiagram.Links.Add(new UmlDiagramLink
        {
            FromId = from.Id, ToId = to.Id, Kind = kind, Label = kind,
            SourceRole = string.IsNullOrWhiteSpace(SourceRoleBox.Text) ? null : SourceRoleBox.Text.Trim(),
            TargetRole = string.IsNullOrWhiteSpace(TargetRoleBox.Text) ? null : TargetRoleBox.Text.Trim(),
            GuardCondition = string.IsNullOrWhiteSpace(LinkGuardBox.Text) ? null : LinkGuardBox.Text.Trim(),
            IsNavigable = IsNavigableCheck.IsChecked == true
        });
        Render(); LastAction = "Lien créé"; InfoText.Text = "Lien créé. Enregistrez le modèle pour conserver les changements.";
    }

    private void LoadNode()
    {
        _loading = true;
        NodeNameBox.Text = _selectedNode?.Name ?? ""; NodeKindBox.SelectedItem = _selectedNode?.Kind; NodeDetailsBox.Text = _selectedNode?.Details ?? "";
        StereotypeBox.Text = _selectedNode?.Stereotype ?? ""; VisibilityBox.SelectedItem = _selectedNode?.Visibility ?? "public";
        MultiplicityBox.Text = _selectedNode?.Multiplicity ?? ""; GuardConditionBox.Text = _selectedNode?.GuardCondition ?? "";
        ConstraintBox.Text = _selectedNode?.Constraint ?? ""; DurationBox.Text = _selectedNode?.Duration ?? "";
        IsAbstractCheck.IsChecked = _selectedNode?.IsAbstract ?? false; IsStaticCheck.IsChecked = _selectedNode?.IsStatic ?? false;
        _loading = false;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDiagram == null) return;
        _selectedDiagram.Name = string.IsNullOrWhiteSpace(DiagramNameBox.Text) ? "Diagramme UML" : DiagramNameBox.Text.Trim();
        _selectedDiagram.Type = DiagramTypeBox.SelectedItem as string ?? _selectedDiagram.Type;
        if (_selectedNode != null)
        {
            _selectedNode.Name = NodeNameBox.Text.Trim(); _selectedNode.Kind = NodeKindBox.SelectedItem as string ?? _selectedNode.Kind; _selectedNode.Details = NodeDetailsBox.Text;
            _selectedNode.Stereotype = string.IsNullOrWhiteSpace(StereotypeBox.Text) ? null : StereotypeBox.Text.Trim();
            _selectedNode.Visibility = VisibilityBox.SelectedItem as string ?? "public";
            _selectedNode.Multiplicity = string.IsNullOrWhiteSpace(MultiplicityBox.Text) ? null : MultiplicityBox.Text.Trim();
            _selectedNode.GuardCondition = string.IsNullOrWhiteSpace(GuardConditionBox.Text) ? null : GuardConditionBox.Text.Trim();
            _selectedNode.Constraint = string.IsNullOrWhiteSpace(ConstraintBox.Text) ? null : ConstraintBox.Text.Trim();
            _selectedNode.Duration = string.IsNullOrWhiteSpace(DurationBox.Text) ? null : DurationBox.Text.Trim();
            _selectedNode.IsAbstract = IsAbstractCheck.IsChecked == true; _selectedNode.IsStatic = IsStaticCheck.IsChecked == true;
        }
        DiagramList.Items.Refresh(); Render(); LastAction = "Modifications appliquées"; InfoText.Text = "Modèle enregistré en mémoire. Enregistrez le projet principal pour persister les changements.";
    }

    private void DeleteNode_Click(object sender, RoutedEventArgs e) { if (_selectedDiagram == null || _selectedNode == null) return; _selectedDiagram.Links.RemoveAll(l => l.FromId == _selectedNode.Id || l.ToId == _selectedNode.Id); _selectedDiagram.Nodes.Remove(_selectedNode); _selectedNode = null; LinkSourceBox.ItemsSource = _selectedDiagram.Nodes; LinkTargetBox.ItemsSource = _selectedDiagram.Nodes; LoadNode(); Render(); LastAction = "Élément supprimé"; }
    private void DeleteDiagram_Click(object sender, RoutedEventArgs e) { if (_selectedDiagram == null) return; _model.Diagrams.Remove(_selectedDiagram); _selectedDiagram = null; _selectedNode = null; DiagramList.SelectedIndex = _model.Diagrams.Count - 1; Render(); LastAction = "Diagramme supprimé"; }
    private void Done_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        var reports = UmlValidationService.Validate(_model);
        var projectIssues = UmlProjectValidationService.Validate(_model);
        var issues = projectIssues.Select(i => $"[{i.Severity}] {i.RuleId} · {i.Element} : {i.Message}").ToArray();
        var errors = projectIssues.Count(i => i.Severity == UmlIssueSeverity.Error);
        var warnings = projectIssues.Count(i => i.Severity == UmlIssueSeverity.Warning);
        var summary = $"Validation UML : {reports.Count} diagramme(s), {errors} erreur(s), {warnings} avertissement(s), {projectIssues.Count(i => i.Severity == UmlIssueSeverity.Info)} information(s).";
        InfoText.Text = summary;
        MessageBox.Show(summary + "\n\n" + (issues.Length == 0 ? "Aucun problème détecté par les règles intégrées." : string.Join("\n", issues.Take(80))) + (issues.Length > 80 ? "\n… autres problèmes omis de cette fenêtre." : "") + "\n\nCette validation couvre les règles UMLForge du modèle et des diagrammes. Elle ne constitue pas encore une certification complète de conformité OMG UML 2.5.1, ni un moteur OCL complet.", "Validation UML", MessageBoxButton.OK, errors > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        LastAction = "Validation UML exécutée";
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedDiagram == null) return;
        var dlg = new SaveFileDialog { Filter = "PlantUML (*.puml)|*.puml|JSON (*.json)|*.json", FileName = _selectedDiagram.Name.Replace(' ', '_') + ".puml" };
        if (dlg.ShowDialog() != true) return;
        var output = dlg.FilterIndex == 2 ? System.Text.Json.JsonSerializer.Serialize(_selectedDiagram, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) : ExportDiagram(_selectedDiagram);
        File.WriteAllText(dlg.FileName, output); InfoText.Text = "Export terminé : " + dlg.FileName; LastAction = "Export effectué";
    }

    private static string ExportDiagram(UmlDiagram d)
    {
        var b = new System.Text.StringBuilder("@startuml\n title " + d.Name.Replace('\n', ' ') + "\n");
        if (d.Type == "Class") b.AppendLine("skinparam classAttributeIconSize 0");
        var aliases = d.Nodes.ToDictionary(n => n.Id, n => "n" + n.Id.ToString("N")[..8]);
        foreach (var n in d.Nodes)
        {
            var id = aliases[n.Id]; var name = n.Name.Replace('\"', '\''); var details = n.Details.Replace('\"', '\'');
            switch (d.Type)
            {
                case "Sequence":
                    if (n.Kind == "Actor") b.AppendLine($"actor \"{name}\" as {id}");
                    else if (n.Kind == "Note") b.AppendLine($"note over {id} : {details}");
                    else b.AppendLine($"participant \"{name}\" as {id}");
                    break;
                case "UseCase":
                    if (n.Kind == "Actor") b.AppendLine($"actor \"{name}\" as {id}");
                    else if (n.Kind == "Note") b.AppendLine($"note \"{details}\" as {id}");
                    else b.AppendLine($"usecase \"{name}\" as {id}");
                    break;
                case "Class":
                    var keyword = n.Kind switch { "Interface" => "interface", "Enumeration" => "enum", _ => "class" };
                    b.AppendLine($"{keyword} \"{name}\" as {id} {{");
                    foreach (var member in details.Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) b.AppendLine("  " + member);
                    b.AppendLine("}");
                    break;
                case "Component": b.AppendLine(n.Kind == "Interface" ? $"() \"{name}\" as {id}" : $"component \"{name}\" as {id}"); break;
                case "Deployment": b.AppendLine(n.Kind == "Artifact" ? $"artifact \"{name}\" as {id}" : n.Kind == "Database" ? $"database \"{name}\" as {id}" : $"node \"{name}\" as {id}"); break;
                case "State": b.AppendLine(n.Kind == "Start" ? $"state \"{name}\" as {id} <<start>>" : n.Kind == "End" ? $"state \"{name}\" as {id} <<end>>" : $"state \"{name}\" as {id}"); break;
                case "Activity": b.AppendLine(n.Kind switch { "Start" => $"(*) as {id}", "End" => $"(*) as {id} <<end>>", "Decision" => $"if (\"{name}\") then (yes) as {id}", _ => $":{name}; as {id}" }); break;
                case "Package": b.AppendLine($"package \"{name}\" as {id} {{ }}"); break;
                case "Object": b.AppendLine($"object \"{name}\" as {id}"); break;
                case "Timing": b.AppendLine($"concise \"{name}\" as {id}"); break;
                case "Profile": b.AppendLine($"class \"{name}\" as {id} <<{n.Kind.ToLowerInvariant()}>>"); break;
                default: b.AppendLine($"rectangle \"{name}\" as {id}"); break;
            }
        }
        foreach (var link in d.Links)
        {
            if (!aliases.TryGetValue(link.FromId, out var from) || !aliases.TryGetValue(link.ToId, out var to)) continue;
            var arrow = link.Kind switch { "Inheritance" or "Generalization" => " --|> ", "Realization" => " ..|> ", "Dependency" => " ..> ", "Composition" => " *-- ", "Aggregation" => " o-- ", "ControlFlow" or "Transition" => " --> ", "Message" => " -> ", _ => " --> " };
            var label = string.IsNullOrWhiteSpace(link.Label) ? "" : " : " + link.Label.Replace('\n', ' ');
            b.AppendLine(from + arrow + to + label);
        }
        b.AppendLine("@enduml"); return b.ToString();
    }
}