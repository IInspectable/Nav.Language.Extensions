#region Using Directives

using System;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Completion;

#endregion

namespace Nav.Language.Tests.Completion;

[TestFixture]
public class NavCompletionServiceTests {

    const string Nav = "taskref Sub\n"          +
                       "{\n"                     +
                       "    init si;\n"          +
                       "    exit se;\n"          +
                       "}\n"                     +
                       "\n"                      +
                       "task Main\n"             +
                       "{\n"                     +
                       "    init i;\n"           +
                       "    exit e;\n"           +
                       "    task Sub;\n"         +
                       "    i      --> Sub;\n"   +
                       "    Sub:se --> e;\n"     +
                       "}\n";

    [Test]
    public void AfterTaskKeyword_OffersTaskDeclarations() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "task Sub;", "task "); // direkt hinter `task ` vor dem Knotennamen

        var items = NavCompletionService.GetCompletions(unit, caret);

        Assert.That(Labels(items), Does.Contain("Sub"));
        Assert.That(items.Single(i => i.Label == "Sub").Kind, Is.EqualTo(NavCompletionItemKind.Task));
        // Reine Task-Vervollständigung — keine Keywords gemischt.
        Assert.That(Labels(items), Has.None.EqualTo("init"));
    }

    [Test]
    public void AfterNodeColon_OffersExitConnectionPoints() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "Sub:se --> e;", "Sub:"); // direkt hinter `Sub:`

        var items = NavCompletionService.GetCompletions(unit, caret);

        Assert.That(Labels(items), Does.Contain("se"));
        Assert.That(items.Single(i => i.Label == "se").Kind, Is.EqualTo(NavCompletionItemKind.ConnectionPoint));
        // Nach dem Doppelpunkt nur die Exit-Connection-Points — keine Edge-Keywords, keine Knoten.
        Assert.That(Labels(items), Has.None.EqualTo(SyntaxFacts.GoToEdgeKeyword));
        Assert.That(Labels(items), Has.None.EqualTo("i"));
    }

    [Test]
    public void TargetSlot_OffersTargetNodesAndEndKeyword() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "i      --> Sub;", "i      --> "); // auf der Ziel-Knotenreferenz 'Sub'

        var items  = NavCompletionService.GetCompletions(unit, caret);
        var labels = Labels(items);

        // Hinter der Edge stehen die Knoten als Ziel...
        Assert.That(labels, Does.Contain("i"));
        Assert.That(labels, Does.Contain("e"));
        Assert.That(labels, Does.Contain("Sub"));
        // ...plus das Ziel-Keyword `end`.
        Assert.That(labels, Does.Contain(SyntaxFacts.EndKeyword));
        // Aber KEINE Deklarations-Keywords, keine Edge-Keywords und keine Folge-Klauseln.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.ExitKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.GoToEdgeKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.OnKeyword));
    }

    [Test]
    public void EdgeSlot_OffersOnlyVisibleEdgeKeywords() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "i      --> Sub;", "i      "); // hinter dem Quellknoten `i`, vor der Edge

        var items  = NavCompletionService.GetCompletions(unit, caret);
        var labels = Labels(items);

        // Sichtbare Edge-Keywords vorhanden, als Keyword-Kategorie.
        Assert.That(labels, Does.Contain(SyntaxFacts.GoToEdgeKeyword));
        Assert.That(labels, Does.Contain(SyntaxFacts.ModalEdgeKeyword)); // o->
        Assert.That(items.Single(i => i.Label == SyntaxFacts.ModalEdgeKeyword).Kind,
                    Is.EqualTo(NavCompletionItemKind.Keyword));
        // Versteckte Edge-Keywords nicht.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.ModalEdgeKeywordAlt)); // *->
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.NonModalEdgeKeyword));
        // Hinter dem Quellknoten kann nur eine Edge folgen — keine Knoten, keine sonstigen Keywords.
        Assert.That(labels, Has.None.EqualTo("Sub"));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.InitKeyword));
    }

    [Test]
    public void StatementStart_OffersNodeDeclarationKeywordsAndNodes() {

        const string nav = "task A\n"            +
                           "{\n"                 +
                           "    init i;\n"       +
                           "    exit e;\n"       +
                           "    \n"              + // leere, eingerückte Zeile — Cursor hier
                           "    i --> e;\n"      +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\stmt.nav");
        var caret = IndexOfToken(nav, "exit e;\n    \n", "exit e;\n    "); // Satzanfang auf der leeren Zeile

        var items  = NavCompletionService.GetCompletions(unit, caret);
        var labels = Labels(items);

        // Knoten-Deklarations-Keywords...
        Assert.That(labels, Does.Contain(SyntaxFacts.InitKeyword));
        Assert.That(labels, Does.Contain(SyntaxFacts.ExitKeyword));
        Assert.That(labels, Does.Contain(SyntaxFacts.TaskKeyword));
        // ...und die vorhandenen Knoten (als Quelle einer neuen Transition).
        Assert.That(labels, Does.Contain("i"));
        Assert.That(labels, Does.Contain("e"));
        // Aber KEINE Folge-Klauseln, kein `taskref`, keine Edge-Keywords.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.OnKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.IfKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.DoKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.TaskrefKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.GoToEdgeKeyword));
    }

    [Test]
    public void AfterTarget_OffersFollowupClauses() {

        const string nav = "task A\n"          +
                           "{\n"               +
                           "    init i;\n"     +
                           "    exit e;\n"     +
                           "    i --> e ;\n"   + // Cursor hinter dem vollständigen Ziel `e`, vor `;`
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\after.nav");
        var caret = IndexOfToken(nav, "i --> e ;", "i --> e "); // hinter `e ` (Whitespace), vor `;`

        var items  = NavCompletionService.GetCompletions(unit, caret);
        var labels = Labels(items);

        // Folge-Klauseln nach dem Ziel.
        Assert.That(labels, Does.Contain(SyntaxFacts.OnKeyword));
        Assert.That(labels, Does.Contain(SyntaxFacts.IfKeyword));
        Assert.That(labels, Does.Contain(SyntaxFacts.DoKeyword));
        // Keine Knoten, keine Deklarations-Keywords.
        Assert.That(labels, Has.None.EqualTo("i"));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.InitKeyword));
    }

    [Test]
    public void MemberLevel_OffersOnlyTaskAndTaskref() {

        const string nav = "task A\n"          +
                           "{\n"               +
                           "    init i;\n"     +
                           "    exit e;\n"     +
                           "    i --> e;\n"    +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\member.nav");

        // Cursor ganz am Dateianfang — außerhalb jeder Task-Definition.
        var items  = NavCompletionService.GetCompletions(unit, 0);
        var labels = Labels(items);

        Assert.That(labels, Does.Contain(SyntaxFacts.TaskKeyword));
        Assert.That(labels, Does.Contain(SyntaxFacts.TaskrefKeyword));
        // Keine knoten-/transitionsbezogenen Vorschläge auf Member-Ebene.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.InitKeyword));
        Assert.That(labels, Has.None.EqualTo("i"));
    }

    [Test]
    public void InSingleLineComment_OffersNothing() {

        const string nav = "task A\n"          +
                           "{\n"               +
                           "    // hier nix\n" +
                           "    init i;\n"     +
                           "    exit e;\n"     +
                           "    i --> e;\n"    +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\c.nav");
        var caret = IndexOfToken(nav, "// hier nix", "// h"); // mitten im Kommentar

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void InStringLiteral_OffersNothing() {

        const string nav = "taskref \"Sub.nav\";\n" +
                           "\n"                      +
                           "task A\n"                +
                           "{\n"                     +
                           "    init i;\n"           +
                           "    exit e;\n"           +
                           "    i --> e;\n"          +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\s.nav");
        var caret = IndexOfToken(nav, "\"Sub.nav\"", "\"S"); // innerhalb der Zeichenkette

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void InCodeBlock_OffersNothing() {

        const string nav = "[using Foo]\n" +
                           "\n"            +
                           "task A\n"      +
                           "{\n"           +
                           "    init i;\n" +
                           "    exit e;\n" +
                           "    i --> e;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\b.nav");
        var caret = IndexOfToken(nav, "[using Foo]", "[using F"); // innerhalb des Code-Blocks

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void AfterHash_OffersOnlyVersionKeyword() {

        const string nav = "#\n"       +
                           "task A\n"  +
                           "{\n"       +
                           "    init i;\n" +
                           "    exit e;\n" +
                           "    i --> e;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\d.nav");
        var caret = IndexOfToken(nav, "#\n", "#"); // direkt hinter dem `#`

        var items  = NavCompletionService.GetCompletions(unit, caret);
        var labels = Labels(items);

        // Hinter `#` nur das Direktiv-Schlüsselwort `version`, als Keyword-Kategorie.
        Assert.That(labels, Does.Contain(SyntaxFacts.VersionDirectiveKeyword));
        Assert.That(items.Single(i => i.Label == SyntaxFacts.VersionDirectiveKeyword).Kind,
                    Is.EqualTo(NavCompletionItemKind.Keyword));
        // `pragma` wird bewusst NICHT angeboten (kein bekanntes Pragma).
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.PragmaDirectiveKeyword));
        // Keine Sprach-Keywords oder Knoten auf einer Direktiv-Zeile.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.TaskKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.InitKeyword));
    }

    [Test]
    public void WhileTypingDirectiveKeyword_StillOffersVersion() {

        const string nav = "#v\n"      +
                           "task A\n"  +
                           "{\n"       +
                           "    init i;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\d.nav");
        var caret = IndexOfToken(nav, "#v\n", "#v"); // am Ende des gerade getippten Wort-Präfixes `v`

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        // Das Wort `v` ist nur das Filter-Präfix — der Kontext bleibt der Schlüsselwort-Slot.
        Assert.That(labels, Does.Contain(SyntaxFacts.VersionDirectiveKeyword));
    }

    [Test]
    public void AfterVersionKeyword_OffersSupportedVersionNumbers() {

        // Trailing Space, kein Wert: der Caret steht am Trivia-Ende (`#version ` frisch getippt) — genau der
        // FindTrivia-[Start,End)-Fallstrick, den DirectiveAt über die inklusive Endgrenze abfängt.
        const string nav = "#version \n" +
                           "task A\n"     +
                           "{\n"          +
                           "    init i;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\d.nav");
        var caret = IndexOfToken(nav, "#version \n", "#version "); // hinter `#version ` im Werte-Slot

        var items  = NavCompletionService.GetCompletions(unit, caret);
        var labels = Labels(items);

        // Die gültigen Versionsnummern (heute nur `1`) — aus NavLanguageVersion.SupportedVersions.
        Assert.That(labels, Does.Contain(NavLanguageVersion.Version1.ToString()));
        Assert.That(labels, Is.EquivalentTo(NavLanguageVersion.SupportedVersions.Select(v => v.ToString())));
        // Kein Schlüsselwort mehr im Werte-Slot.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.VersionDirectiveKeyword));
    }

    [Test]
    public void InsideUnknownPragmaSubject_OffersNothing() {

        const string nav = "#pragma foo\n" +
                           "task A\n"       +
                           "{\n"            +
                           "    init i;\n"  +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\d.nav");
        var caret = IndexOfToken(nav, "#pragma foo", "#pragma "); // hinter dem `pragma`-Wort, im Subjekt-Slot

        // Tiefer in einer nicht erkannten Direktive gibt es nichts anzubieten — auch kein Fallback.
        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    #region Trigger-Chars (kanonische Autorität)

    [Test]
    public void TriggerCharacters_ContainAllContextDelimiters() {
        // Die eine Autorität deckt alle Situationen ab, in denen ein Sonderzeichen die Completion eröffnet:
        // '#' Direktiven, ':' Exit-Connection-Points, '-' Edge-Beginn, '[' Code-Block, '"' + Pfadtrenner.
        Assert.That(NavCompletionService.TriggerCharacters,
                    Is.EquivalentTo(new[] { '#', ':', '-', '[', '"', '/', '\\' }));
    }

    [Test]
    public void IsTriggerCharacter_MatchesTriggerCharacters() {
        foreach (var c in NavCompletionService.TriggerCharacters) {
            Assert.That(NavCompletionService.IsTriggerCharacter(c), Is.True, $"'{c}' sollte auslösen.");
        }

        // Bezeichner-Zeichen lösen NICHT über diese Menge aus (Buchstaben laufen getrennt über char.IsLetter).
        Assert.That(NavCompletionService.IsTriggerCharacter('a'), Is.False);
        Assert.That(NavCompletionService.IsTriggerCharacter(' '), Is.False);
    }

    #endregion

    #region Helpers

    static string[] Labels(System.Collections.Generic.IReadOnlyList<NavCompletionItem> items) {
        return items.Select(i => i.Label).ToArray();
    }

    static int IndexOfToken(string source, string anchor, string leading) {
        var anchorIndex = source.IndexOf(anchor, StringComparison.Ordinal);
        Assert.That(anchorIndex, Is.GreaterThanOrEqualTo(0), $"Anker '{anchor}' nicht gefunden.");
        return anchorIndex + leading.Length;
    }

    static CodeGenerationUnit ParseModel(string source, string filePath) {
        var syntax = Syntax.ParseCodeGenerationUnit(text: source, filePath: filePath);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    #endregion

}
