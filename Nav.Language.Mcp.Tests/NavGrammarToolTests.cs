#region Using Directives

using NUnit.Framework;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Nav.Language.Mcp.Tests;

/// <summary>
/// Smoke-Tests für <c>nav_grammar</c> — das einzige zustandslose MCP-Tool (kein Workspace-Parameter).
/// Dient zugleich als Proof, dass das Testprojekt die MCP-Tools direkt als statische Methoden aufrufen
/// kann (kein stdio, kein JSON-RPC, kein DI-Container).
/// </summary>
[TestFixture]
public class NavGrammarToolTests {

    [Test]
    public void Grammar_WithoutArguments_ReturnsFullGrammar() {
        var result = NavGrammarTool.Grammar();

        Assert.IsNull(result.Rule,      "Ohne 'rule' ist keine Einzelregel gesetzt.");
        Assert.IsNull(result.Error,     "Die volle Grammatik ist kein Fehlerfall.");
        Assert.IsNull(result.Terminals, "Ohne includeTerminals bleibt die Terminal-Tabelle leer.");
        StringAssert.Contains("codeGenerationUnit ::=", result.Ebnf,
                              "Die Einstiegsregel codeGenerationUnit muss in der vollen Grammatik stehen.");
    }

    [Test]
    public void Grammar_WithKnownRule_ReturnsSingleProduction() {
        var result = NavGrammarTool.Grammar("taskDefinition");

        Assert.AreEqual("taskDefinition", result.Rule);
        Assert.IsNull(result.Error, "Eine bekannte Regel ist kein Fehlerfall.");
        Assert.IsNotEmpty(result.Ebnf, "Die EBNF der Einzelregel darf nicht leer sein.");
        StringAssert.Contains("taskDefinition ::=", result.Ebnf,
                              "Die Einzelregel enthält ihre eigene Produktion.");
    }

    [Test]
    public void Grammar_WithUnknownRule_ReturnsErrorAndAvailableRules() {
        // 'arrayType' ist eine Nebenproduktion ohne eigenen Schlüssel (steckt im Fragment von codeType) —
        // die dokumentierte Kante, die statt einer Exception einen Fehler mit Regelliste liefern muss.
        var result = NavGrammarTool.Grammar("arrayType");

        Assert.IsNotNull(result.Error,          "Eine unbekannte Regel muss einen Fehler liefern (keine Exception).");
        Assert.IsNotNull(result.AvailableRules, "Im Fehlerfall sind die bekannten Regelnamen aufzulisten.");
        CollectionAssert.IsNotEmpty(result.AvailableRules!);
        CollectionAssert.Contains(result.AvailableRules!, "taskDefinition",
                                  "Die bekannte Regel taskDefinition muss in der Liste stehen.");
        CollectionAssert.DoesNotContain(result.AvailableRules!, "arrayType",
                                        "arrayType hat bewusst keinen eigenen Schlüssel.");
    }

    [Test]
    public void Grammar_WithIncludeTerminals_ReturnsTerminalTable() {
        var result = NavGrammarTool.Grammar(rule: null, includeTerminals: true);

        Assert.IsNotNull(result.Terminals, "includeTerminals muss die Terminal-Tabelle befüllen.");
        CollectionAssert.IsNotEmpty(result.Terminals!.Keywords,     "Die Keyword-Liste darf nicht leer sein.");
        CollectionAssert.IsNotEmpty(result.Terminals!.Punctuations, "Die Interpunktions-Liste darf nicht leer sein.");
        CollectionAssert.Contains(result.Terminals!.Categorical, "Identifier",
                                  "Die kategorischen Terminale enthalten den Identifier.");
    }

}
