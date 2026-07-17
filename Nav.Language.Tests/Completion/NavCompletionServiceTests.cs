#region Using Directives

using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Completion;

using static Nav.Language.Tests.Completion.Completions;

#endregion

namespace Nav.Language.Tests.Completion;

[TestFixture]
public class NavCompletionServiceTests {

    #region Slot-Kontext (welche Items werden je grammatischer Situation angeboten)

    [Test]
    public void AfterTaskKeyword_OffersTaskDeclarations() {

        // Sub ist als taskref deklariert; Caret (|) hinter `task ` vor dem Knotennamen. Reine
        // Task-Vervollständigung — alle deklarierten Tasks (das taskref `Sub` UND die Task-Definition `Main`
        // selbst), keine Keywords gemischt.
        At("""
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

           """)
            .Offers(Task("Sub"), Task("Main"));
    }

    [Test]
    public void KeywordItems_CarryTheirDescription() {

        // Auf Member-Ebene werden `task`/`taskref` angeboten — jedes Keyword-Item trägt seine Bedeutung
        // (einzige Autorität: SyntaxFacts), damit LSP-/VS-Client sie im Doku-Panel zeigen können.
        var r = At("|");

        r.Item("task")   .HasDescription(SyntaxFacts.GetKeywordDescription(SyntaxFacts.TaskKeyword));
        r.Item("taskref").HasDescription(SyntaxFacts.GetKeywordDescription(SyntaxFacts.TaskrefKeyword));
    }

    [Test]
    public void EdgeKeywordItems_CarryTheirDescription() {

        // Hinter einem Quellknoten werden die Edge-Operatoren angeboten — jede Kante ist ein konkretes
        // Keyword-Literal mit fester Bedeutung, also trägt auch das Edge-Item seine Beschreibung.
        At("""
           task Main
           {
               init i;
               exit e;
               i |
           }

           """)
            .Item("-->").HasDescription(SyntaxFacts.GetKeywordDescription(SyntaxFacts.GoToEdgeKeyword));
    }

    [Test]
    public void AfterNodeColon_OffersExitConnectionPoints() {

        // Task-Knoten Sub (Typ taskref Sub mit exit se); Caret (|) direkt hinter `Sub:`. Nach dem
        // Doppelpunkt nur die Exit-Connection-Points — keine Edge-Keywords, keine Knoten.
        At("""
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

           """)
            .Offers(ConnectionPoint("se"));
    }

    [Test]
    public void TargetSlot_OffersTargetNodesAndEndKeyword() {

        // Caret (|) im Ziel-Slot hinter `--> `: hinter der Edge stehen nur die Knoten, die als ZIEL taugen
        // (ITargetNodeSymbol) — e (exit, nur Ziel) und Sub (task-Knoten) — plus das Ziel-Keyword `end`. Der
        // `init`-Knoten `i` (nur Quelle) fehlt bewusst, ebenso Deklarations-/Edge-Keywords und Folge-Klauseln.
        At("""
           task Main
           {
               init i;
               exit e;
               task Sub;
               i --> |Sub;
           }

           """)
            .Offers(ConnectionPoint("e"), Task("Sub"), Keyword(SyntaxFacts.EndKeyword));
    }

    [Test]
    public void TargetSlot_WithEndNode_OffersEndExactlyOnce() {

        // Regression zu den zwei `end`-Einträgen: existiert ein End-Knoten (dessen Name IST `end`), darf `end`
        // im Ziel-Slot trotzdem nur EINMAL erscheinen — als Ziel-Keyword, nicht zusätzlich als benannte
        // Knoten-Referenz. Ein End-Ziel schreibt man ausschließlich über das `end`-Schlüsselwort. Das exakte
        // Multiset fängt eine etwaige Verdopplung automatisch als „überzählig".
        At("""
           task A
           {
               init i;
               end;
               i --> |end;
           }

           """)
            .Offers(Keyword(SyntaxFacts.EndKeyword));
    }

    [Test]
    public void TargetSlot_UnderVersion1_DoesNotOfferCancelKeyword() {

        // `cancel` ist ein Version-2-Zielkeyword (dieselbe Nav5000-Gate-Autorität wie Continuation/choice-params).
        // Unter #version 1 (Default) wird es im Ziel-Slot NICHT angeboten — sonst böte die Completion einen
        // Vorschlag an, der beim Commit sofort Nav5000 würfe. `end` (versionsunabhängig) bleibt. Die exakte
        // Offers-Menge belegt zugleich die Abwesenheit von `cancel`; das DoesNotOffer macht sie explizit.
        At("""
           task Main
           {
               init i;
               exit e;
               task Sub;
               i --> |Sub;
           }

           """)
            .Offers(ConnectionPoint("e"), Task("Sub"), Keyword(SyntaxFacts.EndKeyword))
            .DoesNotOffer(SyntaxFacts.CancelKeyword);
    }

    [Test]
    public void TargetSlot_UnderVersion2_AlsoOffersCancelKeyword() {

        // Ab #version 2 tritt `cancel` als deklarationsloses Ziel-Keyword neben `end` (analog `end`, hinter dem
        // Versionsgate). Der Ziel-Slot bietet dann: die Zielknoten + `end` + `cancel`.
        At("""
           #version 2

           task Main
           {
               init i;
               exit e;
               task Sub;
               i --> |Sub;
           }

           """)
            .Offers(ConnectionPoint("e"), Task("Sub"),
                    Keyword(SyntaxFacts.EndKeyword), Keyword(SyntaxFacts.CancelKeyword));
    }

    [Test]
    public void EdgeSlot_OffersOnlyVisibleEdgeKeywords() {

        // Caret (|) hinter dem Quellknoten `i`, vor der Edge `-->`. Hinter dem Quellknoten kann nur eine Edge
        // folgen — genau die sichtbaren Edge-Keywords (`-->`, `o->`), NICHT das versteckte `==>`, keine
        // Continuation-Kanten (--^/o-^), keine Knoten und keine sonstigen Keywords.
        At("""
           task A
           {
               init i;
               exit e;
               task Sub;
               i |--> Sub;
           }

           """)
            .Offers(Keyword(SyntaxFacts.GoToEdgeKeyword), Keyword(SyntaxFacts.ModalEdgeKeyword));
    }

    [Test]
    public void StatementStart_OffersNodeDeclarationKeywordsAndNodes() {

        // Leere, eingerückte Zeile im Deklarations-Block — Caret (|) am Satzanfang. Angeboten werden die
        // Knoten-Deklarations-Keywords UND die vorhandenen Knoten, die als QUELLE einer neuen Transition taugen
        // (ISourceNodeSymbol): `i` (init). Der `exit`-Knoten `e` (nur Ziel) fehlt — auch wenn `exit` als
        // Deklarations-Keyword angeboten wird. `Init` (InitKeywordAlt) ist KEIN Keyword (nur der Symbol-Name des
        // Init-Knotens), Folge-Klauseln/`taskref`/Edges gehören hier nicht her.
        At("""
           task A
           {
               init i;
               exit e;
               |
               i --> e;
           }

           """)
            .Offers(ConnectionPoint("i"),
                    Keyword(SyntaxFacts.InitKeyword),
                    Keyword(SyntaxFacts.EndKeyword),
                    Keyword(SyntaxFacts.ExitKeyword),
                    Keyword(SyntaxFacts.ChoiceKeyword),
                    Keyword(SyntaxFacts.DialogKeyword),
                    Keyword(SyntaxFacts.ViewKeyword),
                    Keyword(SyntaxFacts.TaskKeyword));
    }

    [Test]
    public void UnnamedInit_NotOfferedAsDuplicateKeyword() {

        // Ein unbenannter Init-Knoten (`init;`) trägt den Symbol-Namen `Init` (InitKeywordAlt). Am Satzanfang
        // wird er als Quellknoten (ISourceNodeSymbol) über AddNodeReferences angeboten — GENAU EINMAL, als
        // Connection-Point. Früher fügte die Keyword-Liste `Init` ein zweites Mal (als Keyword) hinzu; das exakte
        // Multiset würde eine solche Verdopplung als „überzählig" melden. Das kleingeschriebene `init` bleibt als
        // Deklarations-/Transitions-Opener erhalten.
        At("""
           task A
           {
               init;
               exit e;
               |
               init --> e;
           }

           """)
            .Offers(ConnectionPoint(SyntaxFacts.InitKeywordAlt),
                    Keyword(SyntaxFacts.InitKeyword),
                    Keyword(SyntaxFacts.EndKeyword),
                    Keyword(SyntaxFacts.ExitKeyword),
                    Keyword(SyntaxFacts.ChoiceKeyword),
                    Keyword(SyntaxFacts.DialogKeyword),
                    Keyword(SyntaxFacts.ViewKeyword),
                    Keyword(SyntaxFacts.TaskKeyword));
    }

    [Test]
    public void TransitionStart_OffersOnlySourceNodesAndInit() {

        // Der Task-Body ist zweigeteilt: erst Knoten-Deklarationen, dann Transitionen. Steht der Cursor am
        // Satzanfang HINTER einer Transition, ist der Deklarations-Block abgeschlossen — nur noch eine weitere
        // Transition kann folgen. Also: quellfähige Knoten (i, c) + `init` (Init-Transition), aber KEINE
        // Deklarations-Keywords und NICHT der `exit`-Knoten `e` (nur Ziel).
        At("""
           task A
           {
               init i;
               exit e;
               choice c;
               i --> c;
               |
               c --> e;
           }

           """)
            .Offers(ConnectionPoint("i"), Choice("c"), Keyword(SyntaxFacts.InitKeyword));
    }

    [Test]
    public void AfterTarget_FromInitSource_OffersConditionsAndDo() {

        // Caret (|) hinter dem vollständigen Ziel `e ` (Whitespace), vor `;`. Die Quelle `i` ist ein init-Knoten:
        // nach init ist ein Signal-Trigger `on` NICHT zulässig (Nav0200) und wird daher auch NICHT angeboten —
        // es bleiben die Bedingungs-Klauseln `if`/`else` und `do`. Continuation-Kanten sind ein Version-2-
        // Feature und werden unter #version 1 (Default) NICHT angeboten. Keine Knoten, keine Deklarations-Keywords.
        At("""
           task A
           {
               init i;
               exit e;
               i --> e |;
           }

           """)
            .Offers(Keyword(SyntaxFacts.IfKeyword),
                    Keyword(SyntaxFacts.ElseKeyword),
                    Keyword(SyntaxFacts.DoKeyword));
    }

    [Test]
    public void AfterTarget_FromGuiSource_OffersTriggerAndDo() {

        // Der Screenshot-Fall: die Quelle `V` ist ein GUI-Knoten (view) → Trigger-Transition. Dort ist der
        // Trigger `on` zulässig, Bedingungen `if`/`else` dagegen NICHT (Nav0220) und werden daher auch NICHT
        // angeboten — es bleiben `on` und `do`. Caret (|) hinter dem vollständigen Ziel `d ` (Whitespace).
        At("""
           task A
           {
               init i;
               exit e;
               view V;
               dialog d;
               i --> V;
               V o-> d |;
           }

           """)
            .Offers(Keyword(SyntaxFacts.OnKeyword),
                    Keyword(SyntaxFacts.DoKeyword));
    }

    [Test]
    public void AfterTarget_FromChoiceSource_OffersConditionsAndDo() {

        // Die Quelle `c` ist ein choice-Knoten → jeder Trigger ist unzulässig (Nav0203), Bedingungen dagegen
        // zulässig. Angeboten werden daher `if`/`else`/`do`, kein `on`. Caret (|) hinter dem Ziel `e `.
        At("""
           task A
           {
               init i;
               exit e;
               choice c;
               i --> c;
               c --> e |;
           }

           """)
            .Offers(Keyword(SyntaxFacts.IfKeyword),
                    Keyword(SyntaxFacts.ElseKeyword),
                    Keyword(SyntaxFacts.DoKeyword));
    }

    [Test]
    public void AfterTarget_FromExitSource_OffersOnlyIfAndDo() {

        // Exit-Transition (`Sub:se --> …`): grammatisch kann sie keinen Trigger tragen und nur eine
        // `if`-Bedingung (Nav0221) — kein `else`, kein `on`. Angeboten werden daher nur `if` und `do`.
        // Caret (|) hinter dem Ziel `e ` der Exit-Transition.
        At("""
           taskref Sub
           {
               init si;
               exit se;
           }

           task A
           {
               init i;
               exit e;
               task Sub;
               i --> Sub;
               Sub:se --> e |;
           }

           """)
            .Offers(Keyword(SyntaxFacts.IfKeyword),
                    Keyword(SyntaxFacts.DoKeyword));
    }

    [Test]
    public void AfterTarget_FromInitSource_UnderVersion2_AlsoOffersContinuationEdges() {

        // Wie AfterTarget_FromInitSource, aber ab #version 2: hinter dem Ziel darf zusätzlich eine Continuation
        // (`o-^`/`--^`) folgen — dieselbe Gate-Autorität wie Nav5000. Die Continuation hängt am Ziel, nicht am
        // Quellknoten, und ist daher vom SourceKind-Pruning unberührt; `on` bleibt (init-Quelle) trotzdem weg.
        // Caret (|) hinter dem vollständigen Ziel `V ` (Whitespace).
        At("""
           #version 2
           task A
           {
               init i;
               exit e;
               view V;
               i --> V |;
           }

           """)
            .Offers(Keyword(SyntaxFacts.IfKeyword),
                    Keyword(SyntaxFacts.ElseKeyword),
                    Keyword(SyntaxFacts.DoKeyword),
                    Keyword(SyntaxFacts.ContinuationModalEdgeKeyword),  // o-^
                    Keyword(SyntaxFacts.ContinuationGoToEdgeKeyword));  // --^
    }

    [Test]
    public void ContinuationTargetSlot_AfterModalContinuation_OffersOnlyTaskNodes() {

        // Der Fall aus dem Screenshot: hinter der modalen Continuation-Kante `o-^` (leeres Ziel) darf NUR ein
        // Task-Knoten als Continuation-Ziel folgen (Analyzer Nav0121) — kein GUI-Knoten, keine Choice, kein
        // Connection-Point und keine Deklarations-/Edge-Keywords. Bisher fiel diese Stelle auf den pauschalen
        // Fallback zurück. Caret (|) hinter `o-^ ` (Whitespace), im Continuation-Ziel-Slot.
        At("""
           #version 2
           task A
           {
               init i;
               exit e;
               view V;
               choice c;
               task T;
               i --> V o-^ |;
           }

           """)
            .Offers(Task("T"));
    }

    [Test]
    public void ContinuationTargetSlot_AfterGoToContinuation_OffersOnlyTaskNodes() {

        // Wie oben, aber mit der GoTo-Continuation-Kante `--^`. Auch hier gilt: nur Task-Knoten als Ziel.
        // Caret (|) hinter `--^ ` (Whitespace).
        At("""
           #version 2
           task A
           {
               init i;
               exit e;
               view V;
               task T;
               i --> V --^ |;
           }

           """)
            .Offers(Task("T"));
    }

    [Test]
    public void AfterContinuationTarget_OffersClausesButNoFurtherContinuation() {

        // Hinter einem VOLLSTÄNDIGEN Continuation-Ziel (`o-^ T `) folgen die Klauseln on/if/else/do — aber
        // KEINE weitere Continuation-Kante: eine Continuation ist nicht verkettbar. Ohne den eigenen
        // AfterContinuationTarget-Zweig böte AfterTarget hier `o-^`/`--^` fälschlich erneut an.
        // Caret (|) hinter dem gefüllten Continuation-Ziel `T ` (Whitespace).
        At("""
           #version 2
           task A
           {
               init i;
               exit e;
               view V;
               task T;
               i --> V o-^ T |;
           }

           """)
            .Offers(Keyword(SyntaxFacts.OnKeyword),
                    Keyword(SyntaxFacts.IfKeyword),
                    Keyword(SyntaxFacts.ElseKeyword),
                    Keyword(SyntaxFacts.DoKeyword));
    }

    [Test]
    public void AfterTrigger_OffersConditionClausesAndDo() {

        // Vollständiger (spontaner) Trigger: der Kontext-Anker ist das Trigger-Keyword selbst (Parent
        // TriggerSyntax) → AfterTrigger. Danach folgen Bedingungs-Klausel (`if`/`else`) und `do` — aber KEIN
        // weiteres `on`. Caret (|) hinter dem spontanen Trigger `spontaneous ` (Whitespace), vor `;`.
        At("""
           task A
           {
               init i;
               exit e;
               i --> e spontaneous |;
           }

           """)
            .Offers(Keyword(SyntaxFacts.IfKeyword),
                    Keyword(SyntaxFacts.ElseKeyword),
                    Keyword(SyntaxFacts.DoKeyword));
    }

    [Test]
    public void AfterDoKeyword_OffersNothing() {

        // Hinter `do` steht der Wert-Slot: ein freier C#-Aufruf (identifierOrString), kein Nav-Konstrukt.
        // Es darf hier nichts angeboten werden (früher streute der Fallback pauschal Knoten + Keywords ein).
        // Caret (|) hinter `do ` (Whitespace), im Wert-Slot.
        At("""
           task A
           {
               init i;
               exit e;
               i --> e do |
           }

           """)
            .OffersNothing();
    }

    [Test]
    public void AfterFilledSignalTrigger_OffersConditionClausesAndDo() {

        // Vollständiger `on Signal`-Trigger: der Kontext-Anker ist das Signal (direkter Parent = Identifier-Wert,
        // tragende Rolle erst der Trigger darüber). Über die Ancestor-Kette wird das als AfterTrigger erkannt
        // (nicht als pauschaler Fallback) → if/else/do, aber kein zweites `on`, keine Knoten, keine Edges.
        // Caret (|) hinter dem gefüllten `on Signal `-Trigger (Whitespace).
        At("""
           task A
           {
               init i;
               exit e;
               i --> e on Signal |
           }

           """)
            .Offers(Keyword(SyntaxFacts.IfKeyword),
                    Keyword(SyntaxFacts.ElseKeyword),
                    Keyword(SyntaxFacts.DoKeyword));
    }

    [Test]
    public void AfterTrigger_FromGuiSource_OffersOnlyDo() {

        // GUI-Quelle (`V` = view) mit gesetztem Trigger `on Sig`: die Transition IST eine Trigger-Transition,
        // Bedingungen sind dort unzulässig (Nav0220) — hinter dem Trigger bleibt daher nur `do`, kein if/else.
        // Caret (|) hinter dem gefüllten `on Sig `-Trigger (Whitespace).
        At("""
           task A
           {
               init i;
               exit e;
               view V;
               dialog d;
               i --> V;
               V o-> d on Sig |
           }

           """)
            .Offers(Keyword(SyntaxFacts.DoKeyword));
    }

    [Test]
    public void AfterFilledCondition_OffersOnlyDo() {

        // Vollständige `if Bedingung`-Klausel: Anker ist der Bedingungs-Wert, tragende Rolle die Bedingung
        // darüber → AfterCondition (nur `do`), statt Fallback. Kein weiteres if/else/on, keine Knoten.
        // Caret (|) hinter der gefüllten `if Cond `-Bedingung (Whitespace).
        At("""
           task A
           {
               init i;
               exit e;
               i --> e if Cond |
           }

           """)
            .Offers(Keyword(SyntaxFacts.DoKeyword));
    }

    [Test]
    public void EdgeSlot_AfterPartialModalEdge_OffersEdgeKeywordsNotClauses() {

        // `View o-|`: der Cursor klebt an der gerade angefangenen modalen Kante `o->`. Das führende `o` lext der
        // Lexer als Identifier (ein `o` allein bildet kein Edge-Token), und der Parser hängt es als vermeintliches
        // Ziel `o` in den Baum. Lückenlos an das `-` geklebt ist es aber der Auftakt der Kante — der Kontext bleibt
        // der Quellknoten `View` (EdgeSlot), nicht das Ziel. Angeboten werden daher die Edge-Keywords (der Host
        // filtert per `o-`-Präfix auf `o->`), NICHT die Folge-Klauseln do/on/if/else.
        At("""
           task A
           {
               init i;
               exit e;
               view View;
               dialog MsgExit;
               i --> View;
               View o-|
           }

           """)
            .Offers(Keyword(SyntaxFacts.GoToEdgeKeyword),
                    Keyword(SyntaxFacts.ModalEdgeKeyword));
    }

    [Test]
    public void EdgeSlot_CommitModalEdge_AfterPartialModalEdge_ReplacesTypedChars() {

        // Commit von `o->` an `View o-|` ersetzt die bereits getippten Zeichen `o-`, statt sie zu `o-o->` zu
        // verdoppeln — der Operator-Ersetzungsbereich deckt den ganzen Edge-Lauf (inkl. des führenden `o`) ab.
        At("""
           task A
           {
               init i;
               exit e;
               view View;
               dialog MsgExit;
               i --> View;
               View o-|
           }

           """)
            .Commit(SyntaxFacts.ModalEdgeKeyword)
            .Produces("""
                      task A
                      {
                          init i;
                          exit e;
                          view View;
                          dialog MsgExit;
                          i --> View;
                          View o->
                      }

                      """);
    }

    [Test]
    public void AfterFilledDo_OffersNothing() {

        // Vollständige `do Aufruf`-Klausel: Anker ist der Aufruf-Wert, tragende Rolle die do-Klausel darüber
        // → Suppress (freier C#-Aufruf), statt Fallback. Caret (|) hinter dem gefüllten `do Call `-Aufruf.
        At("""
           task A
           {
               init i;
               exit e;
               i --> e do Call |
           }

           """)
            .OffersNothing();
    }

    [Test]
    public void InitNodeTail_AfterName_OffersOnlyDo() {

        // Hinter dem Namen einer init-Knoten-Deklaration folgt grammatisch nur noch die optionale `do`-Klausel
        // (und, über `[`, die Code-Blöcke) bzw. das abschließende `;`. Statt des pauschalen Fallbacks darf hier
        // NUR `do` erscheinen. Caret (|) hinter `init i ` (Whitespace), vor dem `;`.
        At("""
           task A
           {
               init i |;
               exit e;
               i --> e;
           }

           """)
            .Offers(Keyword(SyntaxFacts.DoKeyword));
    }

    [Test]
    public void InitNodeTail_WithExistingDoClause_OffersNothing() {

        // Ist die `do`-Klausel bereits vorhanden, gibt es im „Schwanz" davor nichts mehr anzubieten (die
        // Singleton-`do`-Klausel darf nicht ein zweites Mal vorgeschlagen werden).
        // Caret (|) hinter `init i ` (Whitespace), vor der bereits vorhandenen `do`-Klausel.
        At("""
           task A
           {
               init i |do Foo;
               exit e;
               i --> e;
           }

           """)
            .OffersNothing();
    }

    [Test]
    public void NodeDeclarationTail_AfterExitName_OffersNothing() {

        // Hinter dem Namen einer exit-Knoten-Deklaration folgt grammatisch nur noch das `;`. Der pauschale
        // Fallback wird hier zu Suppress präzisiert. Caret (|) hinter `exit e ` (Whitespace), vor dem `;`.
        At("""
           task A
           {
               init i;
               exit e |;
               i --> e;
           }

           """)
            .OffersNothing();
    }

    [Test]
    public void NodeDeclarationTail_AfterChoiceName_OffersNothing() {

        // Caret (|) hinter `choice c ` (Whitespace), vor dem `;` — grammatisch folgt nur noch das `;`.
        At("""
           task A
           {
               init i;
               exit e;
               choice c |;
               i --> c;
               c --> e;
           }

           """)
            .OffersNothing();
    }

    [Test]
    public void NodeDeclarationTail_AfterTaskNodeName_OffersNothing() {

        // Der Task-Knoten ist der subtile Fall: hinter dem Referenznamen kann noch ein Alias (freier Bezeichner)
        // sowie `[donotinject]`/`[abstractmethod]` folgen — allesamt nichts, was die Completion anbietet. Also
        // Suppress statt Fallback. Caret (|) hinter `task Sub ` (Whitespace), vor dem `;`.
        At("""
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

           """)
            .OffersNothing();
    }

    [Test]
    public void MemberLevel_OffersOnlyTaskAndTaskref() {

        // Caret (|) ganz am Dateianfang — außerhalb jeder Task-Definition. Keine knoten-/transitionsbezogenen
        // Vorschläge auf Member-Ebene.
        At("""
           |task A
           {
               init i;
               exit e;
               i --> e;
           }

           """)
            .Offers(Keyword(SyntaxFacts.TaskKeyword), Keyword(SyntaxFacts.TaskrefKeyword));
    }

    [Test]
    public void TaskRefBody_AfterOpenBrace_OffersConnectionPointKeywords() {

        // Im Body einer taskref-Deklaration erlaubt die Grammatik nur Connection-Point-Deklarationen
        // (init/exit/end). Ein taskref ist KEINE Task-Definition — früher fiel der Kontext auf die
        // Member-Ebene zurück und bot fälschlich task/taskref an. Caret (|) direkt hinter dem `{`.
        At("""
           taskref Sub
           {|
               init si;
               exit se;
           }

           """)
            .Offers(Keyword(SyntaxFacts.InitKeyword),
                    Keyword(SyntaxFacts.ExitKeyword),
                    Keyword(SyntaxFacts.EndKeyword));
    }

    [Test]
    public void TaskRefBody_AfterConnectionPointSemicolon_OffersConnectionPointKeywords() {

        // Caret (|) am Satzanfang hinter dem `;` einer Connection-Point-Deklaration.
        At("""
           taskref Sub
           {
               init si;|
               exit se;
           }

           """)
            .Offers(Keyword(SyntaxFacts.InitKeyword),
                    Keyword(SyntaxFacts.ExitKeyword),
                    Keyword(SyntaxFacts.EndKeyword));
    }

    [Test]
    public void TaskRefBody_InConnectorNameSlot_OffersNothing() {

        // Hinter dem Connection-Point-Keyword steht der Connector-Name — ein freier, neu vergebener Bezeichner.
        // Dort gibt es nichts anzubieten (und schon gar nicht task/taskref der Member-Ebene).
        // Caret (|) hinter `exit ` im Connector-Name-Slot.
        At("""
           taskref Sub
           {
               exit |se;
           }

           """)
            .OffersNothing();
    }

    [Test]
    public void InSingleLineComment_OffersNothing() {

        // Caret (|) mitten im Zeilenkommentar.
        At("""
           task A
           {
               // h|ier nix
               init i;
               exit e;
               i --> e;
           }

           """)
            .OffersNothing();
    }

    [Test]
    public void InStringLiteral_OffersNothing() {

        // Caret (|) innerhalb der Zeichenkette "Sub.nav".
        At("""
           taskref "S|ub.nav";

           task A
           {
               init i;
               exit e;
               i --> e;
           }

           """)
            .OffersNothing();
    }

    [Test]
    public void InCodeBlock_OffersNothing() {

        // Caret (|) im C#-Inhalt des Code-Blocks.
        At("""
           [using F|oo]

           task A
           {
               init i;
               exit e;
               i --> e;
           }

           """)
            .OffersNothing();
    }

    [Test]
    public void InMultilineCodeBlock_OffersNothing() {

        // Mehrzeiliger, an einem Wirt (init-Knoten) hängender Code-Block: das öffnende `[` steht auf einer
        // FRÜHEREN Zeile als der Cursor. Der zeilenbegrenzte Klammer-Scan sieht es dort nicht und streute
        // Fallback-Vorschläge ein; die baumbasierte Erkennung (Kontext-Anker im geparsten CodeSyntax-Knoten)
        // unterdrückt korrekt über die Zeilengrenze hinweg. Caret (|) im C#-Inhalt, eine Zeile unter dem `[`.
        At("""
           task A
           {
               init i [params
                   Fo|o];
               exit e;
               i --> e;
           }

           """)
            .OffersNothing();
    }

    [Test]
    public void InCodeBlockKeywordSlot_AtFileLevel_OffersOnlyUsingAndNamespacePrefix() {

        // Caret (|) im Schlüsselwort-Slot direkt hinter `[`. Auf Datei-Ebene erlaubt die Grammatik nur `using`
        // und `namespaceprefix` — keine Code-Keywords anderer Wirte, keine Nav-Sprach-Keywords/Knoten und keine
        // versteckten Code-Keywords (`notimplemented`).
        At("""
           [u|sing Foo]

           task A
           {
               init i;
               exit e;
               i --> e;
           }

           """)
            .Offers(Keyword(SyntaxFacts.UsingKeyword), Keyword(SyntaxFacts.NamespaceprefixKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_InTaskHeader_OffersTaskHeaderCodeKeywords() {

        // Code-Block im task-Definitions-Kopf (nach `task A`, vor dem Body-`{`) — erlaubt
        // code/base/generateto/params/result, NICHT die Datei-/Knoten-Keywords. Frisch getippter, leerer
        // Code-Block — Caret (|) hinter `[`.
        At("""
           task A
           [|]
           {
               init i;
               exit e;
               i --> e;
           }

           """)
            .Offers(Keyword(SyntaxFacts.CodeKeyword),
                    Keyword(SyntaxFacts.BaseKeyword),
                    Keyword(SyntaxFacts.GeneratetoKeyword),
                    Keyword(SyntaxFacts.ParamsKeyword),
                    Keyword(SyntaxFacts.ResultKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_OnInitNode_OffersAbstractMethodAndParams() {

        // Code-Block an einem init-Knoten (`init i [ … ];`). Caret (|) hinter dem `[`.
        At("""
           task A
           {
               init i [|];
               exit e;
               i --> e;
           }

           """)
            .Offers(Keyword(SyntaxFacts.AbstractmethodKeyword), Keyword(SyntaxFacts.ParamsKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_OnChoiceNode_UnderVersion1_OffersNothing() {

        // Der choice-Knoten kennt als einziges Code-Keyword `params` — das aber ist ein Version-2-Feature.
        // Unter #version 1 (Default) wird es NICHT angeboten, sonst böte die Completion einen Vorschlag an,
        // der sofort Nav5000 würfe → hier bleibt nichts übrig. Caret (|) hinter dem `[` am choice-Knoten.
        At("""
           task A
           {
               init i;
               exit e;
               choice c [|];
               i --> c;
               c --> e;
           }

           """)
            .OffersNothing();
    }

    [Test]
    public void InCodeBlockKeywordSlot_OnChoiceNode_UnderVersion2_OffersParams() {

        // Ab #version 2 ist die Choice-`[params]`-Klausel verfügbar (dieselbe Gate-Autorität wie Nav5000).
        // Caret (|) hinter dem `[` am choice-Knoten.
        At("""
           #version 2
           task A
           {
               init i;
               exit e;
               choice c [|];
               i --> c;
               c --> e;
           }

           """)
            .Offers(Keyword(SyntaxFacts.ParamsKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_OnTaskNode_OffersDoNotInjectAndAbstractMethod() {

        // Code-Block an einem task-Knoten (`task Sub [ … ];`). Caret (|) hinter dem `[`.
        At("""
           task A
           {
               init i;
               exit e;
               task Sub [|];
               i --> e;
           }

           """)
            .Offers(Keyword(SyntaxFacts.DonotinjectKeyword), Keyword(SyntaxFacts.AbstractmethodKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_InTaskRef_OffersTaskRefCodeKeywords() {

        // Code-Block in einer taskref-Deklaration (nach `taskref Sub`, vor dem Body-`{`). taskref erlaubt
        // namespaceprefix + result (notimplemented ist versteckt). Caret (|) hinter dem `[`.
        At("""
           taskref Sub
           [|]
           {
               init si;
               exit se;
           }

           """)
            .Offers(Keyword(SyntaxFacts.NamespaceprefixKeyword), Keyword(SyntaxFacts.ResultKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_InTaskRef_WithFollowingSingletons_OffersNothing() {

        // Frischer (leerer) Block zwischen `[namespaceprefix …]` und den folgenden `[notimplemented]`/
        // `[result …]`. Alle sichtbaren Singletons des taskref-Kopfs (namespaceprefix, result) sind — vor
        // wie nach dem Caret — bereits vorhanden → es bleibt nichts mehr anzubieten. Vor dem Parser-Fix
        // verschluckte das leere `[]` die nachfolgenden Blöcke, sodass `result` fälschlich noch erschien.
        // Caret (|) im leeren Block direkt hinter `[namespaceprefix NS.2]`.
        At("""
           taskref TR1 [namespaceprefix NS.2][|]
                       [notimplemented]
                       [result RT1 r1]
           {
               init si;
               exit se;
           }

           """)
            .OffersNothing();
    }

    [Test]
    public void InCodeBlockKeywordSlot_InTaskHeader_WithExistingCode_OmitsCode() {

        // Der task-Kopf trägt bereits ein `[code …]`; ein zweiter Code-Block darf `code` (Singleton) nicht
        // erneut anbieten — die übrigen Kopf-Keywords bleiben. Frisch getippter zweiter Block — Caret (|) hinter `[`.
        At("""
           task A
           [code "Foo"]
           [|]
           {
               init i;
               exit e;
               i --> e;
           }

           """)
            .Offers(Keyword(SyntaxFacts.BaseKeyword),
                    Keyword(SyntaxFacts.GeneratetoKeyword),
                    Keyword(SyntaxFacts.ParamsKeyword),
                    Keyword(SyntaxFacts.ResultKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_InTaskHeader_WithFollowingCode_OmitsCode() {

        // Wie WithExistingCode_OmitsCode, aber der frische (leere) Block steht VOR dem vorhandenen `[code …]`.
        // Ein vorangestelltes malformes `[]` darf das nachfolgende, gültige `[code …]` nicht aus dem Baum
        // verschlucken — sonst böte die Completion `code` fälschlich erneut an. Caret (|) hinter `[`.
        At("""
           task A
           [|]
           [code "Foo"]
           {
               init i;
               exit e;
               i --> e;
           }

           """)
            .Offers(Keyword(SyntaxFacts.BaseKeyword),
                    Keyword(SyntaxFacts.GeneratetoKeyword),
                    Keyword(SyntaxFacts.ParamsKeyword),
                    Keyword(SyntaxFacts.ResultKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_AtFileLevel_WithNamespacePrefix_OmitsPrefixButKeepsUsing() {

        // Datei-Kopf mit bereits vorhandenem `[namespaceprefix …]` (Singleton) — dieses fällt weg, `using`
        // bleibt aber, weil die Grammatik es wiederholt erlaubt (`codeUsingDeclaration*`). Caret (|) im zweiten Block.
        At("""
           [namespaceprefix Foo]
           [|]

           task A
           {
               init i;
               exit e;
               i --> e;
           }

           """)
            .Offers(Keyword(SyntaxFacts.UsingKeyword));
    }

    [Test]
    public void InCodeBlockKeywordSlot_OnInitNode_WithExistingParams_OmitsParams() {

        // Ein init-Knoten mit bereits vorhandenem `[params …]`; ein zweiter Block bietet nur noch
        // `abstractmethod` an. Zweiter Block am init-Knoten — Caret (|) hinter dem letzten `[`.
        At("""
           task A
           {
               init i [params] [|];
               exit e;
               i --> e;
           }

           """)
            .Offers(Keyword(SyntaxFacts.AbstractmethodKeyword));
    }

    [Test]
    public void AfterHash_OffersOnlyVersionKeyword() {

        // Caret (|) direkt hinter dem `#`. Hinter `#` nur das Direktiv-Schlüsselwort `version` — `pragma` wird
        // bewusst NICHT angeboten (kein bekanntes Pragma), keine Sprach-Keywords/Knoten auf einer Direktiv-Zeile.
        At("""
           #|
           task A
           {
               init i;
               exit e;
               i --> e;
           }

           """)
            .Offers(Keyword(SyntaxFacts.VersionDirectiveKeyword));
    }

    [Test]
    public void WhileTypingDirectiveKeyword_StillOffersVersion() {

        // Caret (|) am Ende des gerade getippten Wort-Präfixes `v`. Das Wort `v` ist nur das Filter-Präfix —
        // der Kontext bleibt der Schlüsselwort-Slot, es wird weiterhin nur `version` angeboten.
        At("""
           #v|
           task A
           {
               init i;
           }

           """)
            .Offers(Keyword(SyntaxFacts.VersionDirectiveKeyword));
    }

    [Test]
    public void AfterVersionKeyword_OffersSupportedVersionNumbers() {

        // Trailing Space, kein Wert: der Caret steht am Trivia-Ende (`#version ` frisch getippt) — genau der
        // FindTrivia-[Start,End)-Fallstrick, den DirectiveAt über die inklusive Endgrenze abfängt. Angeboten
        // werden die gültigen Versionsnummern (heute nur `1`) aus NavLanguageVersion.SupportedVersions, kein
        // Schlüsselwort mehr. Caret (|) hinter `#version ` (Trailing Space) im Werte-Slot.
        At("""
           #version |
           task A
           {
               init i;
           }

           """)
            .Offers(NavLanguageVersion.SupportedVersions.Select(v => Keyword(v.ToString())).ToArray());
    }

    [Test]
    public void InsideUnknownPragmaSubject_OffersNothing() {

        // Caret (|) hinter dem `pragma`-Wort, im Subjekt-Slot. Tiefer in einer nicht erkannten Direktive gibt
        // es nichts anzubieten — auch kein Fallback.
        At("""
           #pragma |foo
           task A
           {
               init i;
           }

           """)
            .OffersNothing();
    }

    #endregion

    #region Ersetzungsbereiche (Edge-/Continuation-Spans über den Commit-Effekt)

    // Diese Suite prüft die Operator-Ersetzungsbereiche NICHT mehr über Offset-Arithmetik am
    // ReplacementExtent, sondern über den sichtbaren Commit-Effekt: `.Commit(keyword).Produces(text)`
    // wendet den Einfügetext des Vorschlags über seinen Ersetzungsbereich an und vergleicht das Resultat.
    // So steht im Test, was der Nutzer nach dem Commit im Editor sieht — Verdopplungen (`--->`, `--^--^`)
    // und Übergriffe in den Zielknoten fallen als falscher Ergebnistext auf.

    [Test]
    public void EdgeSlot_CommitBeforeExistingEdge_ReplacesItWithoutDuplicating() {

        // Caret (|) hinter dem Quellknoten `i`, VOR der bereits vorhandenen Edge `-->`. Der Commit desselben
        // Keywords ersetzt die vorhandene Edge, statt sie zu einer zweiten `-->` zu verdoppeln — der Text
        // bleibt unverändert.
        At("""
           task A
           {
               init i;
               exit e;
               task Sub;
               i |--> Sub;
           }

           """)
            .Commit(SyntaxFacts.GoToEdgeKeyword)
            .Produces("""
                      task A
                      {
                          init i;
                          exit e;
                          task Sub;
                          i --> Sub;
                      }

                      """);
    }

    [Test]
    public void EdgeSlot_CommitReplacesExistingModalEdge_NoDuplicate() {

        // Regression zu „i -->--> e": Caret (|) direkt VOR einer vorhandenen modalen Edge `o->`. Der Commit
        // von `-->` ersetzt die komplette vorhandene Edge (auch die Zeichen HINTER dem Caret), statt eine
        // zweite Edge einzufügen.
        At("""
           task A
           {
               init i;
               exit e;
               i |o-> e;
           }

           """)
            .Commit(SyntaxFacts.GoToEdgeKeyword)
            .Produces("""
                      task A
                      {
                          init i;
                          exit e;
                          i --> e;
                      }

                      """);
    }

    [Test]
    public void EdgeSlot_CommitStopsAtAdjacentTargetToken() {

        // Sicherheitsnetz gegen einen rohen Zeichen-Vorlauf: die Edge grenzt OHNE Leerzeichen an einen
        // Zielknoten, der mit einem Edge-Zeichen beginnt (`o1`). Der Commit von `-->` ersetzt NUR die Edge (das
        // Lexer-Token `-->`) und frisst sich nicht in das Ziel `o1` hinein — `o1` bleibt erhalten.
        At("""
           task A
           {
               init i;
               view o1;
               i |-->o1;
           }

           """)
            .Commit(SyntaxFacts.GoToEdgeKeyword)
            .Produces("""
                      task A
                      {
                          init i;
                          view o1;
                          i -->o1;
                      }

                      """);
    }

    [Test]
    public void EdgeSlot_CommitReplacesTypedEdgePrefix() {

        // Angefangene modale Edge (`o` getippt, Beginn von `o->`) hinter dem Quellknoten: das `o` ist zugleich
        // Bezeichner- und Edge-Zeichen und wird als Wort-Präfix behandelt (Kontext bleibt der Quellknoten →
        // EdgeSlot). Der Commit ersetzt das getippte `o` durch das vollständige Keyword (statt `oo->` zu erzeugen).
        At("""
           task A
           {
               init i;
               exit e;
               i o|
           }

           """)
            .Commit(SyntaxFacts.ModalEdgeKeyword) // o->
            .Produces("""
                      task A
                      {
                          init i;
                          exit e;
                          i o->
                      }

                      """);
    }

    [Test]
    public void PartialEdge_AfterSourceNode_OffersEdgeKeywordsAndCommitReplacesTypedDash() {

        // Angefangene Edge: der Nutzer hat hinter dem Quellknoten ein `-` getippt (Beginn von `-->`). Das
        // einzelne `-` ist kein gültiges Edge-Keyword und bleibt als unbekanntes, an die Wurzel gehängtes
        // Token übrig — trotzdem MUSS hier (wie beim `o` von `o->`) der Quellknoten-Kontext greifen und exakt
        // die sichtbaren Edge-Keywords anbieten (nicht die Member-Ebene task/taskref, keine Knoten). Der Commit
        // ersetzt das getippte `-` durch das vollständige Keyword (statt `--->` zu erzeugen).
        At("""
           task A
           {
               init i;
               exit e;
               i -|
           }

           """)
            .Offers(Keyword(SyntaxFacts.GoToEdgeKeyword), Keyword(SyntaxFacts.ModalEdgeKeyword))
            .Commit(SyntaxFacts.GoToEdgeKeyword)
            .Produces("""
                      task A
                      {
                          init i;
                          exit e;
                          i -->
                      }

                      """);
    }

    [Test]
    public void AfterTarget_CommitContinuationEdge_ReplacesTypedDash() {

        // Regression zu „---^": Hinter dem Ziel `V` hat der Nutzer ein `-` getippt (Beginn von `--^`). Wie die
        // regulären Edges ersetzt auch die Continuation-Kante das getippte `-`, statt `-` + `--^` = `---^` zu
        // erzeugen — für BEIDE Continuation-Keywords (`--^` und `o-^`).
        var r = At("""
                   #version 2
                   task A
                   {
                       init i;
                       exit e;
                       view V;
                       i --> V -|;
                   }

                   """);

        r.Commit(SyntaxFacts.ContinuationGoToEdgeKeyword) // --^
         .Produces("""
                   #version 2
                   task A
                   {
                       init i;
                       exit e;
                       view V;
                       i --> V --^;
                   }

                   """);

        r.Commit(SyntaxFacts.ContinuationModalEdgeKeyword) // o-^
         .Produces("""
                   #version 2
                   task A
                   {
                       init i;
                       exit e;
                       view V;
                       i --> V o-^;
                   }

                   """);
    }

    [Test]
    public void AfterTarget_CommitContinuationEdge_ReplacesExistingEdge_NoDuplicate() {

        // Caret (|) VOR einer bereits vorhandenen Continuation-Kante `--^`. Der Commit ersetzt die vorhandene
        // Kante komplett, statt sie zu `--^--^` zu verdoppeln — der Ergebnistext bleibt unverändert.
        At("""
           #version 2
           task A
           {
               init i;
               exit e;
               view V;
               task T;
               i --> V |--^ T;
           }

           """)
            .Commit(SyntaxFacts.ContinuationGoToEdgeKeyword)
            .Produces("""
                      #version 2
                      task A
                      {
                          init i;
                          exit e;
                          view V;
                          task T;
                          i --> V --^ T;
                      }

                      """);
    }

    #endregion

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

}
