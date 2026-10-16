using UMLForge.Models;

namespace UMLForge.Services;

/// <summary>Evaluates model constraints against an explicit runtime instance using the safe OCL subset.</summary>
public static class OclConstraintService
{
    public static IReadOnlyList<OclConstraintResult> EvaluateElementConstraints(UmlModel model, Guid elementId, object instance)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(instance);
        var constraints = model.Constraints.Where(c => c.ConstrainedElementId == elementId)
            .Concat(model.Classes.Where(c => c.Id == elementId).SelectMany(c => c.Constraints))
            .Concat(model.Classes.SelectMany(c => c.Operations).Where(o => o.Id == elementId).SelectMany(o => o.Constraints))
            .Where(c => c.Language.Equals("OCL", StringComparison.OrdinalIgnoreCase));
        var context = new Dictionary<string, object?> { ["self"] = instance };
        return constraints.Select(c => new OclConstraintResult(c.Id, c.Name, c.Severity, c.Expression, OclInterpreter.Evaluate(c.Expression, context))).ToArray();
    }
}

public sealed record OclConstraintResult(Guid ConstraintId, string Name, string Severity, string Expression, OclEvaluationResult Evaluation);
