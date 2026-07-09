#region Using Directives

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.CodeGen;

#endregion

namespace Nav.Language.CodeAnalysis.Tests;

/// <summary>
/// Pinnt die sonst nur <b>zufällig</b> erfüllte Versions-Annahme der annotationsgetriebenen
/// Call-Site-Navigation im <c>LocationFinder</c> (<c>FindCallBeginLogicDeclarationLocationsAsync</c>,
/// <c>FindCallChoiceLogicDeclarationLocationAsync</c>).
/// <para>
/// Jene Pfade starten an einer C#-Annotation (<c>&lt;NavInitCall&gt;</c>/<c>&lt;NavChoiceCall&gt;</c>) im
/// generierten Code — <b>ohne</b> Nav-Symbol und damit ohne die Sprach-Version der Quell-<c>.nav</c>. Sie
/// leiten den gesuchten Membernamen (<c>BeginLogic</c> bzw. <c>{Choice}Logic</c>) deshalb aus der
/// <b>Default-Generation</b> ab (<c>LocationFinder.DefaultBeginLogicMethodName</c> /
/// <c>DefaultLogicMethodSuffix</c>). Der genuine Nav→C#-Pfad zieht seine Namen dagegen versionsrichtig aus
/// <c>*CodeInfo</c>.
/// </para>
/// <para>
/// Wichtig: die betroffenen Namensbausteine (<c>BeginMethodPrefix</c>, <c>LogicMethodSuffix</c>) sind
/// <b>implementierungs-intern</b> (abstrakte Logic-Methoden der <c>{Task}WFSBase</c>) — sie sind <i>nicht</i>
/// von der Cross-Version-Invariante der <c>IBegin{Task}WFS</c>-Schnittstellen gedeckt (die nur die
/// interface-seitigen Namen bindet). Eine künftige Generation dürfte sie also divergieren lassen. Genau dann
/// bräche die Call-Site-Navigation gegen deren generierten Code — und die Golden-Call-Site-Tests
/// (<c>InitCallSite_*</c>, <c>ChoiceCallSite_*</c>, <c>EscalateCallSite_*</c>, die auf
/// <c>#version&#160;2</c>-Fixtures laufen) blieben grün oder brächen nur <i>still</i>, weil V2 diese
/// Bausteine derzeit wie V1 schreibt.
/// </para>
/// <para>
/// Dieser Guard fängt das <b>laut und lokalisiert</b> ab: Er hält fest, dass die Default-Namensbausteine für
/// <b>jede</b> unterstützte Version gelten — die eigentliche Voraussetzung des Call-Site-Pfads. Schlägt er
/// fehl, ist der versionsbewusste Umbau des Call-Site-Pfads fällig („Option B": z.B. die Sprach-Version in
/// die Call-Annotation einbetten, statt die Default-Generation zu unterstellen).
/// </para>
/// </summary>
[TestFixture]
public class CallSiteVersionAssumptionTests {

    [Test]
    public void CallSitePath_AssumesDefaultLogicNamesHoldForEverySupportedVersion() {

        var defaults = NavCodeGenFacts.For(NavLanguageVersion.Default);

        foreach (var version in NavLanguageVersion.SupportedVersions) {

            var facts = NavCodeGenFacts.For(version);

            Assert.That(
                facts.BeginMethodPrefix, Is.EqualTo(defaults.BeginMethodPrefix),
                $"Version {version}: BeginMethodPrefix weicht von der Default-Generation ab. Die annotationsgetriebene " +
                "Call-Site-Navigation (LocationFinder.FindCallBeginLogicDeclarationLocationsAsync) kennt die Quell-" +
                "Sprachversion nicht und sucht die 'BeginLogic' der Default-Generation — gegen von dieser Version " +
                "generierten Code trifft sie damit ins Leere. Jetzt ist 'Option B' (versionsbewusster Call-Site-Pfad) fällig.");

            Assert.That(
                facts.LogicMethodSuffix, Is.EqualTo(defaults.LogicMethodSuffix),
                $"Version {version}: LogicMethodSuffix weicht von der Default-Generation ab. Die annotationsgetriebene " +
                "Call-Site-Navigation (…FindCallBeginLogicDeclarationLocationsAsync / …FindCallChoiceLogicDeclarationLocationAsync) " +
                "baut 'BeginLogic'/'{Choice}Logic' aus der Default-Generation — gegen von dieser Version generierten Code " +
                "trifft sie ins Leere. Jetzt ist 'Option B' (versionsbewusster Call-Site-Pfad) fällig.");
        }
    }
}
