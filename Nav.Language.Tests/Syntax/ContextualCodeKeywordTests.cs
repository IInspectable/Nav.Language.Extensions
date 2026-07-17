#region Using Directives

using System.Collections.Generic;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Nav.Language.Tests;

/// <summary>
/// Syntax-Tests dafür, dass die 10 Code-Keywords (<c>result</c>, <c>params</c>, <c>base</c>,
/// <c>namespaceprefix</c>, <c>using</c>, <c>code</c>, <c>generateto</c>, <c>notimplemented</c>,
/// <c>abstractmethod</c>, <c>donotinject</c>) <b>kontextuelle</b> Keywords sind: Sie lexen als
/// <see cref="SyntaxTokenType.Identifier"/> und sind nur als Leader direkt hinter <c>[</c> ein
/// Schlüsselwort (per Retype im Parser wieder hochgestuft). An jeder anderen Position — insbesondere in
/// der Parameter-Namensposition — sind sie ein gewöhnlicher Bezeichner. Auslöser war
/// <c>[params ErrorBoxResult result]</c>, das zuvor mit „unexpected input 'result'" scheiterte.
/// </summary>
[TestFixture]
public class ContextualCodeKeywordTests {

    [Test]
    public void ParamsDeclaration_AllowsFormerKeywordAsParameterName() {

        // Der ursprünglich gemeldete Fall: 'result' steht an der Parameter-Namensposition.
        var declaration = Syntax.ParseCodeParamsDeclaration("[params ErrorBoxResult result]");

        Assert.That(declaration.SyntaxTree.Diagnostics, Is.Empty);

        var parameter = declaration.ParameterList![0];
        Assert.That(parameter.Type.ToString().Trim(),    Is.EqualTo("ErrorBoxResult"));
        Assert.That(parameter.Identifier.ToString(),     Is.EqualTo("result"));
        Assert.That(parameter.Identifier.Type,           Is.EqualTo(SyntaxTokenType.Identifier));
        Assert.That(parameter.Identifier.Classification, Is.EqualTo(TextClassification.ParameterName));
    }

    [Test]
    public void ResultDeclaration_AllowsFormerKeywordAsParameterName() {

        // Spiegelbildlich: 'params' als Name der Ergebnis-Angabe eines [result …].
        var declaration = Syntax.ParseCodeResultDeclaration("[result Foo params]");

        Assert.That(declaration.SyntaxTree.Diagnostics, Is.Empty);

        Assert.That(declaration.Result.Type.ToString().Trim(),    Is.EqualTo("Foo"));
        Assert.That(declaration.Result.Identifier.ToString(),     Is.EqualTo("params"));
        Assert.That(declaration.Result.Identifier.Type,           Is.EqualTo(SyntaxTokenType.Identifier));
        Assert.That(declaration.Result.Identifier.Classification, Is.EqualTo(TextClassification.ParameterName));
    }

    [Test]
    public void ParamsDeclaration_AllowsFormerKeywordAsTypeAndName() {

        // 'using' in der Typ-Position, 'base' in der Namensposition — beide gewöhnliche Bezeichner.
        var declaration = Syntax.ParseCodeParamsDeclaration("[params using base]");

        Assert.That(declaration.SyntaxTree.Diagnostics, Is.Empty);

        var parameter = declaration.ParameterList![0];
        Assert.That(parameter.Type.ToString().Trim(), Is.EqualTo("using"));
        Assert.That(parameter.Identifier.ToString(),  Is.EqualTo("base"));
        Assert.That(parameter.Identifier.Type,        Is.EqualTo(SyntaxTokenType.Identifier));
    }

    [Test]
    public void ParamsDeclaration_AllowsMultipleFormerKeywordNames() {

        var declaration = Syntax.ParseCodeParamsDeclaration("[params Foo result, Bar params, Baz code]");

        Assert.That(declaration.SyntaxTree.Diagnostics, Is.Empty);

        var names = new List<string>();
        foreach (var parameter in declaration.ParameterList!) {
            Assert.That(parameter.Identifier.Type, Is.EqualTo(SyntaxTokenType.Identifier));
            names.Add(parameter.Identifier.ToString());
        }

        Assert.That(names, Is.EqualTo((string[])["result", "params", "code"]));
    }

    [Test]
    public void ParamsLeaderKeyword_IsRetypedToKeyword() {

        // Der Leader bleibt trotz Lexen-als-Identifier ein echtes Keyword-Token (Retype an der
        // Parser-Grenze) — damit Classification/QuickInfo/Completion unverändert weiterarbeiten.
        var declaration = Syntax.ParseCodeParamsDeclaration("[params ErrorBoxResult result]");

        Assert.That(declaration.ParamsKeyword.Type,           Is.EqualTo(SyntaxTokenType.ParamsKeyword));
        Assert.That(declaration.ParamsKeyword.Classification, Is.EqualTo(TextClassification.Keyword));
        Assert.That(declaration.ParamsKeyword.IsMissing,      Is.False);
    }

    [Test]
    public void TaskHeader_WithFormerKeywordParameterName_ParsesWithoutDiagnostics() {

        const string source = """
                              task SimpleTask [params ErrorBoxResult result]
                              {
                                  init;
                                  exit End;

                                  Init --> End;
                              }
                              """;

        var unit = Syntax.ParseCodeGenerationUnit(source);

        Assert.That(unit.SyntaxTree.Diagnostics, Is.Empty);
        Assert.That(unit.ToString(),             Is.EqualTo(source)); // Roundtrip — kein Zeichen verloren.
    }

    [TestCaseSource(nameof(CodeKeywordLiterals))]
    public void CodeKeyword_RemainsReservedIdentifier(string codeKeyword) {

        // B-konservativ: Die Code-Keywords bleiben in SyntaxFacts.Keywords — als Top-Level-Bezeichner
        // (Task-/Knoten-Name) sind sie weiterhin ungültig (semantisch via Nav2000 gemeldet). Nur die
        // syntaktische Parameter-Namensposition fragt IsValidIdentifier nie und funktioniert unabhängig davon.
        Assert.That(SyntaxFacts.IsValidIdentifier(codeKeyword), Is.False);
    }

    static IEnumerable<string> CodeKeywordLiterals => SyntaxFacts.CodeKeywords;

}
