using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.Grammar.SourceGenerator.Tests.Shared;

namespace Pharmatechnik.Nav.Language.Grammar.SourceGenerator.Tests;

[TestFixture]
public class NavGrammarGeneratorTests: GeneratorTestBase {

    // Wohlgeformter Mini-Parser: zwei Regeln, geschlossen, je ein EBNF-Fragment.
    const string WellFormedParser = """
        class NavParser {

            enum Rule { Foo, Bar }

            /// <remarks><code><![CDATA[
            /// foo ::= "x" bar
            /// ]]></code></remarks>
            object ParseFoo() => null;

            /// <remarks><code><![CDATA[
            /// bar ::= "y"
            /// ]]></code></remarks>
            object ParseBar() => null;
        }
        """;

    [Test]
    public void Generates_Grammar_From_Ebnf_Fragments() {

        var driver = RunGenerator(CreateCompilation(WellFormedParser));

        Assert.That(GetGeneratorDiagnostics(driver), Is.Empty, "Wohlgeformte Grammatik darf keine Diagnosen erzeugen.");

        var generated = GetGeneratedFile(driver, "NavGrammar.g.cs");
        SnapshotAssert.AssertSnapshot(generated, nameof(Generates_Grammar_From_Ebnf_Fragments));
    }

    [Test]
    public void Reports_NAV001_When_Rule_Has_No_Fragment() {

        const string source = """
            class NavParser {

                enum Rule { Foo, Baz }

                /// <remarks><code><![CDATA[
                /// foo ::= "x"
                /// ]]></code></remarks>
                object ParseFoo() => null;
            }
            """;

        var diagnostics = GetGeneratorDiagnostics(RunGenerator(CreateCompilation(source)));

        Assert.That(diagnostics.Select(d => d.Id), Has.Member("NAV001"));
        Assert.That(diagnostics.Single(d => d.Id == "NAV001").GetMessage(), Does.Contain("baz"));
    }

    [Test]
    public void Reports_NAV002_When_Nonterminal_Is_Undefined() {

        const string source = """
            class NavParser {

                enum Rule { Foo }

                /// <remarks><code><![CDATA[
                /// foo ::= "x" qux
                /// ]]></code></remarks>
                object ParseFoo() => null;
            }
            """;

        var diagnostics = GetGeneratorDiagnostics(RunGenerator(CreateCompilation(source)));

        Assert.That(diagnostics.Select(d => d.Id), Has.Member("NAV002"));
        Assert.That(diagnostics.Single(d => d.Id == "NAV002").GetMessage(), Does.Contain("qux"));
    }

}
