namespace UMLForge.Services;

public sealed record UmlMetaclassDescriptor(string Name, string Package, string Purpose, string Status, string[] RelatedMetaclasses);

/// <summary>
/// Navigable inventory of major UML 2.5.1 metaclass families used to drive coverage and validation work.
/// This catalog is a coverage manifest, not an OMG-generated normative metamodel implementation.
/// </summary>
public static class UmlMetamodelCatalog
{
    private static readonly string[] Core = "Element|Comment|Relationship|DirectedRelationship|NamedElement|Namespace|PackageableElement|Type|TypedElement|MultiplicityElement|ParameterableElement|TemplateableElement|TemplateSignature|TemplateParameter|TemplateBinding|Constraint|ValueSpecification|LiteralBoolean|LiteralInteger|LiteralReal|LiteralString|LiteralNull|LiteralUnlimitedNatural|Expression|OpaqueExpression|Duration|Interval|TimeExpression|DurationConstraint|TimeConstraint|Observation|TimeObservation|DurationObservation".Split('|');
    private static readonly string[] Classes = "Classifier|Class|Interface|DataType|PrimitiveType|Enumeration|EnumerationLiteral|Signal|Association|AssociationClass|Property|Port|Connector|ConnectorEnd|Operation|Parameter|Reception|BehavioralFeature|StructuralFeature|Generalization|Realization|Substitution|Dependency|Abstraction|Usage|InterfaceRealization|Feature|RedefinableElement|ParameterSet|Collaboration|CollaborationUse|CollaborationRole|AssociationEnd".Split('|');
    private static readonly string[] Activities = "Activity|ActivityNode|ExecutableNode|Action|OpaqueAction|CallAction|CallOperationAction|CallBehaviorAction|SendSignalAction|AcceptEventAction|StructuralFeatureAction|ReadStructuralFeatureAction|WriteStructuralFeatureAction|CreateObjectAction|DestroyObjectAction|StartObjectBehaviorAction|TestIdentityAction|RaiseExceptionAction|SequenceNode|ConditionalNode|LoopNode|ExpansionRegion|ExpansionNode|InputPin|OutputPin|ValuePin|ControlNode|InitialNode|ActivityFinalNode|FlowFinalNode|DecisionNode|MergeNode|ForkNode|JoinNode|ObjectNode|CentralBufferNode|DataStoreNode|ActivityParameterNode|ActivityEdge|ControlFlow|ObjectFlow|InterruptibleActivityRegion|ExceptionHandler|Variable|ActivityPartition".Split('|');
    private static readonly string[] StateMachines = "StateMachine|Region|Vertex|State|FinalState|Pseudostate|Transition|Trigger|StateInvariant|ProtocolStateMachine|ProtocolConformance|ConnectionPointReference|Behavior|OpaqueBehavior|FunctionBehavior|Event|CallEvent|SignalEvent|TimeEvent|ChangeEvent|AnyReceiveEvent|Reception".Split('|');
    private static readonly string[] Interactions = "Interaction|InteractionFragment|InteractionOperand|CombinedFragment|InteractionUse|Lifeline|Message|MessageEnd|Gate|GeneralOrdering|OccurrenceSpecification|ExecutionSpecification|ActionExecutionSpecification|BehaviorExecutionSpecification|DestructionOccurrenceSpecification|ExecutionOccurrenceSpecification|StateInvariant".Split('|');
    private static readonly string[] UseCases = "UseCase|Actor|Extension|Extend|Include|ExtensionPoint".Split('|');
    private static readonly string[] Components = "Component|ComponentRealization|Artifact|Manifestation|Deployment|DeploymentSpecification|DeployedArtifact|Node|Device|ExecutionEnvironment|CommunicationPath|DeploymentTarget|InstanceSpecification|Slot|InstanceValue".Split('|');
    private static readonly string[] Packages = "Package|Profile|Stereotype|Extension|PackageImport|ElementImport|PackageMerge|Model|ModelLibrary|ProfileApplication|EnumerationLiteral|Image".Split('|');

    private static readonly IReadOnlyList<UmlMetaclassDescriptor> Entries = Build();
    public static IReadOnlyList<UmlMetaclassDescriptor> All => Entries;
    public static IReadOnlyDictionary<string, int> CoverageByPackage => Entries.GroupBy(x => x.Package).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
    public static IEnumerable<UmlMetaclassDescriptor> Search(string query) => string.IsNullOrWhiteSpace(query) ? Entries : Entries.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Package.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Purpose.Contains(query, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<UmlMetaclassDescriptor> Build()
    {
        var result = new List<UmlMetaclassDescriptor>();
        Add("Kernel", Core);
        Add("Classes", Classes);
        Add("Activities", Activities);
        Add("StateMachines", StateMachines);
        Add("Interactions", Interactions);
        Add("UseCases", UseCases);
        Add("ComponentsAndDeployment", Components);
        Add("PackagesAndProfiles", Packages);
        Add("Actions", "ActionInputPin|CallBehaviorAction|CallOperationAction|SendObjectAction|SendSignalAction|BroadcastSignalAction|ReplyAction|UnmarshallAction|ValueSpecificationAction|ReadExtentAction|ReadIsClassifiedObjectAction|ReclassifyObjectAction|ReduceAction|StartClassifierBehaviorAction|StartObjectBehaviorAction|StructuralFeatureAction|ReadLinkAction|ReadLinkObjectEndAction|ReadLinkObjectEndQualifierAction|WriteLinkAction|WriteLinkObjectEndAction|WriteLinkObjectEndQualifierAction".Split('|'));
        Add("InformationFlows", "InformationFlow|InformationItem|InformationRealization|InformationSource|InformationTarget".Split('|'));
        Add("Deployments", "DeployedArtifact|Deployment|DeploymentSpecification|DeploymentTarget|Device|ExecutionEnvironment|Node".Split('|'));
        Add("Templates", "TemplateableElement|TemplateSignature|TemplateParameter|TemplateParameterSubstitution|TemplateBinding|ClassifierTemplateParameter|OperationTemplateParameter|RedefinableTemplateSignature".Split('|'));
        Add("ActivitiesAndStructuredNodes", "StructuredActivityNode|ConditionalNode|LoopNode|ExpansionRegion|ExpansionNode|InterruptibleActivityRegion|ExceptionHandler|ActivityGroup|ActivityPartition|Variable|VariableAction".Split('|'));
        Add("CommonBehaviorsAndEvents", "Behavior|BehavioralFeature|CallEvent|ChangeEvent|SignalEvent|TimeEvent|AnyReceiveEvent|Trigger|Event|Reception|FunctionBehavior|OpaqueBehavior|InvocationAction|InvocationOnNestedClassifierAction|StartClassifierBehaviorAction|StartObjectBehaviorAction|TerminateAction|UnmarshallAction|ReplyAction|BroadcastSignalAction|SendObjectAction|SendSignalAction".Split('|'));
        Add("StructuredClassifiers", "StructuredClassifier|EncapsulatedClassifier|BehavioredClassifier|Classifier|Collaboration|CollaborationUse|CollaborationRole|Connector|ConnectorEnd|Port|ConnectableElement|ConnectableElementTemplateParameter|RedefinableTemplateSignature|ClassifierTemplateParameter|OperationTemplateParameter".Split('|'));
        Add("AssociationsAndDependencies", "Association|AssociationClass|AssociationEnd|Extension|ExtensionEnd|MemberEnd|EndType|Dependency|Abstraction|Usage|Realization|InterfaceRealization|Substitution|PackageImport|ElementImport|PackageMerge|InformationFlow|InformationItem|InformationRealization".Split('|'));
        Add("ValuesAndTime", "LiteralSpecification|LiteralBoolean|LiteralInteger|LiteralReal|LiteralString|LiteralNull|LiteralUnlimitedNatural|Expression|OpaqueExpression|InstanceValue|InstanceSpecification|Slot|Duration|DurationInterval|TimeInterval|Interval|TimeExpression|DurationObservation|TimeObservation|TimeConstraint|DurationConstraint|Observation|ObservationLink|OpaqueBehavior".Split('|'));
        Add("ProfilesAndExtension", "Profile|Stereotype|Extension|ExtensionEnd|ProfileApplication|Image|Model|ModelLibrary|TemplateBinding|TemplateParameter|TemplateParameterSubstitution|TemplateSignature|TemplateableElement".Split('|'));
        return result.GroupBy(x => x.Name, StringComparer.Ordinal).Select(g => g.First()).OrderBy(x => x.Package).ThenBy(x => x.Name).ToArray();

        void Add(string package, IEnumerable<string> names)
        {
            foreach (var name in names)
            {
                var status = name switch
                {
                    "Class" or "Property" or "Operation" or "Parameter" or "Association" or "Generalization" or "Dependency" or "Package" or "Constraint" or "Enumeration" or "EnumerationLiteral" or "Interface" or "Actor" or "UseCase" or "StateMachine" or "State" or "Transition" or "Activity" or "ActivityNode" or "ActivityEdge" or "Interaction" or "Lifeline" or "Message" or "Component" or "Artifact" or "Node" or "Deployment" or "Stereotype" or "Profile" => "Modèle sémantique présent; validation partielle",
                    _ => "Catalogue de couverture; représentation/validation dédiée à compléter"
                };
                result.Add(new UmlMetaclassDescriptor(name, package, Describe(name), status, Related(name)));
            }
        }
    }
    private static string Describe(string name) => name switch
    {
        "Class" => "Type structurel avec attributs, opérations et relations.",
        "Constraint" => "Contrainte attachée à un élément du modèle; expressions OCL prises en charge dans un sous-ensemble sûr.",
        "Activity" => "Comportement modélisé par des nœuds et des flux.",
        "Transition" => "Passage entre états avec déclencheur, garde et effet.",
        "Message" => "Communication ordonnée entre lignes de vie.",
        "Stereotype" => "Extension du modèle via une métaclasse et des valeurs taguées.",
        _ => $"Métaclasse UML de la famille {name}."
    };
    private static string[] Related(string name) => name switch
    {
        "Class" => new[] { "Classifier", "Property", "Operation", "Generalization", "Association" },
        "Activity" => new[] { "ActivityNode", "ActivityEdge", "Action", "ControlFlow", "ObjectFlow" },
        "StateMachine" => new[] { "Region", "State", "Pseudostate", "Transition" },
        "Interaction" => new[] { "Lifeline", "Message", "CombinedFragment", "InteractionUse" },
        "Component" => new[] { "Port", "InterfaceRealization", "Artifact", "Deployment" },
        _ => Array.Empty<string>()
    };
}
