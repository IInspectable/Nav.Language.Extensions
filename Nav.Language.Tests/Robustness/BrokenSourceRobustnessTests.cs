#region Using Directives

using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.QuickInfo;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests.Robustness;

/// <summary>
/// Robustheits-Tests der Feature-Pipeline auf kaputtem Source (Befunde der systematischen
/// NRE-/Exception-Analyse, siehe doc/nav-broken-source-robustness.md): syntaktisch bzw. semantisch
/// fehlerhafter .nav-Source — wie er beim Tippen in VS ständig entsteht — darf die Engine-Kerne von
/// Classification, Diagnostics und QuickInfo niemals crashen. Die Services degradieren (leere oder
/// teilweise Ergebnisse), statt zu werfen.
/// </summary>
[TestFixture]
public class BrokenSourceRobustnessTests {

    // ------------------------------------------------------------------------------------------
    // Befund S002: task-Deklaration mit angefangener [result-Klammer — der Ergebnis-Typ besteht
    // nur aus Missing-Token. CodeParameter.FromResultDeclaration rief Type.ToString() auf, und
    // SyntaxNode.ToString() schnitt mit dem Missing-Extent (Start −1) in den Quelltext →
    // ArgumentOutOfRangeException mitten im SemanticModel-Bau.
    // ------------------------------------------------------------------------------------------

    [Test]
    public void SemanticModel_ResultDeklarationOhneTyp_DegradiertStattZuWerfen() {

        // Realistischer Tipp-Zwischenstand: die [result-Klammer ist angefangen, der Typ fehlt noch.
        AssertPipelineDegradiert(
            """
            task D [result
            """);
    }

    [Test]
    public void SemanticModel_ResultDeklarationOhneTyp_MinimalRepro() {

        // Per Delta-Debugging minimierter Original-Repro der Analyse (Operator: interior-del).
        AssertPipelineDegradiert("t D[r");
    }

    // ------------------------------------------------------------------------------------------
    // Befund S003: init-Knoten mit angefangener [params-Liste — ein Parameter hinter dem Komma
    // besteht nur aus Missing-Token. Der Analyzer Nav0119 (gleiche Init-Signaturen) baute die
    // Signatur über p.Type.ToString() → ArgumentOutOfRangeException im SemanticModel-Bau.
    // ------------------------------------------------------------------------------------------

    [Test]
    public void SemanticModel_InitParamsMitHaengendemKomma_DegradiertStattZuWerfen() {

        AssertPipelineDegradiert(
            """
            task B
            {
                init [params e,
            }
            """);
    }

    [Test]
    public void SemanticModel_InitParamsMitHaengendemKomma_MinimalRepro() {

        AssertPipelineDegradiert("t B init[p e,");
    }

    // ------------------------------------------------------------------------------------------
    // Befund S001: task-Definition mit zerstörter [base-Deklaration (Generic-Typ gelöscht) —
    // WfsBaseType besteht nur aus Missing-Token. Der Hover über einem Signal-Trigger baute via
    // DisplayPartsBuilder → SignalTriggerCodeInfo → TaskCodeInfo die Anzeige-Signatur und rief
    // WfsBaseType.ToString() → ArgumentOutOfRangeException im QuickInfo.
    // ------------------------------------------------------------------------------------------

    [Test]
    public void QuickInfo_SignalTriggerBeiKaputterBaseDeklaration_DegradiertStattZuWerfen() {

        AssertPipelineDegradiert(
            """
            task A [base
            {
                view V;
                init I;
                I --> V;
                V --> V on t;
            }
            """);
    }

    [Test]
    public void QuickInfo_SignalTriggerBeiKaputterBaseDeklaration_MinimalRepro() {

        AssertPipelineDegradiert("t l[b view X X on k");
    }

    #region Infrastructure

    /// <summary>
    /// Fährt die volle Feature-Kette so, wie die Hosts (VS-Extension, LSP) sie live aufrufen:
    /// Parse → SemanticModel → Diagnostics (DiagnosticsComputer) → syntaktische Classification
    /// (Token-Strom, Kommentare, Direktiven, Skipped) → QuickInfo an jeder Position. Kaputter
    /// Source degradiert dabei — kein Schritt darf werfen.
    /// </summary>
    static void AssertPipelineDegradiert(string source) {

        var syntax = Syntax.ParseCodeGenerationUnit(source, @"n:\av\broken.nav");
        var tree   = syntax.SyntaxTree;

        // Syntaktische Classification (wie SyntacticClassificationTagger/SemanticTokensBuilder)
        Assert.DoesNotThrow(() => {
            foreach (var token in tree.Tokens) {
                if (SyntaxFacts.IsTrivia(token.Classification)) {
                    continue;
                }

                token.ToString();
                token.GetLocation();
            }

            foreach (var comment in tree.Comments()) {
                comment.ToString(tree.SourceText);
            }

            foreach (var directive in tree.Directives())
            foreach (var token in directive.ChildTokens()) {
                token.ToString();
            }

            foreach (var skipped in tree.SkippedTokens())
            foreach (var token in skipped.ChildTokens()) {
                token.ToString();
            }
        }, "Classification darf auf kaputtem Source nicht werfen");

        // SemanticModel-Bau (wie SemanticModelProvider)
        CodeGenerationUnit unit = null;
        Assert.DoesNotThrow(() => unit = CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax),
                            "Der SemanticModel-Bau darf auf kaputtem Source nicht werfen");

        // Diagnostics (wie NavWorkspaceCore.GetDiagnostics)
        Assert.DoesNotThrow(() => DiagnosticsComputer.FromUnit(unit, @"n:\av\broken.nav").ToList(),
                            "Diagnostics dürfen auf kaputtem Source nicht werfen");

        // QuickInfo an jeder Zeichen-Position (wie Hover in VS/LSP)
        Assert.DoesNotThrow(() => {
            for (var position = 0; position <= source.Length; position++) {
                NavHoverService.GetHover(unit, position);
            }
        }, "QuickInfo darf an keiner Position auf kaputtem Source werfen");
    }

    #endregion

}
