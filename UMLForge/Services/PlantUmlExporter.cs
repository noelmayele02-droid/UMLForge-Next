using System.Text;
using UMLForge.Models;

namespace UMLForge.Services;

public static class PlantUmlExporter
{
    public static string ExportClassDiagram(UmlModel model)
    {
        var sb = new StringBuilder("@startuml\n\nskinparam classAttributeIconSize 0\nskinparam shadowing false\n\n");
        foreach (var cls in model.Classes)
        {
            var stereotype = cls.Kind == "interface" ? " <<interface>>" : cls.Kind == "record" ? " <<record>>" : "";
            sb.AppendLine($"class {Quote(cls.Name)}{stereotype} {{");
            foreach (var attr in cls.Attributes) sb.AppendLine("  " + QuoteLine(attr));
            foreach (var method in cls.Methods) sb.AppendLine("  " + QuoteLine(method));
            sb.AppendLine("}");
        }
        foreach (var relation in model.Relations)
        {
            var from = model.Classes.FirstOrDefault(c => c.Id == relation.FromId);
            var to = model.Classes.FirstOrDefault(c => c.Id == relation.ToId);
            if (from is null || to is null) continue;
            var arrow = relation.Kind.ToLowerInvariant() switch
            {
                "inheritance" or "héritage" => "--|>",
                "implementation" => "..|>",
                "composition" => "*--",
                "aggregation" => "o--",
                "dependency" or "dépendance" => "..>",
                _ => "-->"
            };
            sb.AppendLine($"{Quote(from.Name)} {relation.SourceMultiplicity} {arrow} {relation.TargetMultiplicity} {Quote(to.Name)} : {QuoteLine(relation.Label)}");
        }
        sb.AppendLine("\n@enduml");
        return sb.ToString();
    }
    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    private static string QuoteLine(string value) => value.Replace("\r", " ").Replace("\n", " ").Replace("{", "\\{").Replace("}", "\\}");
}
