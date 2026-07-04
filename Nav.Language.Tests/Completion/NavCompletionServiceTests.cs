#region Using Directives

using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Completion;

#endregion

namespace Nav.Language.Tests.Completion;

[TestFixture]
public class NavCompletionServiceTests {

    [Test]
    public void AfterTaskKeyword_OffersTaskDeclarations() {

        // Sub ist als taskref deklariert; Caret (|) hinter `task ` vor dem Knotennamen.
        var m = NavMarkup.Parse(
            """
            taskref Sub
            {
                init si;
                exit se;
            }

            task Main
            {
                init i;
                exit e;
                task |Sub;
                i --> Sub;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Caret;

        var items = NavCompletionService.GetCompletions(unit, caret);

        Assert.That(Labels(items), Does.Contain("Sub"));
        Assert.That(items.Single(i => i.Label == "Sub").Kind, Is.EqualTo(NavCompletionItemKind.Task));
        // Reine Task-Vervollständigung — keine Keywords gemischt.
        Assert.That(Labels(items), Has.None.EqualTo("init"));
    }

    [Test]
    public void AfterNodeColon_OffersExitConnectionPoints() {

        // Task-Knoten Sub (Typ taskref Sub mit exit se); Caret (|) direkt hinter `Sub:`.
        var m = NavMarkup.Parse(
            """
            taskref Sub
            {
                init si;
                exit se;
            }

            task Main
            {
                init i;
                exit e;
                task Sub;
                Sub:|se --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Caret;

        var items = NavCompletionService.GetCompletions(unit, caret);

        Assert.That(Labels(items), Does.Contain("se"));
        Assert.That(items.Single(i => i.Label == "se").Kind, Is.EqualTo(NavCompletionItemKind.ConnectionPoint));
        // Nach dem Doppelpunkt nur die Exit-Connection-Points — keine Edge-Keywords, keine Knoten.
        Assert.That(Labels(items), Has.None.EqualTo(SyntaxFacts.GoToEdgeKeyword));
        Assert.That(Labels(items), Has.None.EqualTo("i"));
    }

    [Test]
    public void TargetSlot_OffersTargetNodesAndEndKeyword() {

        // Caret (|) im Ziel-Slot hinter `--> `: e (exit, nur Ziel), Sub (task-Knoten), i (init, nur Quelle).
        var m = NavMarkup.Parse(
            """
            task Main
            {
                init i;
                exit e;
                task Sub;
                i --> |Sub;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Caret;

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
        // End-Knoten (Name = `end`); Caret (|) im Ziel-Slot hinter der Edge.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                end;
                i --> |end;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\end-target.nav");
        var caret = m.Caret;

        var items = NavCompletionService.GetCompletions(unit, caret);

        // Genau ein `end` — und zwar als Keyword.
        var endItems = items.Where(i => i.Label == SyntaxFacts.EndKeyword).ToArray();
        Assert.That(endItems.Length, Is.EqualTo(1), "`end` darf nicht doppelt (End-Knoten + Keyword) erscheinen.");
        Assert.That(endItems[0].Kind, Is.EqualTo(NavCompletionItemKind.Keyword));
    }

    [Test]
    public void EdgeSlot_OffersOnlyVisibleEdgeKeywords() {

        // Caret (|) hinter dem Quellknoten `i`, vor der Edge `-->`.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                task Sub;
                i |--> Sub;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Caret;

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

        // Caret (|) hinter dem Quellknoten `i`, VOR der bereits vorhandenen Edge `-->`.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                task Sub;
                i |--> Sub;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\a.nav");
        var caret = m.Caret;

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
        // Vollständige modale Edge `o->`; Caret (|) direkt vor dem `o->`.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                i |o-> e;
            }

            """);

        var unit      = ParseModel(m.Source, @"n:\av\before-edge.nav");
        var edgeStart = m.Caret;

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
        // Edge direkt am Ziel, Ziel beginnt mit `o`; Caret (|) hinter der Quelle `i `, vor `-->`.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                view o1;
                i |-->o1;
            }

            """);

        var unit      = ParseModel(m.Source, @"n:\av\adjacent-target.nav");
        var edgeStart = m.Caret;

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
        // Angefangene Edge — Caret (|) direkt hinter dem getippten `o`.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                i o|
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\partial.nav");
        var caret = m.Caret;

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
        // Angefangene Edge — Caret (|) direkt hinter dem getippten `-`.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                i -|
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\partial-dash.nav");
        var caret = m.Caret;

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

        // Leere, eingerückte Zeile — Caret (|) am Satzanfang.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                |
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\stmt.nav");
        var caret = m.Caret;

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
        // Satzanfang im Deklarations-Block — Caret (|) auf der leeren, eingerückten Zeile.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init;
                exit e;
                |
                init --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\unnamed-init.nav");
        var caret = m.Caret;

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
        // Satzanfang im Transitions-Block — Caret (|) auf der leeren Zeile hinter der Transition.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                choice c;
                i --> c;
                |
                c --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\trans.nav");
        var caret = m.Caret;

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

        // Caret (|) hinter dem vollständigen Ziel `e ` (Whitespace), vor `;`.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                i --> e |;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\after.nav");
        var caret = m.Caret;

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
        // Caret (|) hinter dem spontanen Trigger `spontaneous ` (Whitespace), vor `;`.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                i --> e spontaneous |;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\after-trigger.nav");
        var caret = m.Caret;

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
        // Caret (|) hinter `do ` (Whitespace), im Wert-Slot.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                i --> e do |
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\after-do.nav");
        var caret = m.Caret;

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void AfterFilledSignalTrigger_OffersConditionClausesAndDo() {

        // Vollständiger `on Signal`-Trigger: der Kontext-Anker ist das Signal (direkter Parent = Identifier-Wert,
        // tragende Rolle erst der Trigger darüber). Über die Ancestor-Kette wird das als AfterTrigger erkannt
        // (nicht als pauschaler Fallback) → if/else/do, aber kein zweites `on`, keine Knoten, keine Edges.
        // Caret (|) hinter dem gefüllten `on Signal `-Trigger (Whitespace).
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                i --> e on Signal |
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\filled-trigger.nav");
        var caret = m.Caret;

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
        // Caret (|) hinter der gefüllten `if Cond `-Bedingung (Whitespace).
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                i --> e if Cond |
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\filled-cond.nav");
        var caret = m.Caret;

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
        // Caret (|) hinter dem gefüllten `do Call `-Aufruf (Whitespace).
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                i --> e do Call |
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\filled-do.nav");
        var caret = m.Caret;

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void InitNodeTail_AfterName_OffersOnlyDo() {

        // Hinter dem Namen einer init-Knoten-Deklaration folgt grammatisch nur noch die optionale `do`-Klausel
        // (und, über `[`, die Code-Blöcke) bzw. das abschließende `;`. Statt des pauschalen Fallbacks (alle
        // Knoten + Keywords + Edges) darf hier NUR `do` erscheinen.
        // Caret (|) hinter `init i ` (Whitespace), vor dem `;`.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i |;
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\init-tail.nav");
        var caret = m.Caret;

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        // Ausschließlich `do` — die einzige grammatisch mögliche Fortsetzung.
        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.DoKeyword }));
    }

    [Test]
    public void InitNodeTail_WithExistingDoClause_OffersNothing() {

        // Ist die `do`-Klausel bereits vorhanden, gibt es im „Schwanz" davor nichts mehr anzubieten (die
        // Singleton-`do`-Klausel darf nicht ein zweites Mal vorgeschlagen werden).
        // Caret (|) hinter `init i ` (Whitespace), vor der bereits vorhandenen `do`-Klausel.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i |do Foo;
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\init-tail-do.nav");
        var caret = m.Caret;

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void NodeDeclarationTail_AfterExitName_OffersNothing() {

        // Hinter dem Namen einer exit-Knoten-Deklaration folgt grammatisch nur noch das `;`. Der pauschale
        // Fallback (Knoten + Keywords + Edges) wird hier zu Suppress präzisiert.
        // Caret (|) hinter `exit e ` (Whitespace), vor dem `;`.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e |;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\exit-tail.nav");
        var caret = m.Caret;

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void NodeDeclarationTail_AfterChoiceName_OffersNothing() {

        // Caret (|) hinter `choice c ` (Whitespace), vor dem `;` — grammatisch folgt nur noch das `;`.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                choice c |;
                i --> c;
                c --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\choice-tail.nav");
        var caret = m.Caret;

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void NodeDeclarationTail_AfterTaskNodeName_OffersNothing() {

        // Der Task-Knoten ist der subtile Fall: hinter dem Referenznamen kann noch ein Alias (freier Bezeichner)
        // sowie `[donotinject]`/`[abstractmethod]` folgen — allesamt nichts, was die Completion anbietet. Also
        // Suppress statt Fallback.
        // Caret (|) hinter `task Sub ` (Whitespace), vor dem `;`.
        var m = NavMarkup.Parse(
            """
            taskref Sub
            {
                init si;
                exit se;
            }

            task Main
            {
                init i;
                exit e;
                task Sub |;
                i --> Sub;
                Sub --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\tasknode-tail.nav");
        var caret = m.Caret;

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void MemberLevel_OffersOnlyTaskAndTaskref() {

        // Caret (|) ganz am Dateianfang — außerhalb jeder Task-Definition.
        var m = NavMarkup.Parse(
            """
            |task A
            {
                init i;
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\member.nav");

        var items  = NavCompletionService.GetCompletions(unit, m.Caret);
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
        // Caret (|) direkt hinter dem `{` des taskref-Bodys.
        var m = NavMarkup.Parse(
            """
            taskref Sub
            {|
                init si;
                exit se;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\ref-body.nav");
        var caret = m.Caret;

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

        // Caret (|) am Satzanfang hinter dem `;` einer Connection-Point-Deklaration.
        var m = NavMarkup.Parse(
            """
            taskref Sub
            {
                init si;|
                exit se;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\ref-body-semi.nav");
        var caret = m.Caret;

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.InitKeyword, SyntaxFacts.ExitKeyword, SyntaxFacts.EndKeyword }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.TaskKeyword));
    }

    [Test]
    public void TaskRefBody_InConnectorNameSlot_OffersNothing() {

        // Hinter dem Connection-Point-Keyword steht der Connector-Name — ein freier, neu vergebener Bezeichner.
        // Dort gibt es nichts anzubieten (und schon gar nicht task/taskref der Member-Ebene).
        // Caret (|) hinter `exit ` im Connector-Name-Slot.
        var m = NavMarkup.Parse(
            """
            taskref Sub
            {
                exit |se;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\ref-name-slot.nav");
        var caret = m.Caret;

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void InSingleLineComment_OffersNothing() {

        // Caret (|) mitten im Zeilenkommentar.
        var m = NavMarkup.Parse(
            """
            task A
            {
                // h|ier nix
                init i;
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\c.nav");
        var caret = m.Caret;

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void InStringLiteral_OffersNothing() {

        // Caret (|) innerhalb der Zeichenkette "Sub.nav".
        var m = NavMarkup.Parse(
            """
            taskref "S|ub.nav";

            task A
            {
                init i;
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\s.nav");
        var caret = m.Caret;

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void InCodeBlock_OffersNothing() {

        // Caret (|) im C#-Inhalt des Code-Blocks.
        var m = NavMarkup.Parse(
            """
            [using F|oo]

            task A
            {
                init i;
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\b.nav");
        var caret = m.Caret;

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void InMultilineCodeBlock_OffersNothing() {

        // Mehrzeiliger, an einem Wirt (init-Knoten) hängender Code-Block: das öffnende `[` steht auf einer
        // FRÜHEREN Zeile als der Cursor. Der zeilenbegrenzte Klammer-Scan sieht es dort nicht und streute
        // Fallback-Vorschläge ein; die baumbasierte Erkennung (Kontext-Anker im geparsten CodeSyntax-Knoten)
        // unterdrückt korrekt über die Zeilengrenze hinweg.
        // Caret (|) im C#-Inhalt, eine Zeile unter dem öffnenden `[`.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i [params
                    Fo|o];
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\ml.nav");
        var caret = m.Caret;

        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    [Test]
    public void InCodeBlockKeywordSlot_AtFileLevel_OffersOnlyUsingAndNamespacePrefix() {

        // Caret (|) im Schlüsselwort-Slot direkt hinter `[`.
        var m = NavMarkup.Parse(
            """
            [u|sing Foo]

            task A
            {
                init i;
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\b.nav");
        var caret = m.Caret;

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
        // Frisch getippter, leerer Code-Block — Caret (|) hinter `[`.
        var m = NavMarkup.Parse(
            """
            task A
            [|]
            {
                init i;
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\hdr.nav");
        var caret = m.Caret;

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
        // Caret (|) hinter dem `[` des Code-Blocks am init-Knoten.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i [|];
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\init.nav");
        var caret = m.Caret;

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.AbstractmethodKeyword, SyntaxFacts.ParamsKeyword }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.UsingKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.CodeKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.DonotinjectKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_OnTaskNode_OffersDoNotInjectAndAbstractMethod() {

        // Code-Block an einem task-Knoten (`task Sub [ … ];`).
        // Caret (|) hinter dem `[` des Code-Blocks am task-Knoten.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i;
                exit e;
                task Sub [|];
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\tasknode.nav");
        var caret = m.Caret;

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.DonotinjectKeyword, SyntaxFacts.AbstractmethodKeyword }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.ParamsKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.UsingKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_InTaskRef_OffersTaskRefCodeKeywords() {

        // Code-Block in einer taskref-Deklaration (nach `taskref Sub`, vor dem Body-`{`).
        // Caret (|) hinter dem `[` des Code-Blocks in der taskref-Deklaration.
        var m = NavMarkup.Parse(
            """
            taskref Sub
            [|]
            {
                init si;
                exit se;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\ref.nav");
        var caret = m.Caret;

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        // taskref erlaubt namespaceprefix + result (notimplemented ist versteckt).
        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.NamespaceprefixKeyword, SyntaxFacts.ResultKeyword }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.NotimplementedKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.UsingKeyword));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.CodeKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_InTaskRef_WithFollowingSingletons_OffersNothing() {

        // Frischer (leerer) Block zwischen `[namespaceprefix …]` und den folgenden `[notimplemented]`/
        // `[result …]`. Alle sichtbaren Singletons des taskref-Kopfs (namespaceprefix, result) sind — vor
        // wie nach dem Caret — bereits vorhanden → es bleibt nichts mehr anzubieten. Vor dem Parser-Fix
        // verschluckte das leere `[]` die nachfolgenden Blöcke, sodass `result` fälschlich noch erschien.
        // Caret (|) im leeren Block direkt hinter `[namespaceprefix NS.2]`.
        var m = NavMarkup.Parse(
            """
            taskref TR1 [namespaceprefix NS.2][|]
                        [notimplemented]
                        [result RT1 r1]
            {
                init si;
                exit se;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\ref2.nav");
        var caret = m.Caret;

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        Assert.That(labels, Is.Empty);
    }

    [Test]
    public void InCodeBlockKeywordSlot_InTaskHeader_WithExistingCode_OmitsCode() {

        // Der task-Kopf trägt bereits ein `[code …]`; ein zweiter Code-Block darf `code` (Singleton) nicht
        // erneut anbieten — die übrigen Kopf-Keywords bleiben.
        // Frisch getippter zweiter Block — Caret (|) hinter `[`.
        var m = NavMarkup.Parse(
            """
            task A
            [code "Foo"]
            [|]
            {
                init i;
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\dup.nav");
        var caret = m.Caret;

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        // `code` ist bereits deklariert → weg; base/generateto/params/result bleiben.
        Assert.That(labels, Is.EquivalentTo(new[] {
            SyntaxFacts.BaseKeyword, SyntaxFacts.GeneratetoKeyword,
            SyntaxFacts.ParamsKeyword, SyntaxFacts.ResultKeyword
        }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.CodeKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_InTaskHeader_WithFollowingCode_OmitsCode() {

        // Wie WithExistingCode_OmitsCode, aber der frische (leere) Block steht VOR dem vorhandenen
        // `[code …]`. Ein vorangestelltes malformes `[]` darf das nachfolgende, gültige `[code …]` nicht
        // aus dem Baum verschlucken — sonst böte die Completion `code` fälschlich erneut an.
        // Frisch getippter erster Block — Caret (|) hinter `[`.
        var m = NavMarkup.Parse(
            """
            task A
            [|]
            [code "Foo"]
            {
                init i;
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\dup2.nav");
        var caret = m.Caret;

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        // `code` folgt bereits → weg; base/generateto/params/result bleiben.
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
        // Caret (|) hinter `[` des zweiten Blocks.
        var m = NavMarkup.Parse(
            """
            [namespaceprefix Foo]
            [|]

            task A
            {
                init i;
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\prefix.nav");
        var caret = m.Caret;

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.UsingKeyword }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.NamespaceprefixKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_OnInitNode_WithExistingParams_OmitsParams() {

        // Ein init-Knoten mit bereits vorhandenem `[params …]`; ein zweiter Block bietet nur noch
        // `abstractmethod` an.
        // Zweiter Block am init-Knoten — Caret (|) hinter dem letzten `[`.
        var m = NavMarkup.Parse(
            """
            task A
            {
                init i [params] [|];
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\initdup.nav");
        var caret = m.Caret;

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        Assert.That(labels, Is.EquivalentTo(new[] { SyntaxFacts.AbstractmethodKeyword }));
        Assert.That(labels, Has.None.EqualTo(SyntaxFacts.ParamsKeyword));
    }

    [Test]
    public void AfterHash_OffersOnlyVersionKeyword() {

        // Caret (|) direkt hinter dem `#`.
        var m = NavMarkup.Parse(
            """
            #|
            task A
            {
                init i;
                exit e;
                i --> e;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\d.nav");
        var caret = m.Caret;

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

        // Caret (|) am Ende des gerade getippten Wort-Präfixes `v`.
        var m = NavMarkup.Parse(
            """
            #v|
            task A
            {
                init i;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\d.nav");
        var caret = m.Caret;

        var labels = Labels(NavCompletionService.GetCompletions(unit, caret));

        // Das Wort `v` ist nur das Filter-Präfix — der Kontext bleibt der Schlüsselwort-Slot.
        Assert.That(labels, Does.Contain(SyntaxFacts.VersionDirectiveKeyword));
    }

    [Test]
    public void AfterVersionKeyword_OffersSupportedVersionNumbers() {

        // Trailing Space, kein Wert: der Caret steht am Trivia-Ende (`#version ` frisch getippt) — genau der
        // FindTrivia-[Start,End)-Fallstrick, den DirectiveAt über die inklusive Endgrenze abfängt.
        // Caret (|) hinter `#version ` (Trailing Space) im Werte-Slot.
        var m = NavMarkup.Parse(
            """
            #version |
            task A
            {
                init i;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\d.nav");
        var caret = m.Caret;

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

        // Caret (|) hinter dem `pragma`-Wort, im Subjekt-Slot.
        var m = NavMarkup.Parse(
            """
            #pragma |foo
            task A
            {
                init i;
            }

            """);

        var unit  = ParseModel(m.Source, @"n:\av\d.nav");
        var caret = m.Caret;

        // Tiefer in einer nicht erkannten Direktive gibt es nichts anzubieten — auch kein Fallback.
        Assert.That(NavCompletionService.GetCompletions(unit, caret), Is.Empty);
    }

    #region Trigger-Chars (kanonische Autorität)

    [Test]
    public void TriggerCharacters_ContainAllContextDelimiters() {
        // Die eine Autorität deckt alle Situationen ab, in denen ein Sonderzeichen die Completion eröffnet:
        // '#' Direktiven, ':' Exit-Connection-Points, '-' Edge-Beginn, '[' Code-Block, '"' Zeichenkette.
        // Die Pfadtrenner '/' und '\' lösen bewusst NICHT aus (die Pfad-Liste eröffnen '"' + Buchstaben).
        Assert.That(NavCompletionService.TriggerCharacters,
                    Is.EquivalentTo(new[] { '#', ':', '-', '[', '"' }));
        Assert.That(NavCompletionService.TriggerCharacters, Has.None.EqualTo('/'));
        Assert.That(NavCompletionService.TriggerCharacters, Has.None.EqualTo('\\'));
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
        // Bewusst festgelegte Menge: Trenner, Connection-Point-Doppelpunkt und die Zeichenketten-/Code-Block-
        // Begrenzer. Der Punkt ist bewusst NICHT dabei (gültiges Bezeichner-Zeichen); die Pfadtrenner '/' und
        // '\' ebenso wenig (die Pfad-Vervollständigung ersetzt den ganzen String-Inhalt, und außerhalb einer
        // Zeichenkette zerlegte ein Commit auf '/' einen gerade getippten '//'-Kommentar).
        Assert.That(NavCompletionService.CommitCharacters,
                    Is.EquivalentTo(new[] { ' ', ',', ';', ':', '"', '[', ']' }));
        Assert.That(NavCompletionService.CommitCharacters, Has.None.EqualTo('.'));
        Assert.That(NavCompletionService.CommitCharacters, Has.None.EqualTo('/'));
        Assert.That(NavCompletionService.CommitCharacters, Has.None.EqualTo('\\'));
    }

    #endregion

    #region Helpers

    static string[] Labels(System.Collections.Generic.IReadOnlyList<NavCompletionItem> items) {
        return items.Select(i => i.Label).ToArray();
    }

    static CodeGenerationUnit ParseModel(string source, string filePath) {
        var syntax = Syntax.ParseCodeGenerationUnit(text: source, filePath: filePath);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
    }

    #endregion

}
