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

        // Hinter der Edge stehen nur die Knoten, die als ZIEL taugen (ITargetNodeSymbol)...
        Assert.That(labels, Does.Contain("e"));   // exit — nur Ziel
        Assert.That(labels, Does.Contain("Sub")); // task-Knoten — Quelle und Ziel
        // ...plus das Ziel-Keyword `end`.
        Assert.That(labels, Does.Contain(SyntaxFacts.EndKeyword));
        // Aber NICHT der `init`-Knoten `i` (nur Quelle, nie Ziel).
        Assert.That(labels, Has.None.EqualTo("i"));
        // Auch KEINE Deklarations-Keywords, keine Edge-Keywords und keine Folge-Klauseln.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.ExitKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.GoToEdgeKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.OnKeyword));
    }

    [Test]
    public void TargetSlot_WithEndNode_OffersEndExactlyOnce() {

        // Regression zu den zwei `end`-Einträgen: existiert ein End-Knoten (dessen Name IST `end`), darf `end`
        // im Ziel-Slot trotzdem nur EINMAL erscheinen — als Ziel-Keyword, nicht zusätzlich als benannte
        // Knoten-Referenz. Ein End-Ziel schreibt man ausschließlich über das `end`-Schlüsselwort.
        const string nav = "task A\n"          +
                           "{\n"               +
                           "    init i;\n"     +
                           "    end;\n"        + // End-Knoten (Name = `end`)
                           "    i --> end;\n"  +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\end-target.nav");
        var caret = IndexOfToken(nav, "    i --> end;\n", "    i --> "); // Ziel-Slot hinter der Edge

        var items = NavCompletionService.GetCompletions(unit, caret);

        // Genau ein `end` — und zwar als Keyword.
        var endItems = items.Where(i => i.Label == SyntaxFacts.EndKeyword).ToArray();
        Assert.That(endItems.Length, Is.EqualTo(1), "`end` darf nicht doppelt (End-Knoten + Keyword) erscheinen.");
        Assert.That(endItems[0].Kind, Is.EqualTo(NavCompletionItemKind.Keyword));
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
    public void EdgeSlot_EdgeItemsCarryReplacementExtent() {

        var unit  = ParseModel(Nav, @"n:\av\a.nav");
        var caret = IndexOfToken(Nav, "i      --> Sub;", "i      "); // hinter dem Quellknoten `i`, VOR der Edge `-->`

        var edgeItem = NavCompletionService.GetCompletions(unit, caret)
                                           .Single(i => i.Label == SyntaxFacts.GoToEdgeKeyword);

        // Jedes Edge-Item trägt seinen eigenen Ersetzungsbereich (Edge-Keywords bestehen aus Nicht-Bezeichner-
        // Zeichen; der Host ersetzt darüber die angefangene bzw. vorhandene Edge). Hier steht der Caret VOR der
        // bereits vorhandenen `-->` → der Bereich MUSS diese Edge umfassen, damit der Commit sie ersetzt statt
        // sie zu einer zweiten `-->` zu verdoppeln.
        Assert.That(edgeItem.ReplacementExtent, Is.Not.Null);
        Assert.That(edgeItem.ReplacementExtent!.Value.Start, Is.EqualTo(caret));
        Assert.That(edgeItem.ReplacementExtent!.Value.End,   Is.EqualTo(caret + SyntaxFacts.GoToEdgeKeyword.Length));
    }

    [Test]
    public void EdgeSlot_ReplacementExtentReplacesExistingEdge_NoDuplicate() {

        // Regression zu „i -->--> Sub": Caret VOR einer vorhandenen modalen Edge `o->`. Der Ersetzungsbereich
        // deckt die vorhandene Edge komplett ab (auch die Zeichen HINTER dem Caret), damit der Commit eines
        // Edge-Keywords sie ersetzt statt eine zweite Edge einzufügen.
        const string nav = "task A\n"           +
                           "{\n"                 +
                           "    init i;\n"       +
                           "    exit e;\n"       +
                           "    i o-> e;\n"      + // vollständige modale Edge
                           "}\n";

        var unit     = ParseModel(nav, @"n:\av\before-edge.nav");
        var edgeStart = IndexOfToken(nav, "    i o-> e;\n", "    i "); // direkt vor dem `o->`

        var edge = NavCompletionService.GetCompletions(unit, edgeStart)
                                       .Single(i => i.Label == SyntaxFacts.GoToEdgeKeyword);

        var extent = edge.ReplacementExtent!.Value;
        Assert.That(extent.Start, Is.EqualTo(edgeStart));                                     // vor der Edge
        Assert.That(extent.End,   Is.EqualTo(edgeStart + SyntaxFacts.ModalEdgeKeyword.Length)); // deckt `o->` ab
    }

    [Test]
    public void EdgeSlot_ReplacementExtentStopsAtTargetToken() {

        // Sicherheitsnetz gegen einen rohen Zeichen-Vorlauf: die Edge grenzt OHNE Leerzeichen an einen
        // Zielknoten, der mit einem Edge-Zeichen beginnt (`o1`). Der Ersetzungsbereich darf NUR die Edge (das
        // Lexer-Token `-->`) umfassen, nicht in das Ziel `o1` hineinfressen.
        const string nav = "task A\n"           +
                           "{\n"                 +
                           "    init i;\n"       +
                           "    view o1;\n"      +
                           "    i -->o1;\n"      + // Edge direkt am Ziel, Ziel beginnt mit `o`
                           "}\n";

        var unit      = ParseModel(nav, @"n:\av\adjacent-target.nav");
        var edgeStart = IndexOfToken(nav, "    i -->o1;\n", "    i "); // hinter der Quelle `i `, vor `-->`

        var edge = NavCompletionService.GetCompletions(unit, edgeStart)
                                       .Single(i => i.Label == SyntaxFacts.GoToEdgeKeyword);

        var extent = edge.ReplacementExtent!.Value;
        Assert.That(extent.Start, Is.EqualTo(edgeStart));                                    // vor `-->`
        Assert.That(extent.End,   Is.EqualTo(edgeStart + SyntaxFacts.GoToEdgeKeyword.Length)); // deckt `-->`, NICHT `o1` ab
    }

    [Test]
    public void EdgeSlot_ReplacementExtentCoversTypedEdgeCharacters() {

        // Angefangene modale Edge (`o` getippt, Beginn von `o->`) hinter dem Quellknoten: das `o` ist zugleich
        // Bezeichner- und Edge-Zeichen und wird als Wort-Präfix behandelt (Kontext bleibt der Quellknoten →
        // EdgeSlot). Der Ersetzungsbereich MUSS das getippte `o` einschließen, damit der Commit es durch das
        // vollständige Keyword ersetzt (statt `oo->` zu erzeugen).
        const string nav = "task A\n"      +
                           "{\n"            +
                           "    init i;\n"  +
                           "    exit e;\n"  +
                           "    i o\n"      + // angefangene Edge — Cursor hinter dem `o`
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\partial.nav");
        var caret = IndexOfToken(nav, "    i o\n", "    i o"); // direkt hinter dem `o`

        var edge = NavCompletionService.GetCompletions(unit, caret)
                                       .Single(i => i.Label == SyntaxFacts.ModalEdgeKeyword); // o->

        var extent = edge.ReplacementExtent!.Value;
        Assert.That(extent.End,   Is.EqualTo(caret));
        Assert.That(extent.Start, Is.EqualTo(caret - 1)); // das getippte `o`
    }

    [Test]
    public void PartialEdge_AfterSourceNode_OffersEdgeKeywords() {

        // Angefangene Edge: der Nutzer hat hinter dem Quellknoten ein `-` getippt (Beginn von `-->`). Das
        // einzelne `-` ist kein gültiges Edge-Keyword und bleibt als unbekanntes, an die Wurzel gehängtes
        // Token übrig — trotzdem MUSS hier (wie beim `o` von `o->`) der Quellknoten-Kontext greifen und die
        // Edge-Keywords anbieten, statt auf die Member-Ebene (task/taskref) zurückzufallen.
        const string nav = "task A\n"      +
                           "{\n"            +
                           "    init i;\n"  +
                           "    exit e;\n"  +
                           "    i -\n"      + // angefangene Edge — Cursor hinter dem `-`
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\partial-dash.nav");
        var caret = IndexOfToken(nav, "    i -\n", "    i -"); // direkt hinter dem `-`

        var items  = NavCompletionService.GetCompletions(unit, caret);
        var labels = Labels(items);

        // Die sichtbaren Edge-Keywords werden angeboten...
        Assert.That(labels, Does.Contain(SyntaxFacts.GoToEdgeKeyword)); // -->
        Assert.That(labels, Does.Contain(SyntaxFacts.ModalEdgeKeyword)); // o->
        // ...aber KEINE Member-Ebenen-Keywords und keine Knoten.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.TaskKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.TaskrefKeyword));
        Assert.That(labels, Has.None.EqualTo("i"));

        // Das getippte `-` gehört zum Ersetzungsbereich, damit der Commit es durch das vollständige Keyword
        // ersetzt (statt `--->` zu erzeugen).
        var edge = items.Single(i => i.Label == SyntaxFacts.GoToEdgeKeyword);
        Assert.That(edge.ReplacementExtent!.Value.End,   Is.EqualTo(caret));
        Assert.That(edge.ReplacementExtent!.Value.Start, Is.EqualTo(caret - 1)); // das getippte `-`
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
        // ...und die vorhandenen Knoten, die als QUELLE einer neuen Transition taugen (ISourceNodeSymbol).
        Assert.That(labels, Does.Contain("i"));           // init — nur Quelle
        // Aber NICHT der `exit`-Knoten `e` (nur Ziel, nie Quelle) — auch wenn `exit` als Deklarations-Keyword da ist.
        Assert.That(labels, Has.None.EqualTo("e"));
        // Und NICHT `Init` (InitKeywordAlt) als Keyword — Symbol-Name des Init-Knotens, kein Lexer-Keyword.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.InitKeywordAlt));
        // Und NICHT `Init` (InitKeywordAlt) als Keyword — das ist der Symbol-Name des Init-Knotens, kein Keyword.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.InitKeywordAlt));
        // Aber KEINE Folge-Klauseln, kein `taskref`, keine Edge-Keywords.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.OnKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.IfKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.DoKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.TaskrefKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.GoToEdgeKeyword));
    }

    [Test]
    public void UnnamedInit_NotOfferedAsDuplicateKeyword() {

        // Ein unbenannter Init-Knoten (`init;`) trägt den Symbol-Namen `Init` (InitKeywordAlt). Am Satzanfang
        // wird er als Quellknoten (ISourceNodeSymbol) über AddNodeReferences angeboten — GENAU EINMAL, als
        // Connection-Point. Früher fügte die Keyword-Liste `Init` ein zweites Mal (als Keyword) hinzu.
        const string nav = "task A\n"     +
                           "{\n"           +
                           "    init;\n"   +
                           "    exit e;\n" +
                           "    \n"         + // Satzanfang im Deklarations-Block — Cursor hier
                           "    init --> e;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\unnamed-init.nav");
        var caret = IndexOfToken(nav, "exit e;\n    \n", "exit e;\n    ");

        var items = NavCompletionService.GetCompletions(unit, caret);

        // `Init` erscheint genau einmal — und zwar als Knoten (Connection-Point), nicht als Keyword.
        var initItems = items.Where(i => i.Label == SyntaxFacts.InitKeywordAlt).ToArray();
        Assert.That(initItems.Length, Is.EqualTo(1), "`Init` darf nicht doppelt (Knoten + Keyword) erscheinen.");
        Assert.That(initItems[0].Kind, Is.EqualTo(NavCompletionItemKind.ConnectionPoint));
        // Das kleingeschriebene `init`-Keyword bleibt als Deklarations-/Transitions-Opener erhalten.
        Assert.That(Labels(items), Does.Contain(SyntaxFacts.InitKeyword));
    }

    [Test]
    public void TransitionStart_OffersOnlySourceNodesAndInit() {

        // Der Task-Body ist zweigeteilt: erst Knoten-Deklarationen, dann Transitionen. Steht der Cursor am
        // Satzanfang HINTER einer Transition, ist der Deklarations-Block abgeschlossen — nur noch eine weitere
        // Transition kann folgen. Also: quellfähige Knoten + `init` (Init-Transition), aber KEINE
        // Deklarations-Keywords (choice/dialog/end/exit/task/view).
        const string nav = "task A\n"          +
                           "{\n"               +
                           "    init i;\n"     +
                           "    exit e;\n"     +
                           "    choice c;\n"   +
                           "    i --> c;\n"    +
                           "    \n"            + // Satzanfang im Transitions-Block — Cursor hier
                           "    c --> e;\n"    +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\trans.nav");
        var caret = IndexOfToken(nav, "i --> c;\n    \n", "i --> c;\n    "); // leere Zeile hinter der Transition

        var items  = NavCompletionService.GetCompletions(unit, caret);
        var labels = Labels(items);

        // Quellfähige Knoten als Beginn der nächsten Transition...
        Assert.That(labels, Does.Contain("i")); // init  — nur Quelle
        Assert.That(labels, Does.Contain("c")); // choice — Quelle und Ziel
        // ...plus das `init`-Schlüsselwort (Init-Transition `init --> …`).
        Assert.That(labels, Does.Contain(SyntaxFacts.InitKeyword));
        // Aber NICHT der `exit`-Knoten `e` (nur Ziel, nie Quelle).
        Assert.That(labels, Has.None.EqualTo("e"));
        // Und vor allem KEINE Deklarations-Keywords — der Deklarations-Block ist abgeschlossen.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.ChoiceKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.DialogKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.EndKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.ExitKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.TaskKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.ViewKeyword));
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

        // Folge-Klauseln nach dem Ziel — inkl. `else` (Bedingungs-Klausel, z.B. für Choice-Zweige).
        Assert.That(labels, Does.Contain(SyntaxFacts.OnKeyword));
        Assert.That(labels, Does.Contain(SyntaxFacts.IfKeyword));
        Assert.That(labels, Does.Contain(SyntaxFacts.ElseKeyword));
        Assert.That(labels, Does.Contain(SyntaxFacts.DoKeyword));
        // Keine Knoten, keine Deklarations-Keywords.
        Assert.That(labels, Has.None.EqualTo("i"));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.InitKeyword));
    }

    [Test]
    public void AfterTrigger_OffersConditionClausesAndDo() {

        // Vollständiger (spontaner) Trigger: der Kontext-Anker ist das Trigger-Keyword selbst (Parent
        // TriggerSyntax) → AfterTrigger. Danach folgen Bedingungs-Klausel (`if`/`else`) und `do`.
        const string nav = "task A\n"                  +
                           "{\n"                        +
                           "    init i;\n"              +
                           "    exit e;\n"              +
                           "    i --> e spontaneous ;\n" + // Cursor hinter dem Trigger, vor `;`
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\after-trigger.nav");
        var caret = IndexOfToken(nav, "spontaneous ;", "spontaneous "); // hinter `spontaneous ` (Whitespace)

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        // Nach dem Trigger folgen Bedingungs-Klausel (`if`/`else`) und `do` — aber KEIN weiteres `on`.
        Assert.That(labels, Does.Contain(SyntaxFacts.IfKeyword));
        Assert.That(labels, Does.Contain(SyntaxFacts.ElseKeyword));
        Assert.That(labels, Does.Contain(SyntaxFacts.DoKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.OnKeyword));
    }

    [Test]
    public void AfterDoKeyword_OffersNothing() {

        // Hinter `do` steht der Wert-Slot: ein freier C#-Aufruf (identifierOrString), kein Nav-Konstrukt.
        // Es darf hier nichts angeboten werden (früher streute der Fallback pauschal Knoten + Keywords ein).
        const string nav = "task A\n"          +
                           "{\n"                +
                           "    init i;\n"      +
                           "    exit e;\n"      +
                           "    i --> e do \n"  + // Cursor hinter `do `, im Wert-Slot
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\after-do.nav");
        var caret = IndexOfToken(nav, "i --> e do \n", "i --> e do "); // hinter `do ` (Whitespace)

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void AfterFilledSignalTrigger_OffersConditionClausesAndDo() {

        // Vollständiger `on Signal`-Trigger: der Kontext-Anker ist das Signal (direkter Parent = Identifier-Wert,
        // tragende Rolle erst der Trigger darüber). Über die Ancestor-Kette wird das als AfterTrigger erkannt
        // (nicht als pauschaler Fallback) → if/else/do, aber kein zweites `on`, keine Knoten, keine Edges.
        const string nav = "task A\n"                +
                           "{\n"                      +
                           "    init i;\n"            +
                           "    exit e;\n"            +
                           "    i --> e on Signal \n" + // Cursor hinter dem gefüllten Trigger
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\filled-trigger.nav");
        var caret = IndexOfToken(nav, "on Signal \n", "on Signal "); // hinter `on Signal ` (Whitespace)

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        Assert.That(labels, Does.Contain(SyntaxFacts.IfKeyword));
        Assert.That(labels, Does.Contain(SyntaxFacts.ElseKeyword));
        Assert.That(labels, Does.Contain(SyntaxFacts.DoKeyword));
        // KEIN Fallback-Rauschen: kein weiteres `on`, keine Knoten, keine Edge-Keywords.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.OnKeyword));
        Assert.That(labels, Has.None.EqualTo("i"));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.GoToEdgeKeyword));
    }

    [Test]
    public void AfterFilledCondition_OffersOnlyDo() {

        // Vollständige `if Bedingung`-Klausel: Anker ist der Bedingungs-Wert, tragende Rolle die Bedingung
        // darüber → AfterCondition (nur `do`), statt Fallback.
        const string nav = "task A\n"              +
                           "{\n"                    +
                           "    init i;\n"          +
                           "    exit e;\n"          +
                           "    i --> e if Cond \n" + // Cursor hinter der gefüllten Bedingung
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\filled-cond.nav");
        var caret = IndexOfToken(nav, "if Cond \n", "if Cond "); // hinter `if Cond ` (Whitespace)

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        Assert.That(labels, Does.Contain(SyntaxFacts.DoKeyword));
        // Nach der Bedingung folgt nur noch `do` — kein weiteres if/else/on, keine Knoten.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.IfKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.ElseKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.OnKeyword));
        Assert.That(labels, Has.None.EqualTo("i"));
    }

    [Test]
    public void AfterFilledDo_OffersNothing() {

        // Vollständige `do Aufruf`-Klausel: Anker ist der Aufruf-Wert, tragende Rolle die do-Klausel darüber
        // → Suppress (freier C#-Aufruf), statt Fallback.
        const string nav = "task A\n"              +
                           "{\n"                    +
                           "    init i;\n"          +
                           "    exit e;\n"          +
                           "    i --> e do Call \n" + // Cursor hinter dem gefüllten do-Aufruf
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\filled-do.nav");
        var caret = IndexOfToken(nav, "do Call \n", "do Call "); // hinter `do Call ` (Whitespace)

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
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
    public void TaskRefBody_AfterOpenBrace_OffersConnectionPointKeywords() {

        // Im Body einer taskref-Deklaration erlaubt die Grammatik nur Connection-Point-Deklarationen
        // (init/exit/end). Ein taskref ist KEINE Task-Definition — früher fiel der Kontext auf die
        // Member-Ebene zurück und bot fälschlich task/taskref an.
        const string nav = "taskref Sub\n" +
                           "{\n"            + // Cursor direkt hinter dem `{` des taskref-Bodys
                           "    init si;\n" +
                           "    exit se;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\ref-body.nav");
        var caret = IndexOfToken(nav, "{\n    init si;", "{"); // direkt hinter `{`

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        // Nur die Connection-Point-Keywords.
        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.InitKeyword, SyntaxFacts.ExitKeyword, SyntaxFacts.EndKeyword }));
        // KEINE Member-Keywords (das war der Bug), keine Knoten-Deklarations-/Transitions-Keywords.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.TaskKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.TaskrefKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.ChoiceKeyword));
    }

    [Test]
    public void TaskRefBody_AfterConnectionPointSemicolon_OffersConnectionPointKeywords() {

        const string nav = "taskref Sub\n" +
                           "{\n"            +
                           "    init si;\n" + // Satzanfang HINTER dem `;` einer Connection-Point-Deklaration
                           "    exit se;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\ref-body-semi.nav");
        var caret = IndexOfToken(nav, "init si;\n", "init si;"); // direkt hinter dem `;`

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.InitKeyword, SyntaxFacts.ExitKeyword, SyntaxFacts.EndKeyword }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.TaskKeyword));
    }

    [Test]
    public void TaskRefBody_InConnectorNameSlot_OffersNothing() {

        // Hinter dem Connection-Point-Keyword steht der Connector-Name — ein freier, neu vergebener Bezeichner.
        // Dort gibt es nichts anzubieten (und schon gar nicht task/taskref der Member-Ebene).
        const string nav = "taskref Sub\n" +
                           "{\n"            +
                           "    exit se;\n" + // Cursor hinter `exit ` (Name-Slot)
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\ref-name-slot.nav");
        var caret = IndexOfToken(nav, "exit se;", "exit "); // hinter `exit `, vor dem Namen

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
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
        var caret = IndexOfToken(nav, "[using Foo]", "[using F"); // im C#-Inhalt des Code-Blocks

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void InMultilineCodeBlock_OffersNothing() {

        // Mehrzeiliger, an einem Wirt (init-Knoten) hängender Code-Block: das öffnende `[` steht auf einer
        // FRÜHEREN Zeile als der Cursor. Der zeilenbegrenzte Klammer-Scan sieht es dort nicht und streute
        // Fallback-Vorschläge ein; die baumbasierte Erkennung (Kontext-Anker im geparsten CodeSyntax-Knoten)
        // unterdrückt korrekt über die Zeilengrenze hinweg.
        const string nav = "task A\n"            +
                           "{\n"                  +
                           "    init i [params\n" +
                           "        Foo];\n"       +
                           "    exit e;\n"        +
                           "    i --> e;\n"       +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\ml.nav");
        var caret = IndexOfToken(nav, "        Foo];", "        Fo"); // im C#-Inhalt, eine Zeile unter dem `[`

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void InCodeBlockKeywordSlot_AtFileLevel_OffersOnlyUsingAndNamespacePrefix() {

        const string nav = "[using Foo]\n" +
                           "\n"            +
                           "task A\n"      +
                           "{\n"           +
                           "    init i;\n" +
                           "    exit e;\n" +
                           "    i --> e;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\b.nav");
        var caret = IndexOfToken(nav, "[using Foo]", "[u"); // Schlüsselwort-Slot direkt hinter `[`

        var items  = NavCompletionService.GetCompletions(unit, caret);
        var labels = Labels(items);

        // Auf Datei-Ebene erlaubt die Grammatik nur `using` und `namespaceprefix`, als Keyword-Kategorie.
        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.UsingKeyword, SyntaxFacts.NamespaceprefixKeyword }));
        Assert.That(items.Single(i => i.Label == SyntaxFacts.UsingKeyword).Kind,
                    Is.EqualTo(NavCompletionItemKind.Keyword));
        // Code-Keywords anderer Wirte gehören NICHT hierher.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.ResultKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.CodeKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.AbstractmethodKeyword));
        // Keine Nav-Sprach-Keywords oder Knoten; versteckte Code-Keywords (`notimplemented`) nicht.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.TaskKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.InitKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.NotimplementedKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_InTaskHeader_OffersTaskHeaderCodeKeywords() {

        // Code-Block im task-Definitions-Kopf (nach `task A`, vor dem Body-`{`).
        const string nav = "task A\n"      +
                           "[]\n"           + // frisch getippter, leerer Code-Block — Cursor hinter `[`
                           "{\n"            +
                           "    init i;\n"  +
                           "    exit e;\n"  +
                           "    i --> e;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\hdr.nav");
        var caret = IndexOfToken(nav, "[]\n", "[");

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        // Der task-Kopf erlaubt code/base/generateto/params/result.
        Assert.That(labels, Is.EquivalentTo(new[] {
            SyntaxFacts.CodeKeyword, SyntaxFacts.BaseKeyword, SyntaxFacts.GeneratetoKeyword,
            SyntaxFacts.ParamsKeyword, SyntaxFacts.ResultKeyword
        }));
        // NICHT die Datei-/Knoten-Keywords.
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.UsingKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.AbstractmethodKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.DonotinjectKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_OnInitNode_OffersAbstractMethodAndParams() {

        // Code-Block an einem init-Knoten (`init i [ … ];`).
        const string nav = "task A\n"        +
                           "{\n"              +
                           "    init i [];\n" + // Cursor hinter `[`
                           "    exit e;\n"    +
                           "    i --> e;\n"   +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\init.nav");
        var caret = IndexOfToken(nav, "init i [];", "init i [");

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.AbstractmethodKeyword, SyntaxFacts.ParamsKeyword }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.UsingKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.CodeKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.DonotinjectKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_OnTaskNode_OffersDoNotInjectAndAbstractMethod() {

        // Code-Block an einem task-Knoten (`task Sub [ … ];`).
        const string nav = "task A\n"           +
                           "{\n"                 +
                           "    init i;\n"       +
                           "    exit e;\n"       +
                           "    task Sub [];\n"  + // Cursor hinter `[`
                           "    i --> e;\n"      +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\tasknode.nav");
        var caret = IndexOfToken(nav, "task Sub [];", "task Sub [");

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.DonotinjectKeyword, SyntaxFacts.AbstractmethodKeyword }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.ParamsKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.UsingKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_InTaskRef_OffersTaskRefCodeKeywords() {

        // Code-Block in einer taskref-Deklaration (nach `taskref Sub`, vor dem Body-`{`).
        const string nav = "taskref Sub\n" +
                           "[]\n"           + // Cursor hinter `[`
                           "{\n"            +
                           "    init si;\n" +
                           "    exit se;\n" +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\ref.nav");
        var caret = IndexOfToken(nav, "[]\n", "[");

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        // taskref erlaubt namespaceprefix + result (notimplemented ist versteckt).
        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.NamespaceprefixKeyword, SyntaxFacts.ResultKeyword }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.NotimplementedKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.UsingKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.CodeKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_InTaskHeader_WithExistingCode_OmitsCode() {

        // Der task-Kopf trägt bereits ein `[code …]`; ein zweiter Code-Block darf `code` (Singleton) nicht
        // erneut anbieten — die übrigen Kopf-Keywords bleiben.
        const string nav = "task A\n"          +
                           "[code \"Foo\"]\n"  +
                           "[]\n"               + // frisch getippter Block — Cursor hinter `[`
                           "{\n"                +
                           "    init i;\n"      +
                           "    exit e;\n"      +
                           "    i --> e;\n"     +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\dup.nav");
        var caret = IndexOfToken(nav, "[]\n", "[");

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        // `code` ist bereits deklariert → weg; base/generateto/params/result bleiben.
        Assert.That(labels, Is.EquivalentTo(new[] {
            SyntaxFacts.BaseKeyword, SyntaxFacts.GeneratetoKeyword,
            SyntaxFacts.ParamsKeyword, SyntaxFacts.ResultKeyword
        }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.CodeKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_AtFileLevel_WithNamespacePrefix_OmitsPrefixButKeepsUsing() {

        // Datei-Kopf mit bereits vorhandenem `[namespaceprefix …]` (Singleton) — dieses fällt weg, `using`
        // bleibt aber, weil die Grammatik es wiederholt erlaubt (`codeUsingDeclaration*`).
        const string nav = "[namespaceprefix Foo]\n" +
                           "[]\n"                     + // Cursor hinter `[`
                           "\n"                       +
                           "task A\n"                 +
                           "{\n"                      +
                           "    init i;\n"            +
                           "    exit e;\n"            +
                           "    i --> e;\n"           +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\prefix.nav");
        var caret = IndexOfToken(nav, "[]\n", "[");

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.UsingKeyword }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.NamespaceprefixKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_OnInitNode_WithExistingParams_OmitsParams() {

        // Ein init-Knoten mit bereits vorhandenem `[params …]`; ein zweiter Block bietet nur noch
        // `abstractmethod` an.
        const string nav = "task A\n"                 +
                           "{\n"                       +
                           "    init i [params] [];\n" + // zweiter Block — Cursor hinter dem letzten `[`
                           "    exit e;\n"             +
                           "    i --> e;\n"            +
                           "}\n";

        var unit  = ParseModel(nav, @"n:\av\initdup.nav");
        var caret = IndexOfToken(nav, "[params] [];", "[params] [");

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.AbstractmethodKeyword }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.ParamsKeyword));
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

    #region Commit-Chars (kanonische Autorität)

    [Test]
    public void CommitCharacters_AreTheDeliberateSet() {
        // Bewusst festgelegte Menge: Trenner, Connection-Point-Doppelpunkt, Zeichenketten-/Code-Block-
        // Begrenzer und Pfadtrenner. Der Punkt ist bewusst NICHT dabei (gültiges Bezeichner-Zeichen).
        Assert.That(NavCompletionService.CommitCharacters,
                    Is.EquivalentTo(new[] { ' ', ',', ';', ':', '"', '[', ']', '/', '\\' }));
        Assert.That(NavCompletionService.CommitCharacters, Has.None.EqualTo('.'));
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
