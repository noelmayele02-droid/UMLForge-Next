using Microsoft.CodeAnalysis.CSharp;
using UMLForge.Models;

namespace UMLForge.Services;

/// <summary>
/// Project-level consistency checks for UMLForge's semantic model. These rules are deliberately
/// explicit and extensible; they are not represented as OMG-certified UML 2.5.1 conformance.
/// </summary>
public static class UmlProjectValidationService
{
    public static IReadOnlyList<UmlValidationIssue> Validate(UmlModel model)
    {
        var issues = new List<UmlValidationIssue>();
        void Add(UmlIssueSeverity severity, string id, string element, string message) => issues.Add(new(severity, id, element, message));
        var classes = model.Classes.ToArray();
        var classIds = classes.Select(c => c.Id).ToHashSet();
        foreach (var group in classes.GroupBy(c => (Namespace: c.Namespace ?? "", Name: c.Name ?? ""), StringTupleComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            Add(UmlIssueSeverity.Error, "UMLP0001", group.Key.Name, "Deux types possèdent le même nom dans le même espace de noms.");
        foreach (var duplicate in classes.GroupBy(c => c.Id).Where(g => g.Count() > 1))
            Add(UmlIssueSeverity.Error, "UMLP0002", duplicate.Key.ToString(), "Identifiant de classe dupliqué.");

        foreach (var item in classes)
        {
            if (string.IsNullOrWhiteSpace(item.Name) || !SyntaxFacts.IsValidIdentifier(item.Name))
                Add(UmlIssueSeverity.Error, "UMLP0010", item.Name, "Le nom du type doit être un identifiant C# valide.");
            if (string.IsNullOrWhiteSpace(item.Namespace) || item.Namespace.Split('.').Any(n => !SyntaxFacts.IsValidIdentifier(n)))
                Add(UmlIssueSeverity.Warning, "UMLP0011", item.Name, "Espace de noms vide ou invalide pour C#.");
            if (item.IsAbstract && item.IsSealed && !item.IsStatic)
                Add(UmlIssueSeverity.Error, "UMLP0012", item.Name, "Un type ne peut pas être simultanément abstract et sealed, sauf classe static.");
            foreach (var group in item.Properties.GroupBy(p => p.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
                Add(UmlIssueSeverity.Error, "UMLP0020", item.Name, $"Propriété dupliquée : {group.Key}.");
            foreach (var group in item.Operations.GroupBy(o => o.Name + "(" + string.Join(",", o.Parameters.Select(p => p.Type)) + ")", StringComparer.Ordinal).Where(g => g.Count() > 1))
                Add(UmlIssueSeverity.Error, "UMLP0021", item.Name, $"Signature d'opération dupliquée : {group.Key}.");
            foreach (var property in item.Properties)
            {
                if (string.IsNullOrWhiteSpace(property.Name) || !SyntaxFacts.IsValidIdentifier(property.Name)) Add(UmlIssueSeverity.Error, "UMLP0022", item.Name, $"Nom de propriété invalide : {property.Name}.");
                if (string.IsNullOrWhiteSpace(property.Type)) Add(UmlIssueSeverity.Error, "UMLP0023", property.Name, "Une propriété doit avoir un type.");
                if (!IsMultiplicity(property.Multiplicity)) Add(UmlIssueSeverity.Error, "UMLP0024", property.Name, $"Multiplicité invalide : {property.Multiplicity}.");
                if (!IsVisibility(property.Visibility)) Add(UmlIssueSeverity.Warning, "UMLP0025", property.Name, $"Visibilité inconnue : {property.Visibility}.");
            }
            foreach (var operation in item.Operations)
            {
                if (string.IsNullOrWhiteSpace(operation.Name) || !SyntaxFacts.IsValidIdentifier(operation.Name)) Add(UmlIssueSeverity.Error, "UMLP0030", item.Name, $"Nom d'opération invalide : {operation.Name}.");
                if (string.IsNullOrWhiteSpace(operation.ReturnType)) Add(UmlIssueSeverity.Error, "UMLP0031", operation.Name, "Une opération doit déclarer un type de retour (utiliser void si nécessaire).");
                foreach (var parameter in operation.Parameters)
                {
                    if (!SyntaxFacts.IsValidIdentifier(parameter.Name)) Add(UmlIssueSeverity.Error, "UMLP0032", operation.Name, $"Nom de paramètre invalide : {parameter.Name}.");
                    if (string.IsNullOrWhiteSpace(parameter.Type)) Add(UmlIssueSeverity.Error, "UMLP0033", parameter.Name, "Le paramètre doit avoir un type.");
                    if (parameter.Direction is not ("in" or "out" or "inout" or "return")) Add(UmlIssueSeverity.Warning, "UMLP0034", parameter.Name, $"Direction UML inhabituelle : {parameter.Direction}.");
                }
                if (operation.IsAbstract && operation.IsStatic) Add(UmlIssueSeverity.Error, "UMLP0035", operation.Name, "Une opération ne peut pas être à la fois abstraite et statique dans ce profil C#.");
            }
        }

        foreach (var relation in model.Relations)
        {
            if (!classIds.Contains(relation.FromId) || !classIds.Contains(relation.ToId)) Add(UmlIssueSeverity.Error, "UMLP0040", relation.Label, "La relation du modèle référence une classe absente.");
            if (relation.Kind.Equals("Generalization", StringComparison.OrdinalIgnoreCase) && relation.FromId == relation.ToId) Add(UmlIssueSeverity.Error, "UMLP0041", relation.Label, "Un type ne peut pas hériter directement de lui-même.");
            if (!IsMultiplicity(relation.SourceMultiplicity) || !IsMultiplicity(relation.TargetMultiplicity)) Add(UmlIssueSeverity.Error, "UMLP0042", relation.Label, "Multiplicité invalide sur la relation.");
        }
        foreach (var cycle in FindGeneralizationCycles(classes, model.Relations))
            Add(UmlIssueSeverity.Error, "UMLP0043", cycle, "Cycle de généralisation détecté dans le modèle UML.");

        foreach (var package in model.Packages)
        {
            if (string.IsNullOrWhiteSpace(package.Name)) Add(UmlIssueSeverity.Error, "UMLP0050", package.Id.ToString(), "Un paquetage doit avoir un nom.");
            if (package.ParentId == package.Id) Add(UmlIssueSeverity.Error, "UMLP0051", package.Name, "Un paquetage ne peut pas être son propre parent.");
            foreach (var classId in package.OwnedClassIds.Where(id => !classIds.Contains(id))) Add(UmlIssueSeverity.Error, "UMLP0052", package.Name, $"Référence de classe inexistante : {classId}.");
        }
        foreach (var stereotype in model.Stereotypes)
        {
            if (string.IsNullOrWhiteSpace(stereotype.Name) || !SyntaxFacts.IsValidIdentifier(stereotype.Name)) Add(UmlIssueSeverity.Error, "UMLP0060", stereotype.Name, "Le nom du stéréotype doit être un identifiant valide.");
            if (string.IsNullOrWhiteSpace(stereotype.ExtendedMetaclass)) Add(UmlIssueSeverity.Error, "UMLP0061", stereotype.Name, "Un stéréotype doit indiquer la métaclasse étendue.");
        }
        var namedElements = model.Enumerations.Cast<UmlNamedElement>()
            .Concat(model.Enumerations.SelectMany(e => e.Literals))
            .Concat(model.Interfaces)
            .Concat(model.Actors)
            .Concat(model.UseCases)
            .Concat(model.Components)
            .Concat(model.DeploymentNodes)
            .Concat(model.Artifacts)
            .Concat(model.Signals)
            .Concat(model.StateMachines)
            .Concat(model.StateMachines.SelectMany(sm => sm.States))
            .Concat(model.Activities)
            .Concat(model.Activities.SelectMany(a => a.Nodes))
            .Concat(model.Activities.SelectMany(a => a.Edges))
            .Concat(model.Interactions)
            .Concat(model.Interactions.SelectMany(i => i.Lifelines))
            .Concat(model.Interactions.SelectMany(i => i.Messages))
            .Concat(model.Interactions.SelectMany(i => i.CombinedFragments))
            .Concat(model.PrimitiveTypes);
        foreach (var duplicateMapping in model.CodeMappings.GroupBy(m => m.UmlElementId).Where(g => g.Count() > 1))
            Add(UmlIssueSeverity.Error, "UMLP0102", duplicateMapping.Key.ToString(), "Plusieurs associations UML ↔ code partagent le même identifiant UML.");
        foreach (var mapping in model.CodeMappings.Where(m => !classes.Any(c => c.Id == m.UmlElementId)))
            Add(UmlIssueSeverity.Warning, "UMLP0103", mapping.UmlElementName, "L'association de code ne référence plus une classe UML présente dans le modèle.");

        var allConstraints = model.Constraints
            .Concat(classes.SelectMany(c => c.Constraints))
            .Concat(classes.SelectMany(c => c.Operations).SelectMany(o => o.Constraints))
            .Concat(namedElements.SelectMany(e => e.Constraints));
        var knownElementIds = classes.Select(c => c.Id)
            .Concat(classes.SelectMany(c => c.Properties).Select(p => p.Id))
            .Concat(classes.SelectMany(c => c.Operations).Select(o => o.Id))
            .Concat(classes.SelectMany(c => c.Operations).SelectMany(o => o.Parameters).Select(p => p.Id))
            .Concat(model.Packages.Select(p => p.Id)).Concat(model.Stereotypes.Select(st => st.Id))
            .Concat(model.Constraints.Select(c => c.Id)).Concat(model.Diagrams.Select(d => d.Id))
            .Concat(model.Diagrams.SelectMany(d => d.Nodes).Select(n => n.Id))
            .Concat(model.Enumerations.Select(e => e.Id)).Concat(model.Enumerations.SelectMany(e => e.Literals).Select(l => l.Id))
            .Concat(model.Interfaces.Select(i => i.Id)).Concat(model.Actors.Select(a => a.Id)).Concat(model.UseCases.Select(u => u.Id))
            .Concat(model.Components.Select(c => c.Id)).Concat(model.DeploymentNodes.Select(n => n.Id)).Concat(model.Artifacts.Select(a => a.Id))
            .Concat(model.Signals.Select(s => s.Id)).Concat(model.StateMachines.Select(sm => sm.Id))
            .Concat(model.StateMachines.SelectMany(sm => sm.States).Select(st => st.Id))
            .Concat(model.StateMachines.SelectMany(sm => sm.Transitions).Select(t => t.Id))
            .Concat(model.Activities.Select(a => a.Id)).Concat(model.Activities.SelectMany(a => a.Nodes).Select(n => n.Id))
            .Concat(model.Activities.SelectMany(a => a.Edges).Select(e => e.Id))
            .Concat(model.Interactions.Select(i => i.Id)).Concat(model.Interactions.SelectMany(i => i.Lifelines).Select(l => l.Id))
            .Concat(model.Interactions.SelectMany(i => i.Messages).Select(m => m.Id))
            .Concat(model.Interactions.SelectMany(i => i.CombinedFragments).Select(f => f.Id)).Concat(model.PrimitiveTypes.Select(t => t.Id)).ToHashSet();
        foreach (var constraint in allConstraints)
        {
            if (string.IsNullOrWhiteSpace(constraint.Name)) Add(UmlIssueSeverity.Error, "UMLP0070", constraint.Id.ToString(), "Une contrainte doit avoir un nom.");
            if (string.IsNullOrWhiteSpace(constraint.Expression)) Add(UmlIssueSeverity.Error, "UMLP0071", constraint.Name, "Une contrainte doit avoir une expression.");
            if (constraint.ConstrainedElementId is Guid constrainedId && !knownElementIds.Contains(constrainedId)) Add(UmlIssueSeverity.Warning, "UMLP0074", constraint.Name, $"Élément contraint introuvable : {constrainedId}.");
            if (constraint.Language.Equals("OCL", StringComparison.OrdinalIgnoreCase))
            {
                if (constraint.Expression.Trim() is "true" or "false")
                {
                    var result = OclInterpreter.Evaluate(constraint.Expression, new Dictionary<string, object?>());
                    if (!result.IsSatisfied) Add(UmlIssueSeverity.Error, "UMLP0072", constraint.Name, "La contrainte OCL constante est fausse.");
                }
                else Add(UmlIssueSeverity.Info, "UMLP0073", constraint.Name, "Contrainte OCL enregistrée. Son évaluation complète nécessite un contexte d'instance à l'exécution.");
            }
        }
        ValidateNamedCollection(model.Enumerations, "UMLP0080", Add);
        foreach (var enumeration in model.Enumerations)
        {
            foreach (var group in enumeration.Literals.GroupBy(l => l.Name, StringComparer.Ordinal).Where(g => g.Count() > 1))
                Add(UmlIssueSeverity.Error, "UMLP0081", enumeration.Name, $"Littéral d'énumération dupliqué : {group.Key}.");
        }
        ValidateNamedCollection(model.Interfaces, "UMLP0082", Add);
        ValidateNamedCollection(model.Actors, "UMLP0083", Add);
        ValidateNamedCollection(model.UseCases, "UMLP0084", Add);
        ValidateNamedCollection(model.Components, "UMLP0085", Add);
        ValidateNamedCollection(model.DeploymentNodes, "UMLP0086", Add);
        ValidateNamedCollection(model.Artifacts, "UMLP0087", Add);
        ValidateNamedCollection(model.Signals, "UMLP0088", Add);
        ValidateNamedCollection(model.StateMachines, "UMLP0089", Add);
        ValidateNamedCollection(model.Activities, "UMLP0090", Add);
        ValidateNamedCollection(model.Interactions, "UMLP0091", Add);
        ValidateNamedCollection(model.PrimitiveTypes, "UMLP0092", Add);
        var actorIds = model.Actors.Select(a => a.Id).ToHashSet();
        var useCaseIds = model.UseCases.Select(u => u.Id).ToHashSet();
        foreach (var useCase in model.UseCases)
        {
            foreach (var id in useCase.ActorIds.Where(id => !actorIds.Contains(id))) Add(UmlIssueSeverity.Error, "UMLP0093", useCase.Name, $"Acteur référencé absent : {id}.");
            foreach (var id in useCase.IncludedUseCaseIds.Where(id => !useCaseIds.Contains(id) || id == useCase.Id)) Add(UmlIssueSeverity.Error, "UMLP0094", useCase.Name, $"Cas inclus invalide ou absent : {id}.");
            foreach (var id in useCase.ExtendedUseCaseIds.Where(id => !useCaseIds.Contains(id) || id == useCase.Id)) Add(UmlIssueSeverity.Error, "UMLP0095", useCase.Name, $"Cas étendu invalide ou absent : {id}.");
        }
        foreach (var machine in model.StateMachines)
        {
            var stateIds = machine.States.Select(st => st.Id).ToHashSet();
            if (machine.InitialStateId is Guid initial && (!stateIds.Contains(initial) || !machine.States.Any(st => st.Id == initial && st.StateKind.Equals("Initial", StringComparison.OrdinalIgnoreCase))))
                Add(UmlIssueSeverity.Error, "UMLP0096", machine.Name, "L'état initial doit référencer un pseudo-état Initial présent dans la machine.");
            foreach (var transition in machine.Transitions)
                if (!stateIds.Contains(transition.SourceStateId) || !stateIds.Contains(transition.TargetStateId)) Add(UmlIssueSeverity.Error, "UMLP0097", transition.Name, "La transition référence un état source ou cible absent.");
        }
        foreach (var activity in model.Activities)
        {
            var nodeIds = activity.Nodes.Select(n => n.Id).ToHashSet();
            foreach (var edge in activity.Edges)
                if (!nodeIds.Contains(edge.SourceId) || !nodeIds.Contains(edge.TargetId)) Add(UmlIssueSeverity.Error, "UMLP0098", edge.Name, "Le flux d'activité référence un nœud source ou cible absent.");
            foreach (var node in activity.Nodes.Where(n => n.LowerBound is < 0 || n.UpperBound is < 0 || (n.LowerBound.HasValue && n.UpperBound.HasValue && n.UpperBound < n.LowerBound)))
                Add(UmlIssueSeverity.Error, "UMLP0099", node.Name, "Bornes de cardinalité d'un nœud d'activité incohérentes.");
        }
        foreach (var interaction in model.Interactions)
        {
            var lifelineIds = interaction.Lifelines.Select(l => l.Id).ToHashSet();
            foreach (var message in interaction.Messages)
                if (!lifelineIds.Contains(message.SenderLifelineId) || !lifelineIds.Contains(message.ReceiverLifelineId)) Add(UmlIssueSeverity.Error, "UMLP0100", message.Name, "Le message référence une ligne de vie absente.");
            foreach (var group in interaction.Messages.GroupBy(m => m.Order).Where(g => g.Key < 0 || g.Count() > 1))
                Add(UmlIssueSeverity.Warning, "UMLP0101", interaction.Name, $"Ordre de message invalide ou ambigu : {group.Key}.");
        }
        foreach (var report in model.Diagrams.Select(UmlValidationService.Validate))
            issues.AddRange(report.Issues);
        return issues;
    }

    private static void ValidateNamedCollection<T>(IEnumerable<T> elements, string ruleId, Action<UmlIssueSeverity, string, string, string> add) where T : UmlNamedElement
    {
        foreach (var element in elements)
        {
            if (string.IsNullOrWhiteSpace(element.Name)) add(UmlIssueSeverity.Error, ruleId, element.Id.ToString(), "Un élément UML doit avoir un nom.");
            foreach (var group in element.TaggedValues.Keys.GroupBy(k => k, StringComparer.Ordinal).Where(g => g.Count() > 1))
                add(UmlIssueSeverity.Error, ruleId, element.Name, $"Valeur taguée dupliquée : {group.Key}.");
        }
    }

    private static IEnumerable<string> FindGeneralizationCycles(IReadOnlyCollection<UmlClass> classes, IEnumerable<UmlRelation> relations)
    {
        var graph = relations.Where(r => r.Kind.Equals("Generalization", StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => r.FromId).ToDictionary(g => g.Key, g => g.Select(r => r.ToId).ToArray());
        var visited = new HashSet<Guid>(); var active = new HashSet<Guid>(); var cycles = new HashSet<string>(StringComparer.Ordinal);
        var names = classes.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Name);
        foreach (var item in classes) Visit(item.Id, new List<Guid>());
        return cycles;
        void Visit(Guid id, List<Guid> path)
        {
            if (active.Contains(id))
            {
                var start = path.IndexOf(id); var cycle = (start >= 0 ? path.Skip(start) : path).Append(id).Select(x => names.GetValueOrDefault(x, x.ToString()));
                cycles.Add(string.Join(" → ", cycle)); return;
            }
            if (!visited.Add(id)) return;
            active.Add(id); path.Add(id);
            if (graph.TryGetValue(id, out var next)) foreach (var target in next) Visit(target, path);
            path.RemoveAt(path.Count - 1); active.Remove(id);
        }
    }

    private static bool IsVisibility(string value) => value is "public" or "private" or "protected" or "internal" or "package" or "+" or "-" or "#" or "~";
    private static bool IsMultiplicity(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        value = value.Trim(); if (value is "*" or "0..*" or "1..*" or "0..1" or "1") return true;
        var parts = value.Split("..", StringSplitOptions.TrimEntries);
        return parts.Length == 2 && int.TryParse(parts[0], out var min) && (parts[1] == "*" || int.TryParse(parts[1], out var max) && max >= min) && min >= 0;
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Namespace, string Name)>
    {
        public static readonly StringTupleComparer OrdinalIgnoreCase = new();
        public bool Equals((string Namespace, string Name) x, (string Namespace, string Name) y) => StringComparer.OrdinalIgnoreCase.Equals(x.Namespace, y.Namespace) && StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name);
        public int GetHashCode((string Namespace, string Name) obj) => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Namespace), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name));
    }
}
