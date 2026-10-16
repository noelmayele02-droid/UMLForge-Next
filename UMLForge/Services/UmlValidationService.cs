using UMLForge.Models;

namespace UMLForge.Services;

public enum UmlIssueSeverity { Error, Warning, Info }
public sealed record UmlValidationIssue(UmlIssueSeverity Severity, string RuleId, string Element, string Message);
public sealed record UmlValidationReport(string DiagramName, string DiagramType, IReadOnlyList<UmlValidationIssue> Issues)
{
    public int Errors => Issues.Count(i => i.Severity == UmlIssueSeverity.Error);
    public int Warnings => Issues.Count(i => i.Severity == UmlIssueSeverity.Warning);
}

/// <summary>Pragmatic model-consistency checks. It is not a substitute for a certified, complete OMG UML 2.5.1 validator.</summary>
public static class UmlValidationService
{
    public static IReadOnlyList<UmlValidationReport> Validate(UmlModel model) => model.Diagrams.Select(Validate).ToArray();

    public static UmlValidationReport Validate(UmlDiagram diagram)
    {
        var issues = new List<UmlValidationIssue>();
        void Add(UmlIssueSeverity severity, string id, string element, string message) => issues.Add(new(severity, id, element, message));
        if (!UmlNotationCatalog.TryGet(diagram.Type, out var definition)) Add(UmlIssueSeverity.Error, "UML0001", diagram.Name, $"Type de diagramme non reconnu : {diagram.Type}.");
        else
        {
            foreach (var node in diagram.Nodes.Where(n => !definition.NodeKinds.Contains(n.Kind, StringComparer.OrdinalIgnoreCase)))
                Add(UmlIssueSeverity.Warning, "UML0004", node.Name, $"L'élément « {node.Kind} » ne fait pas partie de la palette déclarée pour le diagramme {diagram.Type}.");
            foreach (var link in diagram.Links.Where(l => !definition.RelationKinds.Contains(l.Kind, StringComparer.OrdinalIgnoreCase)))
                Add(UmlIssueSeverity.Warning, "UML0005", link.Label, $"La relation « {link.Kind} » n'est pas dans le catalogue courant pour {diagram.Type}; vérifier sa notation.");
        }
        if (string.IsNullOrWhiteSpace(diagram.Name)) Add(UmlIssueSeverity.Error, "UML0002", diagram.Name, "Le diagramme doit avoir un nom.");
        if (diagram.GridSize < 1) Add(UmlIssueSeverity.Warning, "UML0003", diagram.Name, "La grille doit être supérieure à zéro.");

        var ids = diagram.Nodes.Select(n => n.Id).ToHashSet();
        foreach (var duplicate in diagram.Nodes.GroupBy(n => n.Id).Where(g => g.Count() > 1)) Add(UmlIssueSeverity.Error, "UML0010", duplicate.Key.ToString(), "Identifiant d'élément dupliqué.");
        foreach (var node in diagram.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Name)) Add(UmlIssueSeverity.Error, "UML0011", node.Kind, "Un élément n'a pas de nom.");
            if (node.X < 0 || node.Y < 0) Add(UmlIssueSeverity.Warning, "UML0012", node.Name, "L'élément est placé hors de la zone positive du canevas.");
            if (node.Multiplicity is { Length: > 0 } m && !IsMultiplicity(m)) Add(UmlIssueSeverity.Warning, "UML0013", node.Name, $"Multiplicité potentiellement invalide : {m}.");
            if (node.IsAbstract && !string.Equals(node.Kind, "Class", StringComparison.OrdinalIgnoreCase)) Add(UmlIssueSeverity.Warning, "UML0014", node.Name, "Le marqueur abstrait est habituellement réservé aux classes/opérations compatibles.");
            if (node.IsStatic && node.IsAbstract) Add(UmlIssueSeverity.Warning, "UML0015", node.Name, "Vérifiez la combinaison des marqueurs static et abstract pour cet élément.");
            if (node.Visibility is { Length: > 0 } visibility && !new[] { "public", "private", "protected", "internal", "package", "+", "-", "#", "~" }.Contains(visibility, StringComparer.Ordinal)) Add(UmlIssueSeverity.Warning, "UML0016", node.Name, $"Notation de visibilité non reconnue : {visibility}.");
        }
        foreach (var link in diagram.Links)
        {
            if (!ids.Contains(link.FromId) || !ids.Contains(link.ToId)) Add(UmlIssueSeverity.Error, "UML0020", link.Label, "Le lien référence un élément absent du diagramme.");
            if (link.FromId == link.ToId && !diagram.Type.Equals("State", StringComparison.OrdinalIgnoreCase)) Add(UmlIssueSeverity.Warning, "UML0021", link.Label, "Lien réflexif : vérifiez qu'il est intentionnel.");
            if (string.IsNullOrWhiteSpace(link.Kind)) Add(UmlIssueSeverity.Error, "UML0022", link.Label, "Le lien doit avoir un type de relation.");
            if (link.GuardCondition is { Length: > 0 } && diagram.Type is not ("Sequence" or "Activity" or "State" or "Communication" or "InteractionOverview")) Add(UmlIssueSeverity.Warning, "UML0023", link.Label, "Une condition de garde n'est pas couramment utilisée pour ce type de relation.");
            if (link.FromId == link.ToId && link.Kind is not ("SelfMessage" or "InternalTransition")) Add(UmlIssueSeverity.Warning, "UML0024", link.Label, "Une relation réflexive doit être explicitement autorisée par la notation du diagramme.");
            if (diagram.Type == "UseCase" && link.Kind.Equals("Include", StringComparison.OrdinalIgnoreCase) && link.FromId == link.ToId) Add(UmlIssueSeverity.Error, "UML0025", link.Label, "Un cas d'utilisation ne peut pas s'inclure lui-même.");
        }

        var kinds = diagram.Nodes.Select(n => n.Kind).ToHashSet(StringComparer.OrdinalIgnoreCase);
        switch (diagram.Type)
        {
            case "Class":
                RequireAny("Class", "Interface", "Enumeration");
                foreach (var node in diagram.Nodes.Where(n => n.Kind.Equals("Class", StringComparison.OrdinalIgnoreCase)))
                    if (node.Details.Contains(";", StringComparison.Ordinal) && node.Details.Length > 4000) Add(UmlIssueSeverity.Info, "UML0104", node.Name, "Vérifiez si les membres gagneraient à être représentés séparément.");
                break;
            case "UseCase":
                Require("Actor", "Un diagramme de cas d'utilisation devrait représenter au moins un acteur.");
                Require("UseCase", "Un diagramme de cas d'utilisation devrait représenter au moins un cas d'utilisation.");
                break;
            case "Sequence":
                Require("Lifeline", "Un diagramme de séquence nécessite des lignes de vie.");
                Require("Message", "Un diagramme de séquence devrait contenir des messages.");
                foreach (var msg in diagram.Nodes.Where(n => n.Kind.Equals("Message", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(n.Details))) Add(UmlIssueSeverity.Warning, "UML0203", msg.Name, "Précisez le message, l'appel ou la signature.");
                foreach (var fragment in diagram.Nodes.Where(n => n.Kind.Equals("CombinedFragment", StringComparison.OrdinalIgnoreCase)))
                    if (!new[] { "alt", "opt", "loop", "par", "break", "critical", "assert", "neg", "strict", "seq", "ignore", "consider", "region" }.Contains(fragment.Stereotype ?? fragment.Details.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "", StringComparer.OrdinalIgnoreCase))
                        Add(UmlIssueSeverity.Warning, "UML0204", fragment.Name, "Opérateur de fragment combiné absent ou non reconnu (alt, opt, loop, par, break…).");
                var orders = diagram.Nodes.Where(n => n.Kind.Equals("Message", StringComparison.OrdinalIgnoreCase)).Select(n => n.Details).Where(d => !string.IsNullOrWhiteSpace(d)).ToArray();
                if (orders.Length > 1 && orders.All(d => int.TryParse(d.Split(' ', ':', '.')[0], out _)))
                {
                    var numeric = orders.Select(d => int.Parse(d.Split(' ', ':', '.')[0])).ToArray();
                    if (!numeric.SequenceEqual(numeric.OrderBy(n => n))) Add(UmlIssueSeverity.Warning, "UML0205", diagram.Name, "L'ordre visuel des messages ne correspond pas à leur numérotation.");
                }
                break;
            case "Activity":
                Require("Start", "Un diagramme d'activité devrait contenir un nœud initial.");
                Require("End", "Un diagramme d'activité devrait contenir un nœud final.");
                if (diagram.Nodes.Count(n => n.Kind.Equals("Start", StringComparison.OrdinalIgnoreCase)) > 1) Add(UmlIssueSeverity.Warning, "UML0303", diagram.Name, "Plusieurs nœuds initiaux : vérifiez le modèle d'activité.");
                foreach (var fork in diagram.Nodes.Where(n => n.Kind.Equals("Fork", StringComparison.OrdinalIgnoreCase)))
                    if (diagram.Links.Count(l => l.FromId == fork.Id) < 2) Add(UmlIssueSeverity.Warning, "UML0305", fork.Name, "Un fork devrait distribuer le flux vers au moins deux branches.");
                foreach (var join in diagram.Nodes.Where(n => n.Kind.Equals("Join", StringComparison.OrdinalIgnoreCase)))
                    if (diagram.Links.Count(l => l.ToId == join.Id) < 2) Add(UmlIssueSeverity.Warning, "UML0306", join.Name, "Un join devrait synchroniser au moins deux branches.");
                foreach (var end in diagram.Nodes.Where(n => n.Kind.Equals("End", StringComparison.OrdinalIgnoreCase)))
                    if (diagram.Links.Count(l => l.ToId == end.Id) == 0) Add(UmlIssueSeverity.Warning, "UML0307", end.Name, "Le nœud final n'a aucun flux entrant.");
                foreach (var decision in diagram.Nodes.Where(n => n.Kind.Equals("Decision", StringComparison.OrdinalIgnoreCase)))
                    if (!diagram.Links.Any(l => l.FromId == decision.Id && !string.IsNullOrWhiteSpace(l.GuardCondition))) Add(UmlIssueSeverity.Warning, "UML0304", decision.Name, "Ajoutez des conditions de garde aux sorties du nœud de décision.");
                break;
            case "State":
                Require("Start", "Un diagramme d'états devrait avoir un état initial.");
                Require("State", "Ajoutez au moins un état.");
                foreach (var transition in diagram.Links.Where(l => l.Kind.Equals("Transition", StringComparison.OrdinalIgnoreCase)))
                    if (string.IsNullOrWhiteSpace(transition.Label) && string.IsNullOrWhiteSpace(transition.GuardCondition)) Add(UmlIssueSeverity.Info, "UML0401", diagram.Name, "Une transition peut documenter son déclencheur, sa garde ou son effet.");
                foreach (var final in diagram.Nodes.Where(n => n.Kind.Equals("End", StringComparison.OrdinalIgnoreCase)))
                    if (diagram.Links.Count(l => l.ToId == final.Id) == 0) Add(UmlIssueSeverity.Warning, "UML0402", final.Name, "L'état final n'a aucune transition entrante.");
                break;
            case "Component":
                Require("Component", "Ajoutez au moins un composant.");
                foreach (var port in diagram.Nodes.Where(n => n.Kind.Equals("Port", StringComparison.OrdinalIgnoreCase)))
                    if (string.IsNullOrWhiteSpace(port.Details)) Add(UmlIssueSeverity.Warning, "UML0502", port.Name, "Précisez les interfaces fournies/requises du port.");
                break;
            case "Deployment":
                Require("Node", "Ajoutez au moins un nœud de déploiement.");
                Require("Artifact", "Ajoutez un artefact ou documentez pourquoi le diagramme représente uniquement l'infrastructure.");
                foreach (var node in diagram.Nodes.Where(n => n.Kind.Equals("ExecutionEnvironment", StringComparison.OrdinalIgnoreCase)))
                    if (diagram.Links.Count(l => l.FromId == node.Id || l.ToId == node.Id) == 0) Add(UmlIssueSeverity.Info, "UML0503", node.Name, "L'environnement d'exécution n'est relié à aucun artefact ou nœud.");
                break;
            case "Package": Require("Package", "Ajoutez au moins un paquetage."); break;
            case "Object": Require("Object", "Ajoutez au moins un objet."); break;
            case "Communication":
                Require("Message", "Ajoutez au moins un message numéroté ou lien de communication.");
                foreach (var message in diagram.Nodes.Where(n => n.Kind.Equals("Message", StringComparison.OrdinalIgnoreCase)))
                    if (string.IsNullOrWhiteSpace(message.Details) || !message.Details.Any(char.IsDigit)) Add(UmlIssueSeverity.Warning, "UML0501", message.Name, "Un message de communication devrait porter une numérotation de séquence (ex. 1, 1.1).");
                break;
            case "CompositeStructure":
                RequireAny("Class", "Part", "Collaboration");
                if (kinds.Contains("Connector") && !kinds.Contains("Port")) Add(UmlIssueSeverity.Warning, "UML0601", diagram.Name, "Des connecteurs sont présents sans ports explicites; vérifiez la structure interne.");
                break;
            case "InteractionOverview":
                Require("Start", "La vue d'interaction devrait avoir un nœud initial.");
                Require("InteractionUse", "Ajoutez une référence à une interaction ou une action d'interaction.");
                foreach (var decision in diagram.Nodes.Where(n => n.Kind.Equals("Decision", StringComparison.OrdinalIgnoreCase)))
                    if (diagram.Links.Count(l => l.FromId == decision.Id) < 2) Add(UmlIssueSeverity.Warning, "UML0701", decision.Name, "Une décision devrait avoir plusieurs flux sortants avec des gardes appropriées.");
                break;
            case "Profile":
                Require("Stereotype", "Un diagramme de profil devrait déclarer au moins un stéréotype.");
                foreach (var extension in diagram.Links.Where(l => l.Kind.Equals("Extension", StringComparison.OrdinalIgnoreCase)))
                    if (string.IsNullOrWhiteSpace(extension.Label)) Add(UmlIssueSeverity.Warning, "UML0801", diagram.Name, "Précisez la métaclasse étendue par cette relation Extension.");
                break;
            case "Timing":
                Require("Lifeline", "Un diagramme de timing nécessite au moins une ligne de vie.");
                if (!diagram.Nodes.Any(n => n.Kind.Equals("TimeConstraint", StringComparison.OrdinalIgnoreCase) || n.Kind.Equals("DurationConstraint", StringComparison.OrdinalIgnoreCase))) Add(UmlIssueSeverity.Info, "UML0901", diagram.Name, "Ajoutez des contraintes temporelles si le comportement dépend de délais.");
                break;
        }
        if (diagram.Nodes.Count == 0) Add(UmlIssueSeverity.Warning, "UML0090", diagram.Name, "Le diagramme est vide.");
        if (diagram.Nodes.Count > 250) Add(UmlIssueSeverity.Info, "UML0091", diagram.Name, "Diagramme volumineux : envisagez des sous-diagrammes ou des vues complémentaires.");
        return new UmlValidationReport(diagram.Name, diagram.Type, issues);

        void Require(string kind, string message) { if (!kinds.Contains(kind)) Add(UmlIssueSeverity.Warning, "UML1000", diagram.Name, message); }
        void RequireAny(params string[] candidates) { if (!candidates.Any(kinds.Contains)) Add(UmlIssueSeverity.Warning, "UML1001", diagram.Name, "Ajoutez un élément central adapté au diagramme : " + string.Join(", ", candidates) + "."); }
    }

    private static bool IsMultiplicity(string value)
    {
        value = value.Trim();
        if (value is "*" or "0..*" or "1..*" or "0..1" or "1") return true;
        var parts = value.Split("..", StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out var min) && int.TryParse(parts[1], out var max) && min >= 0 && max >= min;
    }
}
