namespace UMLForge.Services;

public sealed record UmlDiagramDefinition(string Type, string DisplayName, string Purpose, IReadOnlyList<string> NodeKinds, IReadOnlyList<string> RelationKinds, IReadOnlyList<string> ValidationRules, string ExportCoverage);

/// <summary>Central registry of diagram-specific UML concepts and expected relationships.</summary>
public static class UmlNotationCatalog
{
    private static readonly IReadOnlyDictionary<string, UmlDiagramDefinition> Definitions = new Dictionary<string, UmlDiagramDefinition>(StringComparer.OrdinalIgnoreCase)
    {
        ["Class"] = D("Class", "Classes", "Structure statique : types, attributs, opérations et relations", "Class|Interface|Enumeration|Package|Note", "Association|Aggregation|Composition|Generalization|Realization|Dependency", "membres typés|visibilité|multiplicités|généralisation acyclique", "Partiel : classes, interfaces, énumérations et relations de base"),
        ["UseCase"] = D("UseCase", "Cas d'utilisation", "Acteurs, objectifs fonctionnels et frontières du système", "Actor|UseCase|Boundary|Note", "Association|Include|Extend|Generalization|Dependency", "acteurs et cas d'utilisation|relations include/extend|frontière du système", "Partiel : acteurs, use cases et liens"),
        ["Sequence"] = D("Sequence", "Séquence", "Lignes de vie et messages ordonnés dans le temps", "Actor|Lifeline|Message|Boundary|Control|Entity|Note", "Message|Return|Create|Destroy|SelfMessage", "lignes de vie distinctes|ordre des messages|garde|fragments combinés", "Partiel : participants et liens; fragments UML avancés à développer"),
        ["Activity"] = D("Activity", "Activité", "Flux de contrôle et d'objets", "Start|End|Action|Decision|Merge|Fork|Join|ObjectNode|SendSignal|AcceptEvent|Note", "ControlFlow|ObjectFlow|ExceptionFlow", "nœud initial/final|gardes de décision|synchronisation fork/join", "Partiel : nœuds de base et flux"),
        ["Component"] = D("Component", "Composants", "Composants, interfaces et dépendances d'architecture", "Component|Interface|Port|ProvidedInterface|RequiredInterface|Artifact|Note", "Dependency|Realization|AssemblyConnector|DelegationConnector|Association", "interfaces fournies/requises|ports|dépendances", "Partiel : composants et dépendances simples"),
        ["Deployment"] = D("Deployment", "Déploiement", "Nœuds d'exécution, artefacts et chemins de communication", "Node|Device|ExecutionEnvironment|Artifact|Component|Database|CommunicationPath|Note", "Deployment|Dependency|Association|CommunicationPath", "artefacts déployés|nœuds|environnements d'exécution", "Partiel : nœuds, artefacts et liens"),
        ["State"] = D("State", "États", "États, transitions et pseudo-états", "Start|End|State|CompositeState|Choice|Junction|Fork|Join|EntryPoint|ExitPoint|HistoryState|Note", "Transition|InternalTransition", "état initial/final|événement|garde|effet|historique", "Partiel : états et transitions simples"),
        ["Package"] = D("Package", "Paquetages", "Organisation logique, import et fusion de paquetages", "Package|Class|Interface|Note", "PackageImport|PackageMerge|Dependency|Nesting", "noms qualifiés|imports|dépendances cycliques", "Partiel : paquetages et dépendances"),
        ["Object"] = D("Object", "Objets", "Instances et liens à un instant donné", "Object|Link|Note", "Link|Association", "nom instance:Type|valeurs d'attributs|liens cohérents", "Partiel : objets et liens"),
        ["Communication"] = D("Communication", "Communication", "Interactions centrées sur les liens entre participants", "Actor|Object|Lifeline|Message|Note", "Message|Link", "numérotation des messages|participants|ordre logique", "Partiel : objets et messages"),
        ["Timing"] = D("Timing", "Temps", "Évolution des états et contraintes temporelles", "Lifeline|StateInvariant|TimeConstraint|DurationConstraint|Message|TimeObservation|DurationObservation", "TimeObservation|DurationObservation|Message", "axes temporels|contraintes de durée|observations", "Prototype : notations temporelles avancées incomplètes"),
        ["CompositeStructure"] = D("CompositeStructure", "Structure composite", "Parties internes, ports et connecteurs", "Class|Part|Port|Connector|Collaboration|Role|Note", "Connector|DelegationConnector|Association", "parts|ports|connecteurs|rôles", "Prototype : structure interne à enrichir"),
        ["InteractionOverview"] = D("InteractionOverview", "Vue d'interaction", "Coordination d'interactions sous forme de flux", "Start|End|InteractionUse|Decision|Merge|Fork|Join|Note", "ControlFlow", "nœud initial/final|références aux interactions|gardes", "Prototype : interactions référencées à développer"),
        ["Profile"] = D("Profile", "Profils", "Métamodélisation légère : stéréotypes, métaclasses et extensions", "Profile|Stereotype|Metaclass|Extension|Note", "Extension|Generalization", "stéréotypes|métaclasses étendues|extensions", "Prototype : profil UML partiel")
    };

    public static IReadOnlyCollection<UmlDiagramDefinition> All => Definitions.Values.OrderBy(d => d.Type).ToArray();
    public static bool TryGet(string type, out UmlDiagramDefinition definition) => Definitions.TryGetValue(type, out definition!);
    public static UmlDiagramDefinition? Get(string type) => Definitions.GetValueOrDefault(type);
    public static string[] GetNodeKinds(string type) => Get(type)?.NodeKinds.ToArray() ?? new[] { "Note" };
    public static string[] GetRelationKinds(string type) => Get(type)?.RelationKinds.ToArray() ?? new[] { "Dependency", "Association" };

    private static UmlDiagramDefinition D(string type, string name, string purpose, string nodes, string links, string rules, string coverage) =>
        new(type, name, purpose, nodes.Split('|'), links.Split('|'), rules.Split('|'), coverage);
}
