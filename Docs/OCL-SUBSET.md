# OCL dans UMLForge Next — sous-ensemble actuel

UMLForge contient un évaluateur d'expressions qui n'exécute jamais de code .NET arbitraire. Il peut évaluer une contrainte avec `OclInterpreter.Evaluate(expression, context)` ou les contraintes rattachées à une classe/opération via `OclConstraintService.EvaluateElementConstraints(model, elementId, instance)`.

## Syntaxe prise en charge

- Littéraux `true`, `false`, `null`, nombres et chaînes entre apostrophes.
- Lecture de propriétés de `self` (réflexion publique) et de dictionnaires.
- Comparaisons `=`, `<>`, `!=`, `<`, `>`, `<=`, `>=`.
- Logique `not`, `and`, `or`, `xor`, `implies` et alias `!`, `&&`, `||`.
- Arithmétique `+`, `-`, `*`, `/`, `mod` et parenthèses.
- Opérations de base `->size()`, `->isEmpty()` et `->notEmpty()` sur chaînes/collections.

## Exemple

```csharp
var context = new Dictionary<string, object?>
{
    ["self"] = new Customer { Age = 20, Active = true }
};
var result = OclInterpreter.Evaluate("self.Age >= 18 and self.Active = true", context);
```

## Limites explicites

Ce moteur n'est pas un parseur/interpréteur complet de la norme OCL. Les itérateurs (`forAll`, `exists`, `select`, `collect`), les opérations de collections complètes, les types invalid/void formels, les pré/postconditions évaluées sur des états, les tuples, les opérations définies par le modèle et la sémantique normative complète restent à implémenter. Une expression non prise en charge renvoie un diagnostic ; elle ne doit pas être considérée comme satisfaite.
