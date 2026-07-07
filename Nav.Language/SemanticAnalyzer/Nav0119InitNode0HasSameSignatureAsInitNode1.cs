using System.Linq;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.SemanticAnalyzer;

public class Nav0119InitNode0HasSameSignatureAsInitNode1: NavAnalyzer {

    public override DiagnosticDescriptor Descriptor => DiagnosticDescriptors.Semantic.Nav0119InitNode0HasSameSignatureAsInitNode1;

    public override IEnumerable<Diagnostic> Analyze(ITaskDefinitionSymbol taskDefinition, AnalyzerContext context) {
        //==============================
        // The init node '{0}' has the same parameter signature as init node '{1}'
        //==============================
        // Alle Init-Knoten eines Tasks werden auf dieselbe Begin-Methode abgebildet
        // (IBegin{Task}WFS.Begin bzw. Begin{Node}), die über die Parameter der Init-Knoten überladen
        // wird — der Init-Knotenname landet nur als Annotation, nicht im Methodennamen. Zwei Init-Knoten
        // mit identischer Parameter-Typ-Signatur erzeugen also zweimal dasselbe Member und damit
        // nicht-übersetzbaren Code (CS0111). Gegen diesen latenten Fall — verwandt mit '--> End' aus
        // Init-Reichweite (Nav0118) — schützt sonst keine Regel.
        var firstBySignature = new Dictionary<string, IInitNodeSymbol>();

        foreach (var initNode in taskDefinition.NodeDeclarations.OfType<IInitNodeSymbol>()) {

            var signature = GetSignature(initNode);

            if (firstBySignature.TryGetValue(signature, out var firstNode)) {
                yield return new Diagnostic(
                    initNode.Alias?.Location ?? initNode.Location,
                    Descriptor,
                    initNode.Name,
                    firstNode.Name);
            } else {
                firstBySignature.Add(signature, initNode);
            }
        }
    }

    // Überladungs-Signatur = geordnete Folge der Parameter-Typen (Namen sind für die Überladung
    // unerheblich). Whitespace innerhalb eines Typs wird entfernt, damit z.B. 'List<int>' und
    // 'List< int >' als gleiche Signatur erkannt werden (der C#-Compiler sieht ebenfalls denselben Typ).
    static string GetSignature(IInitNodeSymbol initNode) {

        var parameters = initNode.Syntax.CodeParamsDeclaration?.ParameterList;
        if (parameters == null) {
            return string.Empty;
        }

        return string.Join(
            ",",
            parameters.Select(p => new string(p.Type.ToString().Where(c => !char.IsWhiteSpace(c)).ToArray())));
    }

}
