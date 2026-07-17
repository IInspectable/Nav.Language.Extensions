using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.CodeGen;

namespace Nav.Language.Tests;

/// <summary>
/// Codegen-Tests für das V2-Gating der <c>Cancel()</c>-Aufruffläche (S4). Kernaussage: In V2 emittiert
/// ein Call-Context die geerbte <c>Cancel()</c>-Callable <b>nur dann</b>, wenn seine Quelle einen
/// <c>cancel</c>-Ausgang deklariert (<c>… --&gt; cancel …</c>, erkannt via
/// <see cref="EdgeExtensions.TargetsCancel"/>). Fehlt die Deklaration, fehlt die Callable — womit ein
/// <c>return next.Cancel()</c> in der Logik ein Compile-Fehler wird (E3, das Typsystem erzwingt die
/// Deckungsgleichheit von Deklaration und Implementierung). V1 bleibt vom Gating unberührt (dort ist der
/// unbedingte Cancel-Default gewollt, E1) — abgesichert über die byte-identischen V1-Regression-Goldens.
/// </summary>
[TestFixture]
public class CancelCodeGenTests {

    // V2-Unit OHNE cancel-Ausgang: kein Context darf eine Cancel()-Callable tragen.
    const string NoCancelNav = """
                               #version 2

                               [namespaceprefix Nav.Language.Tests.V2.CancelGating]

                               [using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL]
                               [using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL]

                               task Sample [base StandardWFS : IWFServiceBase]
                                   [result bool]
                               {
                                   init Init1;
                                   exit Exit;
                                   view Home;

                                   Init1 --> Home;
                                   Home  --> Exit on OnOk;
                               }

                               """;

    // V2-Unit mit cancel an einer direkten Trigger-Kante (E5, unbedingter Swallow): genau der
    // OnEscape-Context bekommt Cancel().
    const string DirectCancelNav = """
                                   #version 2

                                   [namespaceprefix Nav.Language.Tests.V2.CancelGating]

                                   [using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL]
                                   [using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL]

                                   task Sample [base StandardWFS : IWFServiceBase]
                                       [result bool]
                                   {
                                       init Init1;
                                       exit Exit;
                                       view Home;

                                       Init1 --> Home;
                                       Home  --> Exit   on OnOk;
                                       Home  --> cancel on OnEscape;
                                   }

                                   """;

    // V2-Unit mit cancel an einem Choice-Arm (E5, bedingter Cancel): genau der Choice-Context bekommt
    // Cancel() — nicht die auf die Choice zeigende Trigger-Quelle.
    const string ChoiceCancelNav = """
                                   #version 2

                                   [namespaceprefix Nav.Language.Tests.V2.CancelGating]

                                   [using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL]
                                   [using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL]

                                   task Sample [base StandardWFS : IWFServiceBase]
                                       [result bool]
                                   {
                                       init Init1;
                                       exit Exit;
                                       view Home;
                                       choice C;

                                       Init1 --> Home;
                                       Home  --> C    on OnDecide;
                                       C     --> Exit;
                                       C     --> cancel if "Abbruch";
                                   }

                                   """;

    [Test]
    public void WithoutCancelDeclaration_NoCancelCallableIsEmitted() {

        var wfsBase = GenerateWfsBase(NoCancelNav);

        Assert.That(CancelCallableCount(wfsBase), Is.EqualTo(0),
                    "Ohne cancel-Deklaration darf kein Context eine Cancel()-Callable tragen (S4-Gating).");
    }

    [Test]
    public void CancelOnDirectTriggerEdge_EmitsExactlyOneCancelCallable() {

        var wfsBase = GenerateWfsBase(DirectCancelNav);

        Assert.That(CancelCallableCount(wfsBase), Is.EqualTo(1),
                    "Nur der Context der cancel-deklarierenden Trigger-Quelle bekommt Cancel().");
    }

    [Test]
    public void CancelOnChoiceArm_EmitsExactlyOneCancelCallable() {

        var wfsBase = GenerateWfsBase(ChoiceCancelNav);

        Assert.That(CancelCallableCount(wfsBase), Is.EqualTo(1),
                    "Nur der Choice-Context mit cancel-Arm bekommt Cancel(), nicht die vorgelagerte Quelle.");
    }

    // Der V2-Emitter schreibt die Cancel-Aufruffläche als 'public Result Cancel() => new(() => _wfs.Cancel());'.
    static int CancelCallableCount(string content) {
        return content.Split(["public Result Cancel()"], System.StringSplitOptions.None).Length - 1;
    }

    static string GenerateWfsBase(string source) {

        var syntax             = Syntax.ParseCodeGenerationUnit(source, filePath: System.IO.Path.Combine(@"n:\av", "CancelGating.nav"));
        var codeGenerationUnit = CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);

        Assert.That(codeGenerationUnit.LanguageVersion, Is.EqualTo(NavLanguageVersion.Version2),
                    "Das Cancel-Gating ist V2-only — die Fixture muss #version 2 tragen.");

        var results = new CodeGeneratorV2(GenerationOptions.Default).Generate(codeGenerationUnit);

        var wfsBaseSpec = results.SelectMany(r => r.Specs)
                                 .Single(spec => spec.FilePath.EndsWith("WFSBase.generated.cs", System.StringComparison.Ordinal));

        return wfsBaseSpec.Content;
    }

}
