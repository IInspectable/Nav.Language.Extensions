using NUnit.Framework;

using Pharmatechnik.Nav.Language.Analyzer.SourceGenerator.Tests.Shared;

namespace Pharmatechnik.Nav.Language.Analyzer.SourceGenerator.Tests;

[TestFixture]
public class NavAnalyzerRegistryGeneratorTests: GeneratorTestBase {

    // Minimale Analyzer-Hierarchie: das INavAnalyzer-Interface, eine abstrakte Zwischenbasis
    // (ausgeschlossen), zwei konkrete Implementierungen (absichtlich verkehrt herum deklariert, um die
    // Ordinal-Sortierung zu prüfen), eine weitere abstrakte Klasse (ausgeschlossen) und eine konkrete
    // Klasse ohne parameterlosen Konstruktor (ausgeschlossen).
    const string Source = """
        namespace Pharmatechnik.Nav.Language.SemanticAnalyzer {
            public interface INavAnalyzer {}
            public abstract class NavAnalyzer: INavAnalyzer {}
            public class Nav0011Foo: NavAnalyzer {}
            public class Nav0010Bar: NavAnalyzer {}
            public abstract class NavBaseIgnored: NavAnalyzer {}
            public class NavNeedsArgument: NavAnalyzer { public NavNeedsArgument(int x) {} }
        }
        """;

    [Test]
    public void Generates_AnalyzerRegistry() {

        var driver    = RunGenerator(new NavAnalyzerRegistryGenerator(), CreateCompilation(Source));
        var generated = GetGeneratedFile(driver, "Analyzer.Registry.g.cs");

        SnapshotAssert.AssertSnapshot(generated, nameof(Generates_AnalyzerRegistry));
    }

}
