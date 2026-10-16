using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace UMLForge.Services;

/// <summary>
/// Safe, deliberately bounded OCL expression evaluator for common invariants.
/// It does not execute .NET code. Unsupported standard-library operations return a diagnostic.
/// </summary>
public static class OclInterpreter
{
    public static OclEvaluationResult Evaluate(string expression, IReadOnlyDictionary<string, object?> context)
    {
        try
        {
            var parser = new Parser(expression, context);
            var value = parser.Parse();
            return new OclEvaluationResult(value is bool b && b, value, null, value is not bool);
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or DivideByZeroException or OverflowException)
        {
            return new OclEvaluationResult(false, null, ex.Message, true);
        }
    }

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private readonly IReadOnlyDictionary<string, object?> _context;
        private int _position;
        public Parser(string source, IReadOnlyDictionary<string, object?> context) { _tokens = Tokenize(source); _context = context; }
        public object? Parse() { var value = ParseImplies(); Expect(TokenKind.End); return value; }

        private object? ParseImplies() { var left = ParseOr(); while (Match("implies")) { var right = ParseOr(); left = !ToBool(left) || ToBool(right); } return left; }
        private object? ParseOr() { var left = ParseXor(); while (Match("or") || Match("||")) { var right = ParseXor(); left = ToBool(left) || ToBool(right); } return left; }
        private object? ParseXor() { var left = ParseAnd(); while (Match("xor")) { var right = ParseAnd(); left = ToBool(left) ^ ToBool(right); } return left; }
        private object? ParseAnd() { var left = ParseCompare(); while (Match("and") || Match("&&")) { var right = ParseCompare(); left = ToBool(left) && ToBool(right); } return left; }
        private object? ParseCompare()
        {
            var left = ParseAdd();
            while (Current.Text is "=" or "<>" or "!=" or "<" or ">" or "<=" or ">=")
            {
                var op = Next().Text; var right = ParseAdd();
                var cmp = Compare(left, right);
                left = op switch { "=" => EqualsNormalized(left, right), "<>" or "!=" => !EqualsNormalized(left, right), "<" => cmp < 0, ">" => cmp > 0, "<=" => cmp <= 0, ">=" => cmp >= 0, _ => false };
            }
            return left;
        }
        private object? ParseAdd()
        {
            var left = ParseMultiply();
            while (Current.Text is "+" or "-")
            {
                var op = Next().Text; var right = ParseMultiply();
                if (op == "+" && (left is string || right is string)) left = $"{left}{right}";
                else left = op == "+" ? Number(left) + Number(right) : Number(left) - Number(right);
            }
            return left;
        }
        private object? ParseMultiply() { var left = ParseUnary(); while (Current.Text is "*" or "/" or "mod") { var op = Next().Text; var right = ParseUnary(); left = op switch { "*" => Number(left) * Number(right), "/" => Number(left) / Number(right), "mod" => Number(left) % Number(right), _ => left }; } return left; }
        private object? ParseUnary()
        {
            if (Match("not") || Match("!")) return !ToBool(ParseUnary());
            if (Match("-")) return -Number(ParseUnary());
            if (Match("+")) return Number(ParseUnary());
            return ParsePostfix();
        }
        private object? ParsePostfix()
        {
            var value = ParsePrimary();
            while (Match("." ) || Match("->"))
            {
                var member = Expect(TokenKind.Identifier).Text;
                if (member.Equals("size", StringComparison.OrdinalIgnoreCase) && Match("(")) { Expect(")"); value = Size(value); continue; }
                if (member.Equals("isEmpty", StringComparison.OrdinalIgnoreCase) && Match("(")) { Expect(")"); value = Size(value) == 0; continue; }
                if (member.Equals("notEmpty", StringComparison.OrdinalIgnoreCase) && Match("(")) { Expect(")"); value = Size(value) != 0; continue; }
                value = ReadMember(value, member);
            }
            return value;
        }
        private object? ParsePrimary()
        {
            if (Match("(")) { var v = ParseImplies(); Expect(")"); return v; }
            var token = Next();
            if (token.Kind == TokenKind.Number) return double.Parse(token.Text, CultureInfo.InvariantCulture);
            if (token.Kind == TokenKind.String) return token.Text;
            if (token.Kind == TokenKind.Identifier)
            {
                if (token.Text.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
                if (token.Text.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
                if (token.Text.Equals("null", StringComparison.OrdinalIgnoreCase) || token.Text.Equals("invalid", StringComparison.OrdinalIgnoreCase)) return null;
                if (token.Text.Equals("self", StringComparison.OrdinalIgnoreCase)) return _context.GetValueOrDefault("self");
                if (_context.TryGetValue(token.Text, out var found)) return found;
                throw new InvalidOperationException($"La variable OCL '{token.Text}' n'existe pas dans le contexte.");
            }
            throw new FormatException($"Expression OCL inattendue près de '{token.Text}'.");
        }
        private static object? ReadMember(object? value, string member)
        {
            if (value is null) throw new InvalidOperationException($"Impossible de lire '{member}' sur null.");
            if (value is IReadOnlyDictionary<string, object?> ro && ro.TryGetValue(member, out var rv)) return rv;
            if (value is IDictionary dict && dict.Contains(member)) return dict[member];
            var type = value.GetType();
            var prop = type.GetProperty(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (prop is not null && prop.GetIndexParameters().Length == 0) return prop.GetValue(value);
            var field = type.GetField(member, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
            if (field is not null) return field.GetValue(value);
            throw new InvalidOperationException($"Propriété OCL '{member}' introuvable sur {type.Name}.");
        }
        private static int Size(object? value) => value switch { null => 0, string s => s.Length, ICollection c => c.Count, IEnumerable e => e.Cast<object?>().Count(), _ => throw new InvalidOperationException("size()/isEmpty()/notEmpty() attend une collection ou une chaîne.") };
        private static bool ToBool(object? value) => value is bool b ? b : throw new InvalidOperationException("Une expression logique OCL doit produire true ou false.");
        private static double Number(object? value) => value is IConvertible c ? c.ToDouble(CultureInfo.InvariantCulture) : throw new InvalidOperationException("Une opération arithmétique attend un nombre.");
        private static int Compare(object? a, object? b)
        {
            if (a is null || b is null) return a is null ? b is null ? 0 : -1 : 1;
            if (a is string sa && b is string sb) return string.CompareOrdinal(sa, sb);
            if (a is IConvertible && b is IConvertible) return Number(a).CompareTo(Number(b));
            if (Equals(a, b)) return 0;
            throw new InvalidOperationException("Ces valeurs OCL ne peuvent pas être ordonnées.");
        }
        private static bool EqualsNormalized(object? a, object? b) => a is IConvertible && b is IConvertible && a is not string && b is not string ? Number(a).Equals(Number(b)) : Equals(a, b);
        private Token Current => _tokens[Math.Min(_position, _tokens.Count - 1)];
        private Token Next() => _tokens[Math.Min(_position++, _tokens.Count - 1)];
        private bool Match(string text) { if (!Current.Text.Equals(text, StringComparison.OrdinalIgnoreCase)) return false; _position++; return true; }
        private Token Expect(string text) { if (!Match(text)) throw new FormatException($"'{text}' attendu, '{Current.Text}' trouvé."); return _tokens[_position - 1]; }
        private Token Expect(TokenKind kind) { if (Current.Kind != kind) throw new FormatException($"{kind} attendu, '{Current.Text}' trouvé."); return Next(); }

        private static List<Token> Tokenize(string source)
        {
            var result = new List<Token>(); var i = 0;
            while (i < source.Length)
            {
                if (char.IsWhiteSpace(source[i])) { i++; continue; }
                if (char.IsLetter(source[i]) || source[i] == '_') { var start = i++; while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_')) i++; result.Add(new(TokenKind.Identifier, source[start..i])); continue; }
                if (char.IsDigit(source[i])) { var start = i++; while (i < source.Length && (char.IsDigit(source[i]) || source[i] == '.')) i++; result.Add(new(TokenKind.Number, source[start..i])); continue; }
                if (source[i] == '\'') { i++; var b = new StringBuilder(); while (i < source.Length && source[i] != '\'') { if (source[i] == '\\' && i + 1 < source.Length) i++; b.Append(source[i++]); } if (i >= source.Length) throw new FormatException("Chaîne OCL non terminée."); i++; result.Add(new(TokenKind.String, b.ToString())); continue; }
                var two = i + 1 < source.Length ? source.Substring(i, 2) : "";
                if (two is "->" or "<>" or "!=" or "<=" or ">=" or "||" or "&&") { result.Add(new(TokenKind.Symbol, two)); i += 2; continue; }
                if ("().,+-*/%=<>!".Contains(source[i])) { result.Add(new(TokenKind.Symbol, source[i++].ToString())); continue; }
                throw new FormatException($"Caractère OCL non pris en charge : '{source[i]}'.");
            }
            result.Add(new(TokenKind.End, "<fin>")); return result;
        }
    }
    private enum TokenKind { Identifier, Number, String, Symbol, End }
    private sealed record Token(TokenKind Kind, string Text);
}

public sealed record OclEvaluationResult(bool IsSatisfied, object? Value, string? Error, bool IsInvalid)
{
    public string Summary => Error is not null ? "OCL invalide : " + Error : IsInvalid ? "L'expression OCL ne produit pas un booléen." : IsSatisfied ? "Contrainte satisfaite." : "Contrainte violée.";
}
