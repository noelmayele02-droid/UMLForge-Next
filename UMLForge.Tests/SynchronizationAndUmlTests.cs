using UMLForge.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using UMLForge.Services;
using Xunit;

namespace UMLForge.Tests;

public sealed class SynchronizationAndUmlTests
{
    [Fact]
    public void ThreeWayMerge_CombinesIndependentMethodEdits()
    {
        const string baseline = "namespace Demo; public class Sample { public int A() => 1; public int B() => 2; }";
        const string current = "namespace Demo; public class Sample { public int A() => 10; public int B() => 2; }";
        const string generated = "namespace Demo; public class Sample { public int A() => 1; public int B() => 20; }";
        var result = CSharpThreeWayMergeService.Merge(baseline, current, generated);
        Assert.True(result.Success);
        Assert.Contains("A() => 10", result.MergedCode);
        Assert.Contains("B() => 20", result.MergedCode);
    }

    [Fact]
    public void ThreeWayMerge_RefusesConflictingEditToSameMethod()
    {
        const string baseline = "public class Sample { public int A() => 1; }";
        const string current = "public class Sample { public int A() => 2; }";
        const string generated = "public class Sample { public int A() => 3; }";
        var result = CSharpThreeWayMergeService.Merge(baseline, current, generated);
        Assert.False(result.Success);
        Assert.NotEmpty(result.Conflicts);
    }

    [Fact]
    public void ThreeWayMerge_AcceptsOneSidedDeletionOfUnmodifiedMember()
    {
        const string baseline = "public class Sample { public int A() => 1; public int B() => 2; }";
        const string current = baseline;
        const string generated = "public class Sample { public int A() => 1; }";
        var result = CSharpThreeWayMergeService.Merge(baseline, current, generated);
        Assert.True(result.Success);
        Assert.DoesNotContain("B()", result.MergedCode);
    }

    [Fact]
    public void UmlValidation_WarnsWhenUseCaseDiagramHasNoActor()
    {
        var diagram = new UmlDiagram { Name = "Connexion", Type = "UseCase" };
        diagram.Nodes.Add(new UmlDiagramNode { Name = "Se connecter", Kind = "UseCase" });
        var report = UmlValidationService.Validate(diagram);
        Assert.Contains(report.Issues, i => i.Message.Contains("acteur", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UmlCatalog_ContainsFourteenDiagramFamilies()
    {
        Assert.Equal(14, UmlNotationCatalog.All.Count);
        Assert.True(UmlNotationCatalog.TryGet("Sequence", out var sequence));
        Assert.Contains("Lifeline", sequence.NodeKinds);
        Assert.Contains("Message", sequence.NodeKinds);
    }
    [Fact]
    public void UmlProjectValidation_DetectsGeneralizationCycleAndInvalidPropertyMultiplicity()
    {
        var a = new UmlClass { Name = "Alpha" };
        var b = new UmlClass { Name = "Beta" };
        var model = new UmlModel();
        model.Classes.Add(a); model.Classes.Add(b);
        model.Relations.Add(new UmlRelation { FromId = a.Id, ToId = b.Id, Kind = "Generalization" });
        model.Relations.Add(new UmlRelation { FromId = b.Id, ToId = a.Id, Kind = "Generalization" });
        a.Properties.Add(new UmlProperty { Name = "Children", Type = "Beta", Multiplicity = "4..2" });
        var issues = UmlProjectValidationService.Validate(model);
        Assert.Contains(issues, i => i.RuleId == "UMLP0043");
        Assert.Contains(issues, i => i.RuleId == "UMLP0024");
    }

    [Fact]
    public void UmlProjectValidation_DetectsDuplicateTypeNamesInNamespace()
    {
        var model = new UmlModel();
        model.Classes.Add(new UmlClass { Name = "Customer", Namespace = "Demo" });
        model.Classes.Add(new UmlClass { Name = "customer", Namespace = "demo" });
        Assert.Contains(UmlProjectValidationService.Validate(model), i => i.RuleId == "UMLP0001");
    }

    [Fact]
    public void SynchronizationPlan_TracksGeneratedFileDeletionAsExplicitConflict()
    {
        var root = Path.Combine(Path.GetTempPath(), "UMLForgeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "Sample.cs"), "public class Sample { }");
            var plan = SynchronizationEngine.CreatePlan(root, new Dictionary<string, string>(), new Dictionary<string, string> { ["Sample.cs"] = "public class Sample { }" });
            var file = Assert.Single(plan.Files);
            Assert.Equal("Sample.cs", file.RelativePath);
            Assert.Null(file.GeneratedCode);
            Assert.True(file.HasConflict);
            Assert.Contains(plan.Conflicts, c => c.Kind == SyncConflictKind.GeneratedDeletion);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void SynchronizationPlan_FlagsPotentialTypeRenameAcrossPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "UMLForgeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "Old.cs"), "public class Sample { }");
            var plan = SynchronizationEngine.CreatePlan(root,
                new Dictionary<string, string> { ["New.cs"] = "public class Sample { }" },
                new Dictionary<string, string> { ["Old.cs"] = "public class Sample { }" });
            Assert.Equal("Old.cs", plan.Files.Single(f => f.RelativePath == "New.cs").PotentialRenameFrom);
            Assert.Equal("New.cs", plan.Files.Single(f => f.RelativePath == "Old.cs").PotentialRenameTo);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SynchronizationApply_DeletionRequiresExplicitDecisionAndCreatesBackup()
    {
        var root = Path.Combine(Path.GetTempPath(), "UMLForgeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var text = "public class Sample { }";
            File.WriteAllText(Path.Combine(root, "Sample.cs"), text);
            var plan = SynchronizationEngine.CreatePlan(root, new Dictionary<string, string>(), new Dictionary<string, string> { ["Sample.cs"] = text });
            await SynchronizationEngine.ApplyPlanAsync(plan, new Dictionary<string, SyncDecision> { ["Sample.cs"] = SyncDecision.AcceptGeneratedDeletion }, Path.Combine(root, ".umlforge", "history"));
            Assert.False(File.Exists(Path.Combine(root, "Sample.cs")));
            Assert.NotEmpty(Directory.GetFiles(Path.Combine(root, ".umlforge", "history"), "Sample.cs", SearchOption.AllDirectories));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void SynchronizationPlan_PreservesManualOnlyChangeByDefault()
    {
        var root = Path.Combine(Path.GetTempPath(), "UMLForgeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string baselineText = "public class Sample { public int Value => 1; }";
            const string manualText = "public class Sample { public int Value => 42; }";
            File.WriteAllText(Path.Combine(root, "Sample.cs"), manualText);
            var plan = SynchronizationEngine.CreatePlan(root, new Dictionary<string, string> { ["Sample.cs"] = baselineText }, new Dictionary<string, string> { ["Sample.cs"] = baselineText });
            Assert.Equal(SyncDecision.KeepCurrentCode, Assert.Single(plan.Files).RecommendedDecision);
            Assert.Empty(plan.Conflicts);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SynchronizationApply_PreflightsAllFilesBeforeWritingAny()
    {
        var root = Path.Combine(Path.GetTempPath(), "UMLForgeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string original = "public class Sample { }";
            File.WriteAllText(Path.Combine(root, "A.cs"), original);
            File.WriteAllText(Path.Combine(root, "B.cs"), original);
            var baseline = new Dictionary<string, string> { ["A.cs"] = original, ["B.cs"] = original };
            var generated = new Dictionary<string, string> { ["A.cs"] = "public class Sample { public int A => 1; }", ["B.cs"] = "public class Sample { public int B => 2; }" };
            var plan = SynchronizationEngine.CreatePlan(root, generated, baseline);
            File.WriteAllText(Path.Combine(root, "B.cs"), "public class Sample { public int Manual => 9; }");
            var decisions = new Dictionary<string, SyncDecision> { ["A.cs"] = SyncDecision.AcceptGeneratedCode, ["B.cs"] = SyncDecision.AcceptGeneratedCode };
            await Assert.ThrowsAsync<IOException>(() => SynchronizationEngine.ApplyPlanAsync(plan, decisions, Path.Combine(root, ".umlforge", "history")));
            Assert.Equal(original, File.ReadAllText(Path.Combine(root, "A.cs")));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void ThreeWayMerge_RefusesDifferentConcurrentMethodRenames()
    {
        const string baseline = "public class Sample { public int Read() => 1; }";
        const string current = "public class Sample { public int Fetch() => 1; }";
        const string generated = "public class Sample { public int Load() => 1; }";
        var result = CSharpThreeWayMergeService.Merge(baseline, current, generated);
        Assert.False(result.Success);
        Assert.Contains(result.Conflicts, c => c.Reason.Contains("Renommage", StringComparison.OrdinalIgnoreCase));
    }


    [Fact]
    public void OclInterpreter_EvaluatesBooleanAndPropertyConstraints()
    {
        var context = new Dictionary<string, object?>
        {
            ["self"] = new Dictionary<string, object?> { ["age"] = 23, ["active"] = true, ["name"] = "Albert" }
        };
        var result = OclInterpreter.Evaluate("self.age >= 18 and self.active = true and self.name <> ''", context);
        Assert.True(result.IsSatisfied);
        Assert.Null(result.Error);
    }

    [Fact]
    public void OclInterpreter_EvaluatesCollectionSizeAndReportsUnsupportedNamesSafely()
    {
        var context = new Dictionary<string, object?>
        {
            ["self"] = new Dictionary<string, object?> { ["tags"] = new[] { "uml", "csharp" } }
        };
        Assert.True(OclInterpreter.Evaluate("self.tags->size() > 0", context).IsSatisfied);
        var invalid = OclInterpreter.Evaluate("self.unknown = 1", context);
        Assert.NotNull(invalid.Error);
        Assert.False(invalid.IsSatisfied);
    }

    [Fact]
    public void UmlMetamodelCatalog_ListsCoreMetaclassFamilies()
    {
        Assert.Contains(UmlMetamodelCatalog.All, m => m.Name == "CombinedFragment");
        Assert.Contains(UmlMetamodelCatalog.All, m => m.Name == "Pseudostate");
        Assert.Contains(UmlMetamodelCatalog.All, m => m.Name == "Extension");
        Assert.Contains(UmlMetamodelCatalog.All, m => m.Name == "ActivityEdge");
    }

    [Fact]
    public void UmlProjectValidation_EvaluatesConstantOclConstraints()
    {
        var model = new UmlModel();
        model.Constraints.Add(new UmlConstraint { Name = "AlwaysFalse", Language = "OCL", Expression = "false" });
        Assert.Contains(UmlProjectValidationService.Validate(model), i => i.RuleId == "UMLP0072");
    }


    [Fact]
    public void OclConstraintService_EvaluatesConstraintsAttachedToClassAgainstRuntimeInstance()
    {
        var type = new UmlClass { Name = "Customer" };
        type.Constraints.Add(new UmlConstraint { Name = "Adult", Expression = "self.Age >= 18", Language = "OCL" });
        var model = new UmlModel(); model.Classes.Add(type);
        var result = Assert.Single(OclConstraintService.EvaluateElementConstraints(model, type.Id, new CustomerFixture { Age = 20 }));
        Assert.True(result.Evaluation.IsSatisfied);
    }

    private sealed class CustomerFixture { public int Age { get; set; } }

    [Fact]
    public async Task UmlCodeMapping_MapsTypesPropertiesOperationsInterfacesAndEnumerations()
    {
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        var references = trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path));
        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(), "Demo", "Demo", LanguageNames.CSharp,
                compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)))
            .AddMetadataReferences(projectId, references)
            .AddDocument(DocumentId.CreateNewId(projectId), "Demo.cs", SourceText.From("namespace Demo { public class Customer { public string Name { get; set; } = string.Empty; public int GetId(int seed) => seed; } public interface IStore { void Save(); } public enum Status { New, Done } }"));

        var model = new UmlModel();
        var customer = new UmlClass { Name = "Customer", Namespace = "Demo" };
        customer.Properties.Add(new UmlProperty { Name = "Name", Type = "string" });
        customer.Operations.Add(new UmlOperation { Name = "GetId", ReturnType = "int", Parameters = { new UmlParameter { Name = "seed", Type = "int" } } });
        model.Classes.Add(customer);
        model.Interfaces.Add(new UmlInterface { Name = "IStore", Namespace = "Demo", Operations = { new UmlOperation { Name = "Save", ReturnType = "void" } } });
        model.Enumerations.Add(new UmlEnumeration { Name = "Status", Namespace = "Demo", Literals = { new UmlEnumerationLiteral { Name = "New" }, new UmlEnumerationLiteral { Name = "Done" } } });

        var report = await UmlCodeMappingService.RefreshMappingsAsync(model, solution);
        Assert.Equal(8, report.MappedTotal);
        Assert.Contains(model.CodeMappings, m => m.ElementKind == "Property" && m.UmlElementName == "Name" && m.Status.StartsWith("Mapped", StringComparison.Ordinal));
        Assert.Contains(model.CodeMappings, m => m.ElementKind == "Operation" && m.UmlElementName == "GetId" && m.Status.StartsWith("Mapped", StringComparison.Ordinal));
        Assert.Contains(model.CodeMappings, m => m.ElementKind == "Interface" && m.UmlElementName == "IStore" && m.Status.StartsWith("Mapped", StringComparison.Ordinal));
        Assert.Contains(model.CodeMappings, m => m.ElementKind == "EnumerationLiteral" && m.UmlElementName == "Done" && m.Status.StartsWith("Mapped", StringComparison.Ordinal));
    }

}
