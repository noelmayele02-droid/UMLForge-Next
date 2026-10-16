using System.Collections.ObjectModel;

namespace UMLForge.Models;

public sealed class UmlModel
{
    public string Name { get; set; } = "Nouveau projet";
    public string TargetFramework { get; set; } = "net10.0";
    public string LanguageVersion { get; set; } = "14.0";
    public ObservableCollection<UmlClass> Classes { get; set; } = new();
    public ObservableCollection<UmlRelation> Relations { get; set; } = new();
    public ObservableCollection<UmlDiagram> Diagrams { get; set; } = new();
    // Persisted semantic elements complement the diagram canvas and provide stable IDs for UML↔code mapping.
    public ObservableCollection<UmlPackage> Packages { get; set; } = new();
    public ObservableCollection<UmlStereotype> Stereotypes { get; set; } = new();
    public ObservableCollection<UmlConstraint> Constraints { get; set; } = new();
    public ObservableCollection<UmlEnumeration> Enumerations { get; set; } = new();
    public ObservableCollection<UmlInterface> Interfaces { get; set; } = new();
    public ObservableCollection<UmlActor> Actors { get; set; } = new();
    public ObservableCollection<UmlUseCase> UseCases { get; set; } = new();
    public ObservableCollection<UmlComponent> Components { get; set; } = new();
    public ObservableCollection<UmlDeploymentNode> DeploymentNodes { get; set; } = new();
    public ObservableCollection<UmlArtifact> Artifacts { get; set; } = new();
    public ObservableCollection<UmlSignal> Signals { get; set; } = new();
    public ObservableCollection<UmlStateMachine> StateMachines { get; set; } = new();
    public ObservableCollection<UmlActivity> Activities { get; set; } = new();
    public ObservableCollection<UmlInteraction> Interactions { get; set; } = new();
    public ObservableCollection<UmlPrimitiveType> PrimitiveTypes { get; set; } = new();
    public ObservableCollection<UmlCodeMapping> CodeMappings { get; set; } = new();
    public int SchemaVersion { get; set; } = 2;
    public string UmlSpecificationVersion { get; set; } = "2.5.1";
    public string? LastOpenedProjectPath { get; set; }
}

public sealed class UmlClass
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "NouvelleClasse";
    public string Namespace { get; set; } = "Generated";
    public string Kind { get; set; } = "class";
    public string Accessibility { get; set; } = "public";
    public string? BaseType { get; set; }
    public double X { get; set; } = 80;
    public double Y { get; set; } = 80;
    public List<string> Attributes { get; set; } = new() { "Id: Guid" };
    public List<string> Methods { get; set; } = new() { "ToString(): string" };
    public bool IsAbstract { get; set; }
    public bool IsStatic { get; set; }
    public bool IsSealed { get; set; }
    public bool IsPartial { get; set; }
    public List<UmlProperty> Properties { get; set; } = new();
    public List<UmlOperation> Operations { get; set; } = new();
    public List<UmlConstraint> Constraints { get; set; } = new();
    public Dictionary<string, string> TaggedValues { get; set; } = new(StringComparer.Ordinal);
}

public sealed class UmlRelation
{
    public Guid FromId { get; set; }
    public Guid ToId { get; set; }
    public string Kind { get; set; } = "Association";
    public string Label { get; set; } = "";
    public string SourceMultiplicity { get; set; } = "1";
    public string TargetMultiplicity { get; set; } = "*";
}


public sealed class UmlDiagram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Nouveau diagramme";
    public string Type { get; set; } = "UseCase";
    public string? Profile { get; set; }
    public string? Documentation { get; set; }
    public bool SnapToGrid { get; set; } = true;
    public int GridSize { get; set; } = 10;
    public string LayoutEngine { get; set; } = "Manual";
    public List<UmlDiagramNode> Nodes { get; set; } = new();
    public List<UmlDiagramLink> Links { get; set; } = new();
}

public sealed class UmlDiagramNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Élément";
    public string Kind { get; set; } = "Action";
    public string Details { get; set; } = "";
    public string? Stereotype { get; set; }
    public string Visibility { get; set; } = "public";
    public string? GuardCondition { get; set; }
    public string? Constraint { get; set; }
    public string? Multiplicity { get; set; }
    public string? Duration { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsStatic { get; set; }
    public double X { get; set; } = 80;
    public double Y { get; set; } = 80;
}

public sealed class UmlDiagramLink
{
    public Guid FromId { get; set; }
    public Guid ToId { get; set; }
    public string Label { get; set; } = "";
    public string Kind { get; set; } = "Association";
    public string? SourceRole { get; set; }
    public string? TargetRole { get; set; }
    public bool IsNavigable { get; set; } = true;
    public string? GuardCondition { get; set; }
}


/// <summary>Semantic UML package with a persistent identity and qualified name.</summary>
public sealed class UmlPackage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Package";
    public Guid? ParentId { get; set; }
    public List<Guid> OwnedClassIds { get; set; } = new();
    public List<Guid> ImportedPackageIds { get; set; } = new();
}

public sealed class UmlProperty
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Property";
    public string Type { get; set; } = "object";
    public string Visibility { get; set; } = "private";
    public string Multiplicity { get; set; } = "1";
    public bool IsReadOnly { get; set; }
    public bool IsStatic { get; set; }
    public string? DefaultValue { get; set; }
}

public sealed class UmlOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Operation";
    public string ReturnType { get; set; } = "void";
    public string Visibility { get; set; } = "public";
    public bool IsAbstract { get; set; }
    public bool IsStatic { get; set; }
    public bool IsAsync { get; set; }
    public List<UmlParameter> Parameters { get; set; } = new();
    public List<UmlConstraint> Constraints { get; set; } = new();
}

public sealed class UmlParameter
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "parameter";
    public string Type { get; set; } = "object";
    public string Direction { get; set; } = "in";
    public string? DefaultValue { get; set; }
}

public sealed class UmlConstraint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Constraint";
    public string Language { get; set; } = "OCL";
    public string Expression { get; set; } = "true";
    public string Severity { get; set; } = "Error";
    public Guid? ConstrainedElementId { get; set; }
}

public sealed class UmlStereotype
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Stereotype";
    public string ExtendedMetaclass { get; set; } = "Class";
    public Dictionary<string, string> TaggedValueTypes { get; set; } = new(StringComparer.Ordinal);
}


// Additional persistent semantic metaclasses used by the UML project model.
// Diagram canvas nodes remain presentation elements; these records represent model semantics.
public abstract class UmlNamedElement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Element";
    public string Namespace { get; set; } = "";
    public string? Documentation { get; set; }
    public string? Stereotype { get; set; }
    public Dictionary<string, string> TaggedValues { get; set; } = new(StringComparer.Ordinal);
    public List<UmlConstraint> Constraints { get; set; } = new();
}

public sealed class UmlEnumeration : UmlNamedElement
{
    public List<UmlEnumerationLiteral> Literals { get; set; } = new();
    public bool IsFlags { get; set; }
}
public sealed class UmlEnumerationLiteral : UmlNamedElement { public string? Value { get; set; } }
public sealed class UmlInterface : UmlNamedElement
{
    public List<UmlOperation> Operations { get; set; } = new();
    public List<UmlProperty> Properties { get; set; } = new();
    public List<Guid> GeneralizationIds { get; set; } = new();
}
public sealed class UmlActor : UmlNamedElement { public bool IsAbstract { get; set; } public List<Guid> GeneralizationIds { get; set; } = new(); }
public sealed class UmlUseCase : UmlNamedElement
{
    public List<Guid> ActorIds { get; set; } = new();
    public List<Guid> IncludedUseCaseIds { get; set; } = new();
    public List<Guid> ExtendedUseCaseIds { get; set; } = new();
    public string? ExtensionPoint { get; set; }
}
public sealed class UmlComponent : UmlNamedElement
{
    public List<UmlInterface> ProvidedInterfaces { get; set; } = new();
    public List<UmlInterface> RequiredInterfaces { get; set; } = new();
    public List<string> Ports { get; set; } = new();
}
public sealed class UmlDeploymentNode : UmlNamedElement
{
    public string NodeKind { get; set; } = "Node";
    public List<Guid> DeployedArtifactIds { get; set; } = new();
    public List<Guid> NestedNodeIds { get; set; } = new();
}
public sealed class UmlArtifact : UmlNamedElement
{
    public string ArtifactKind { get; set; } = "File";
    public string? FilePath { get; set; }
    public List<Guid> ManifestedComponentIds { get; set; } = new();
}
public sealed class UmlSignal : UmlNamedElement { public List<UmlProperty> Attributes { get; set; } = new(); }
public sealed class UmlStateMachine : UmlNamedElement
{
    public List<UmlState> States { get; set; } = new();
    public List<UmlTransition> Transitions { get; set; } = new();
    public Guid? InitialStateId { get; set; }
}
public sealed class UmlState : UmlNamedElement
{
    public string StateKind { get; set; } = "State";
    public string? EntryBehavior { get; set; }
    public string? DoBehavior { get; set; }
    public string? ExitBehavior { get; set; }
    public bool IsComposite { get; set; }
    public List<UmlState> Regions { get; set; } = new();
}
public sealed class UmlTransition : UmlNamedElement
{
    public Guid SourceStateId { get; set; }
    public Guid TargetStateId { get; set; }
    public string? Trigger { get; set; }
    public string? Guard { get; set; }
    public string? Effect { get; set; }
}
public sealed class UmlActivity : UmlNamedElement
{
    public List<UmlActivityNode> Nodes { get; set; } = new();
    public List<UmlActivityEdge> Edges { get; set; } = new();
}
public sealed class UmlActivityNode : UmlNamedElement
{
    public string NodeKind { get; set; } = "Action";
    public string? Guard { get; set; }
    public int? LowerBound { get; set; }
    public int? UpperBound { get; set; }
}
public sealed class UmlActivityEdge : UmlNamedElement
{
    public Guid SourceId { get; set; }
    public Guid TargetId { get; set; }
    public string EdgeKind { get; set; } = "ControlFlow";
    public string? Guard { get; set; }
}
public sealed class UmlInteraction : UmlNamedElement
{
    public List<UmlLifeline> Lifelines { get; set; } = new();
    public List<UmlMessage> Messages { get; set; } = new();
    public List<UmlCombinedFragment> CombinedFragments { get; set; } = new();
}
public sealed class UmlLifeline : UmlNamedElement { public string? Represents { get; set; } }
public sealed class UmlMessage : UmlNamedElement
{
    public Guid SenderLifelineId { get; set; }
    public Guid ReceiverLifelineId { get; set; }
    public string MessageKind { get; set; } = "SynchCall";
    public string? Signature { get; set; }
    public int Order { get; set; }
    public string? Guard { get; set; }
}
public sealed class UmlCombinedFragment : UmlNamedElement
{
    public string Operator { get; set; } = "alt";
    public List<string> Operands { get; set; } = new();
}
public sealed class UmlPrimitiveType : UmlNamedElement { public string? CSharpTypeName { get; set; } }


/// <summary>Persistent identity bridge between a UML element GUID and its current Roslyn source symbol.</summary>
public sealed class UmlCodeMapping
{
    public Guid UmlElementId { get; set; }
    public string UmlElementName { get; set; } = "";
    public string ElementKind { get; set; } = "Class";
    public string FullyQualifiedName { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string SymbolFingerprint { get; set; } = "";
    public string Status { get; set; } = "Unresolved";
    public DateTimeOffset LastMappedUtc { get; set; }
}
