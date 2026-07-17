#region Using Directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Completion;

/// <summary>
/// VS-freier Vervollständigungs-Service auf Engine-Ebene — Grundlage für LSP <c>textDocument/completion</c>
/// und (über dieselbe Logik) die VS-Quellen. Die grammatische Situation an der Cursor-Position wird über den
/// recovery-festen Syntaxbaum bestimmt (<see cref="NavCompletionContext"/>) statt über einen zeilenbegrenzten
/// Text-Rückwärtsscan; je nach Situation werden nur die dort tatsächlich sinnvollen Kategorien angeboten:
/// auf Member-Ebene <c>task</c>/<c>taskref</c>; hinter <c>task</c> die deklarierten Tasks; am Satzanfang im
/// Body die Knoten-Deklarations-Keywords samt vorhandenen Knoten; hinter einem Quellknoten die Edge-Keywords;
/// hinter einer Edge die Zielknoten (plus <c>end</c>, ab <c>#version 2</c> zusätzlich <c>cancel</c>); hinter
/// <c>knoten:</c> die Exit-Connection-Points; hinter
/// einem Ziel die je nach Quellknoten zulässigen Folge-Klauseln (GUI-Quelle <c>on</c>/<c>do</c>, init/choice
/// <c>if</c>/<c>else</c>/<c>do</c>, Exit-Transition <c>if</c>/<c>do</c>) — nie eine Klausel, die sofort einen
/// Analyzer-Fehler auslöste; im Schlüsselwort-Slot eines Code-Blocks
/// (direkt hinter <c>[</c>) die Code-Block-Keywords. Keine Vorschläge in Kommentaren, Zeichenketten
/// (<c>"…"</c>), im C#-Inhalt eines Code-Blocks oder im Wert-Slot hinter <c>do</c> (freier C#-Aufruf); innerhalb
/// von <c>taskref "…"</c> die Pfad-Vervollständigung.
/// </summary>
public static class NavCompletionService {

    /// <summary>
    /// Die kanonische Menge der Trigger-Zeichen (Trigger-Chars) der Completion — die Vereinigung aller
    /// Situationen, in denen ein einzelnes Sonderzeichen (also KEIN Bezeichner-Zeichen) automatisch eine
    /// Vervollständigung eröffnen soll. Einzige Autorität für beide Hosts: der LSP-Server speist damit
    /// <c>CompletionOptions.TriggerCharacters</c>, die VS-Quellen leiten daraus ihr Auslöse-Verhalten ab.
    /// Buchstaben lösen zusätzlich immer aus (Client- bzw. <c>char.IsLetter</c>-seitig) und sind hier daher
    /// bewusst NICHT enthalten. Die Pfadtrenner <c>/</c> und <c>\</c> sind bewusst KEINE Trigger-Zeichen: das
    /// Öffnen der Pfad-Liste in <c>taskref "…"</c> tragen bereits <c>"</c> und die Buchstaben, während ein
    /// auslösendes <c>/</c> außerhalb einer Zeichenkette (etwa am Beginn eines <c>//</c>-Kommentars) eine
    /// unangebrachte Vorschlagsliste eröffnete.
    /// </summary>
    public static readonly IReadOnlyList<char> TriggerCharacters = new[] {
        SyntaxFacts.Hash,          // '#'  — Direktiven (#version)
        SyntaxFacts.Colon,         // ':'  — Exit-Connection-Points hinter `knoten:`
        '-',                       // '-'  — Beginn einer Edge (-->)
        SyntaxFacts.OpenBracket,   // '['  — Code-Block-Keywords (do [ … ])
        '"'                        // '"'  — Beginn einer Zeichenkette (Pfad in taskref "…")
    };

    /// <summary>Ob <paramref name="c"/> ein kanonisches Trigger-Zeichen der Completion ist (siehe <see cref="TriggerCharacters"/>).</summary>
    public static bool IsTriggerCharacter(char c) {
        // Kleine, feste Menge — lineare Suche ist billiger als ein Set-Aufbau.
        for (var i = 0; i < TriggerCharacters.Count; i++) {
            if (TriggerCharacters[i] == c) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Die kanonische Menge der Abschluss-Zeichen (Commit-Chars): Zeichen, deren Eingabe bei offener
    /// Vorschlagsliste den markierten Vorschlag übernimmt und dann selbst eingefügt wird. Bewusst nur die
    /// Zeichen, die in der Nav-Grammatik ein Bezeichner-/Keyword-Token beenden oder trennen — Trenner
    /// (<c>, ;</c>), der Connection-Point-Doppelpunkt (<c>knoten:exit</c>) und die Zeichenketten-/Code-Block-
    /// Begrenzer (<c>" [ ]</c>). Der Punkt ist bewusst NICHT dabei — er ist in Nav ein gültiges
    /// Bezeichner-Zeichen (siehe <see cref="SyntaxFacts.IsIdentifierCharacter"/>), ein Commit darauf würde
    /// qualifizierte Namen zerreißen. Auch die Pfadtrenner <c>/</c> und <c>\</c> gehören bewusst NICHT dazu:
    /// Die Pfad-Vervollständigung ersetzt ohnehin den gesamten String-Inhalt (kein Segment-für-Segment-Commit),
    /// ein Commit auf <c>/</c> hängte den Trenner nur an den bereits eingefügten Pfad an — und außerhalb einer
    /// Zeichenkette zerlegte er einen gerade getippten <c>//</c>-Kommentar (das zweite <c>/</c> übernähme den
    /// vorselektierten Vorschlag). Einzige Autorität für beide Hosts: VS speist damit
    /// <c>IAsyncCompletionCommitManager.PotentialCommitCharacters</c>, der LSP-Server
    /// <c>CompletionOptions.AllCommitCharacters</c>.
    /// </summary>
    public static readonly IReadOnlyList<char> CommitCharacters = new[] {
        ' ',                        // Leerzeichen — Wort-/Bezeichner-Ende
        SyntaxFacts.Comma,          // ','  — Trennzeichen
        SyntaxFacts.Semicolon,      // ';'  — Anweisungs-Ende
        SyntaxFacts.Colon,          // ':'  — Connection-Point-Trenner (knoten:exit)
        '"',                        // '"'  — Zeichenketten-Begrenzer (taskref "…")
        SyntaxFacts.OpenBracket,    // '['  — Code-Block-Beginn
        SyntaxFacts.CloseBracket    // ']'  — Code-Block-Ende
    };

    /// <summary>
    /// Liefert die Vervollständigungs-Vorschläge zur angegebenen Zeichen-Position (0-basierter Offset)
    /// in der Reihenfolge, in der sie dem Nutzer angeboten werden sollen — oder eine leere Liste, wenn
    /// an der Position nichts vorgeschlagen werden soll.
    /// </summary>
    public static IReadOnlyList<NavCompletionItem> GetCompletions(CodeGenerationUnit unit, int position, NavSolution? solution = null) {

        var source = unit.Syntax.SyntaxTree.SourceText;

        // Pfad-Vervollständigung läuft INNERHALB von "…" nach `taskref` — die normale Unterdrückung in
        // Zeichenketten greift hier also bewusst nicht. Liefert null, wenn kein taskref-String-Kontext.
        // Pfad-Items tragen ihren Ersetzungsbereich (String-Inhalt) selbst und laufen daher an der
        // zentralen Operator-Normalisierung vorbei.
        var pathItems = GetPathCompletions(source, position, solution);
        if (pathItems != null) {
            return pathItems;
        }

        var context = NavCompletionContext.Classify(unit, position);
        var items   = BuildItems(context, unit);

        // Einzige Stelle, an der die Operator-Invariante durchgesetzt wird: operator-artige Vorschläge
        // (Edge-/Continuation-Keywords) bekommen ihren Ersetzungsbereich kategorie-übergreifend hier.
        return WithOperatorReplacements(items, unit.Syntax.SyntaxTree, position);
    }

    // Die zur grammatischen Situation passende Vorschlagsmenge — noch OHNE die operator-spezifischen
    // Ersetzungsbereiche (die hängt WithOperatorReplacements zentral an, damit keine Kategorie sie vergessen kann).
    static IReadOnlyList<NavCompletionItem> BuildItems(NavCompletionContext context, CodeGenerationUnit unit) {

        switch (context.Kind) {

            case NavCompletionContextKind.Suppress:
                return Array.Empty<NavCompletionItem>();

            // Direkt hinter `#`: das einzige derzeit sinnvolle Direktiv-Schlüsselwort. `pragma` wird bewusst
            // NICHT angeboten (es gibt kein bekanntes Pragma; siehe doc/nav-completion-status.md).
            case NavCompletionContextKind.DirectiveKeyword:
                return KeywordItems(SyntaxFacts.VersionDirectiveKeyword);

            // Hinter `#version `: die gültigen Sprach-Versionsnummern — aus derselben Autorität, die Nav5001
            // validiert (kein hartkodierter Wert).
            case NavCompletionContextKind.DirectiveVersionValue:
                return VersionValueItems();

            // Im Schlüsselwort-Slot eines Code-Blocks (`[ … ]`): die im jeweiligen Host zulässigen Code-Keywords.
            case NavCompletionContextKind.CodeBlock:
                return CodeBlockKeywordItems(context, unit.LanguageVersion);

            case NavCompletionContextKind.MemberLevel:
                return KeywordItems(SyntaxFacts.TaskKeyword, SyntaxFacts.TaskrefKeyword);

            // Im Body einer taskref-Deklaration: nur die Connection-Point-Deklarations-Keywords.
            case NavCompletionContextKind.ConnectionPointDeclaration:
                return KeywordItems(SyntaxFacts.InitKeyword, SyntaxFacts.ExitKeyword, SyntaxFacts.EndKeyword);

            case NavCompletionContextKind.TaskNodeName:
                return TaskDeclarationItems(unit);

            case NavCompletionContextKind.ExitConnectionPoint:
                return ExitConnectionPointItems(context);

            case NavCompletionContextKind.EdgeSlot:
                return VisibleEdgeKeywordItems();

            case NavCompletionContextKind.TargetSlot:
                return TargetItems(context, unit.LanguageVersion);

            case NavCompletionContextKind.ContinuationTargetSlot:
                return ContinuationTargetItems(context);

            case NavCompletionContextKind.StatementStart:
                return StatementStartItems(context);

            case NavCompletionContextKind.TransitionStart:
                return TransitionStartItems(context);

            case NavCompletionContextKind.AfterTarget:
                return AfterTargetItems(context, unit.LanguageVersion);

            // Wie AfterTarget, aber ohne die Continuation-Kanten: eine Continuation ist nicht verkettbar.
            case NavCompletionContextKind.AfterContinuationTarget:
                return KeywordItems(SyntaxFacts.OnKeyword, SyntaxFacts.IfKeyword,
                                    SyntaxFacts.ElseKeyword, SyntaxFacts.DoKeyword);

            case NavCompletionContextKind.AfterTrigger:
                return AfterTriggerItems(context);

            case NavCompletionContextKind.AfterCondition:
                return KeywordItems(SyntaxFacts.DoKeyword);

            // Im Tail einer init-Knoten-Deklaration folgt grammatisch nur noch die optionale `do`-Klausel.
            case NavCompletionContextKind.InitNodeTail:
                return KeywordItems(SyntaxFacts.DoKeyword);

            default:
                return FallbackItems(context, unit.LanguageVersion);
        }
    }

    // Die Operator-Invariante: Ein Vorschlag, dessen Einfügetext NICHT rein aus Bezeichner-Zeichen besteht
    // (die Edge-/Continuation-Keywords `-->`, `o->`, `--^`, `o-^` …), kann vom wort-basierten Ersetzungsbereich
    // des Hosts nicht abgedeckt werden — der reicht nur über Bezeichner-Zeichen. Ohne eigenen Bereich bliebe
    // ein bereits getipptes Operator-Zeichen beim Commit stehen (`-` + `--^` → `---^`). Deshalb bekommt hier
    // JEDES solche Item — kategorie-übergreifend und damit unvergesslich — den Operator-Ersetzungsbereich,
    // sofern es nicht schon einen eigenen trägt. Reine Wort-Items (Keywords, Namen, Versionswerte) verlassen
    // sich unverändert auf den Host-Wortersatz.
    static IReadOnlyList<NavCompletionItem> WithOperatorReplacements(IReadOnlyList<NavCompletionItem> items, SyntaxTree tree, int position) {

        if (items.Count == 0) {
            return items;
        }

        // Der Ersetzungsbereich hängt nur an Baum + Position (nicht am einzelnen Item) und ist für alle
        // Operator-Items derselbe — einmal berechnen und an jeden Treffer anhängen.
        var extent = OperatorReplacementExtent(tree, position);

        return items.ReplaceIf(
            item => item.ReplacementExtent == null && IsOperatorInsertText(item.InsertText),
            item => item.WithReplacementExtent(extent));
    }

    // Ein „operator-artiger" Einfügetext — Spiegelbild von NavCompletionContext.IsWordToken: nicht-leer und
    // NICHT vollständig aus Bezeichner-Zeichen. Erfasst `o->`/`o-^` korrekt (beginnen mit dem Bezeichner-Zeichen
    // `o`, sind aber keine Wörter) und lässt reine Wörter (`on`, `end`, Knotennamen, Versionswerte wie `2`) außen vor.
    static bool IsOperatorInsertText(string text) {
        return text.Length > 0 && !text.All(SyntaxFacts.IsIdentifierCharacter);
    }

    #region Kategorien

    // Knoten-Deklarations-Keywords (beginnen eine Knoten-Deklaration und taugen zugleich als Transitions-Quelle).
    // `Init`/InitKeywordAlt gehört bewusst NICHT dazu — das ist der Symbol-Name des Init-Knotens, kein
    // Lexer-Keyword (der Knoten wird über AddNodeReferences mit seinem echten Namen angeboten); als Keyword
    // eröffnet ausschließlich das kleingeschriebene `init` eine Init-Deklaration/-Transition.
    static readonly string[] NodeDeclarationKeywords = {
        SyntaxFacts.InitKeyword,
        SyntaxFacts.EndKeyword,
        SyntaxFacts.ExitKeyword,
        SyntaxFacts.ChoiceKeyword,
        SyntaxFacts.DialogKeyword,
        SyntaxFacts.ViewKeyword,
        SyntaxFacts.TaskKeyword
    };

    static List<NavCompletionItem> TaskDeclarationItems(CodeGenerationUnit unit) {
        var items = new List<NavCompletionItem>();
        foreach (var decl in unit.TaskDeclarations) {
            items.Add(FromSymbol(decl));
        }

        return items;
    }

    static List<NavCompletionItem> ExitConnectionPointItems(NavCompletionContext context) {

        var items = new List<NavCompletionItem>();

        if (string.IsNullOrEmpty(context.ExitNodeName)) {
            return items;
        }

        if (context.Task.TryFindNode(context.ExitNodeName) is ITaskNodeSymbol { Declaration: not null } exitNode) {
            // Erst die noch nicht verbundenen Exits, dann die bereits verbundenen.
            foreach (var cp in exitNode.GetUnconnectedExits()) {
                items.Add(FromSymbol(cp));
            }

            foreach (var cp in exitNode.GetConnectedExits()) {
                items.Add(FromSymbol(cp));
            }
        }

        return items;
    }

    // Hinter einer Edge: die Knoten, die als ZIEL taugen (ITargetNodeSymbol — also NICHT `init`), unreferenzierte
    // zuerst — plus das Ziel-Keyword `end`. End-Knoten werden bewusst NICHT als benannte Referenz mit angeboten:
    // ihr Name IST `end` (aus dem `end`-Schlüsselwort gebildet), und ein End-Ziel schreibt man ausschließlich über
    // dieses Schlüsselwort. Ohne den Ausschluss stünde `end` doppelt in der Liste (End-Knoten-Symbol + Keyword) —
    // bei mehreren `end`-Deklarationen sogar mehrfach.
    // Ab #version 2 zusätzlich das deklarationslose Ziel-Keyword `cancel` (dieselbe Nav5000-Gate-Autorität wie die
    // übrigen V2-Konstrukte): in V1 böte die Completion sonst einen Vorschlag an, der beim Commit sofort Nav5000
    // würfe. `cancel` hat — wie `end` — keinen Knoten und wird ausschließlich über sein Schlüsselwort geschrieben;
    // die Kanten-Modus-Restriktion (nur Goto, Nav0125) teilt es sich mit `end` (Nav0106) und wird — wie dort —
    // hier bewusst NICHT zusätzlich gepruned.
    static List<NavCompletionItem> TargetItems(NavCompletionContext context, NavLanguageVersion version) {
        var items = new List<NavCompletionItem>();
        AddNodeReferences(items, context.Task, n => n is ITargetNodeSymbol and not IEndNodeSymbol);
        items.Add(KeywordItem(SyntaxFacts.EndKeyword));

        if (NavLanguageFeatures.IsAvailable(NavLanguageFeature.Cancel, version)) {
            items.Add(KeywordItem(SyntaxFacts.CancelKeyword));
        }

        return items;
    }

    // Hinter einer Continuation-Kante (o-^/--^): das Ziel MUSS ein Task-Knoten sein (Analyzer Nav0121) —
    // daher nur ITaskNodeSymbol, weder die übrigen Zielknoten noch das `end`-Keyword. Würde hier ein
    // Nicht-Task angeboten, schlüge auf dem Commit sofort Nav0121 zu (dieselbe „nichts anbieten, was sofort
    // einen Fehler wirft"-Philosophie wie beim Versions-Gate der Continuation-Keywords).
    static List<NavCompletionItem> ContinuationTargetItems(NavCompletionContext context) {
        var items = new List<NavCompletionItem>();
        AddNodeReferences(items, context.Task, n => n is ITaskNodeSymbol);
        return items;
    }

    // Das `init`-Schlüsselwort — das einzige Keyword, das im Transitions-Block eine Anweisung eröffnen kann
    // (`init --> …`, die Init-Transition). Alle übrigen Deklarations-Keywords gehören in den Deklarations-Block.
    // (`Init`/InitKeywordAlt gehört bewusst NICHT dazu: das ist der Symbol-Name des Init-Knotens, kein Keyword —
    // der Init-Knoten selbst wird über AddNodeReferences mit seinem echten Namen angeboten.)
    static readonly string[] TransitionSourceKeywords = {
        SyntaxFacts.InitKeyword
    };

    // Satzanfang im Deklarations-Block: die vorhandenen Knoten, die als QUELLE einer (ersten) Transition taugen
    // (ISourceNodeSymbol — also NICHT `end`/`exit`), plus die Knoten-Deklarations-Keywords.
    static List<NavCompletionItem> StatementStartItems(NavCompletionContext context) {
        var items = new List<NavCompletionItem>();
        AddNodeReferences(items, context.Task, n => n is ISourceNodeSymbol);

        foreach (var keyword in NodeDeclarationKeywords
                                .Where(k => !SyntaxFacts.IsHiddenKeyword(k))
                                .OrderBy(k => k, StringComparer.Ordinal)) {
            items.Add(KeywordItem(keyword));
        }

        return items;
    }

    // Satzanfang im Transitions-Block: nur was eine Transition eröffnen kann — die quellfähigen Knoten
    // (ISourceNodeSymbol) plus die `init`-Schlüsselwörter (Init-Transition). KEINE Deklarations-Keywords, der
    // Deklarations-Block ist an dieser Stelle bereits abgeschlossen.
    static List<NavCompletionItem> TransitionStartItems(NavCompletionContext context) {
        var items = new List<NavCompletionItem>();
        AddNodeReferences(items, context.Task, n => n is ISourceNodeSymbol);

        foreach (var keyword in TransitionSourceKeywords
                                .Where(k => !SyntaxFacts.IsHiddenKeyword(k))
                                .OrderBy(k => k, StringComparer.Ordinal)) {
            items.Add(KeywordItem(keyword));
        }

        return items;
    }

    // Konservatives Alt-Verhalten für nicht eindeutig klassifizierbare Stellen: vorhandene Knoten +
    // sichtbare Nav-Keywords (ohne Edge-Keywords) + sichtbare Edge-Keywords. So wird nie weniger angeboten.
    // Den Ersetzungsbereich der Edge-Keywords hängt WithOperatorReplacements zentral an.
    // `cancel` ∈ NavKeywords, ist aber ein V2-Feature (Nav5000): hier wird es unter der effektiven #version
    // gegatet — sonst böte der Fallback es auch in V1 an (Loose End aus S1a, dieselbe Gate-Autorität wie
    // TargetItems), obwohl ein cancel-Ausgang dort sofort Nav5000 würfe.
    static List<NavCompletionItem> FallbackItems(NavCompletionContext context, NavLanguageVersion version) {
        var items = new List<NavCompletionItem>();
        AddNodeReferences(items, context.Task);

        foreach (var keyword in SyntaxFacts.NavKeywords
                                .Where(k => !SyntaxFacts.IsHiddenKeyword(k) && !SyntaxFacts.IsEdgeKeyword(k))
                                .Where(k => IsNavKeywordAvailable(k, version))
                                .OrderBy(k => k, StringComparer.Ordinal)) {
            items.Add(KeywordItem(keyword));
        }

        items.AddRange(VisibleEdgeKeywordItems());
        return items;
    }

    // Ob ein Nav-Keyword unter der effektiven Sprachversion überhaupt angeboten werden darf. Nur die
    // versions-gegateten Keywords werden hier eingeschränkt (heute: `cancel` = V2, Nav5000); alle übrigen
    // Nav-Keywords sind versionsneutral.
    static bool IsNavKeywordAvailable(string keyword, NavLanguageVersion version) {
        if (keyword == SyntaxFacts.CancelKeyword) {
            return NavLanguageFeatures.IsAvailable(NavLanguageFeature.Cancel, version);
        }

        return true;
    }

    // Die im jeweiligen Host noch anbietbaren, sichtbaren Code-Block-Keywords (siehe CodeBlockFacts) — die
    // gemeinsame Autorität mit der Parser-Recovery, hier alphabetisch für die Anzeige sortiert. Der Host
    // entscheidet, WELCHE Deklarationen die Grammatik dort erlaubt: Datei-Kopf nur `using`/`namespaceprefix`,
    // ein task-Kopf `code`/`base`/…, ein init-Knoten `abstractmethod`/`params` usw. — statt pauschal aller
    // Code-Keywords. Am Host bereits vorhandene Singletons (alle Deklarationen außer dem wiederholbaren
    // `using`) werden zusätzlich herausgefiltert (context.PresentCodeKeywords).
    static List<NavCompletionItem> CodeBlockKeywordItems(NavCompletionContext context, NavLanguageVersion version) {
        var items = new List<NavCompletionItem>();
        foreach (var keyword in CodeBlockFacts.AvailableDeclarationKeywords(context.Host, context.PresentCodeKeywords)
                                              .Where(keyword => IsCodeKeywordAvailable(context.Host, keyword, version))
                                              .OrderBy(k => k, StringComparer.Ordinal)) {
            items.Add(KeywordItem(keyword, context.Host));
        }

        return items;
    }

    // Die choice-`[params]`-Klausel ist ein Version-2-Feature (dieselbe Nav5000-Gate-Autorität): `params` wird
    // im choice-Knoten erst ab #version 2 angeboten — sonst böte die Completion einen Vorschlag an, der sofort
    // Nav5000 würfe. Alle übrigen Code-Block-Keywords (auch das versionsunabhängige `params` am init-Knoten und
    // an der Task-Definition) sind versionsneutral.
    static bool IsCodeKeywordAvailable(CodeBlockHost host, string keyword, NavLanguageVersion version) {
        if (host == CodeBlockHost.ChoiceNode && keyword == SyntaxFacts.ParamsKeyword) {
            return NavLanguageFeatures.IsAvailable(NavLanguageFeature.ChoiceParameters, version);
        }

        return true;
    }

    // Hinter einem vollständigen Ziel: die je nach Quellknoten zulässigen Folge-Klauseln (siehe
    // FollowupClauseKeywords) — und ab Sprachversion 2 zusätzlich die Continuation-Kanten o-^/--^
    // (`… --> View o-^ Task`), sofern das Feature unter der effektiven #version verfügbar ist (dieselbe
    // Autorität wie das Nav5000-Gate). Die Continuation-Keywords liegen bewusst hier und NICHT in
    // VisibleEdgeKeywordItems: eine Continuation leitet keine neue Transition ein (sie hängt hinter dem
    // Zielknoten), sie sind daher — wie schon in SyntaxFacts — von den regulären Edge-Keywords getrennt. Sie
    // hängen am Ziel, nicht am Quellknoten, und bleiben daher vom SourceKind-Pruning unberührt.
    static IReadOnlyList<NavCompletionItem> AfterTargetItems(NavCompletionContext context, NavLanguageVersion version) {

        var keywords = FollowupClauseKeywords(context.SourceKind);

        if (NavLanguageFeatures.IsAvailable(NavLanguageFeature.Continuation, version)) {
            keywords.AddRange(SyntaxFacts.ContinuationEdgeKeywords);
        }

        return KeywordItems(keywords.ToArray());
    }

    // Hinter einem bereits gesetzten Trigger (`on Signal` / `spontaneous`): grammatisch folgt nur noch eine
    // Bedingung und/oder `do`. Bei einer GUI-Quelle IST die Transition eine Trigger-Transition → Bedingungen
    // sind dort unzulässig (Nav0220), es bleibt nur `do`; bei jeder anderen Quelle (init mit spontaneous)
    // bleiben if/else/do. `on` entfällt hier immer — ein zweiter Trigger ist nie zulässig.
    static IReadOnlyList<NavCompletionItem> AfterTriggerItems(NavCompletionContext context) {
        return context.SourceKind == TransitionSourceKind.Gui
                   ? KeywordItems(SyntaxFacts.DoKeyword)
                   : KeywordItems(SyntaxFacts.IfKeyword, SyntaxFacts.ElseKeyword, SyntaxFacts.DoKeyword);
    }

    // Die hinter einem Ziel zulässigen Folge-Klauseln, abgeleitet aus dem Quellknoten der Transition — so
    // bietet die Completion keine Klausel an, die sofort einen Analyzer-Fehler auslöste:
    //   • GUI-Quelle (view/dialog) → Trigger-Transition: `on` zulässig, `if`/`else` nicht (Nav0220).
    //   • init-Quelle → Signal-Trigger `on` unzulässig (Nav0200); Bedingungen zulässig.
    //   • choice-Quelle → jeder Trigger unzulässig (Nav0203); Bedingungen zulässig.
    //   • Exit-Transition → kein Trigger (Grammatik), nur `if` (Nav0221).
    //   • Quelle unbekannt → konservativ die volle Menge (nie weniger anbieten als nötig).
    // `do` (die Aktions-Klausel) ist überall zulässig und wird stets angehängt.
    static List<string> FollowupClauseKeywords(TransitionSourceKind sourceKind) {

        var keywords = new List<string>();

        switch (sourceKind) {
            case TransitionSourceKind.Gui:
                keywords.Add(SyntaxFacts.OnKeyword);
                break;
            case TransitionSourceKind.Init:
            case TransitionSourceKind.Choice:
                keywords.Add(SyntaxFacts.IfKeyword);
                keywords.Add(SyntaxFacts.ElseKeyword);
                break;
            case TransitionSourceKind.Exit:
                keywords.Add(SyntaxFacts.IfKeyword);
                break;
            default:
                keywords.Add(SyntaxFacts.OnKeyword);
                keywords.Add(SyntaxFacts.IfKeyword);
                keywords.Add(SyntaxFacts.ElseKeyword);
                break;
        }

        keywords.Add(SyntaxFacts.DoKeyword);

        return keywords;
    }

    // Die sichtbaren Edge-Keywords (`-->`, `o->`, …). Den Ersetzungsbereich (bereits getippte Edge-Zeichen)
    // hängt WithOperatorReplacements zentral an — hier werden nur die reinen Keyword-Items erzeugt.
    static List<NavCompletionItem> VisibleEdgeKeywordItems() {
        var items = new List<NavCompletionItem>();
        foreach (var keyword in SyntaxFacts.EdgeKeywords
                                .Where(k => !SyntaxFacts.IsHiddenKeyword(k))
                                .OrderBy(k => k, StringComparer.Ordinal)) {
            items.Add(KeywordItem(keyword));
        }

        return items;
    }

    // Der Ersetzungsbereich eines edge-artigen Operators (reguläre Edge ODER Continuation-Kante) um die
    // Cursor-Position — er umfasst zwei Anteile:
    //
    //  • Rückwärts: der Lauf über die bereits getippten Edge-Zeichen bis zum Zeilenanfang (Port des
    //    VS-`GetStartOfEdge`) — deckt die gerade von links getippte (Teil-)Edge ab, damit ihr Commit die
    //    Zeichen ersetzt statt zu verdoppeln (`i o|` → `o->` statt `oo->`; `V -|` → `--^` statt `---^`).
    //
    //  • Vorwärts: NUR ein bereits VOLLSTÄNDIGES Edge-/Continuation-Keyword (Lexer-Token), das der Cursor
    //    berührt. Das behebt den Fall, dass der Cursor VOR einer vorhandenen Kante steht (`i |--> Sub`): ohne
    //    diesen Anteil fügte der Commit eine zweite Kante ein (`i -->--> Sub`); mit ihm wird die vorhandene
    //    ersetzt. Bewusst KEIN roher Zeichen-Vorlauf wie beim Rückwärtslauf: `o`/`*`/`=`/`-` können auch einen
    //    Zielknoten beginnen (`-->order`), ein Zeichen-Vorlauf fräse ins Ziel. Die Token-Grenze des Lexers
    //    (`-->` ist ein Token, `order` das nächste) ist hier die einzige verlässliche Autorität.
    //
    // Berührt der Cursor keine Kante, ist der Bereich leer (Start == End == position) → reines Einfügen.
    static TextExtent OperatorReplacementExtent(SyntaxTree tree, int position) {
        var source = tree.SourceText;
        var line   = source.GetTextLineAtPosition(position);

        var start = position;
        while (start > line.Start && SyntaxFacts.IsEdgeCharacter(source[start - 1])) {
            start--;
        }

        var end   = position;
        var token = tree.Tokens.FindAtPosition(position);
        if (SyntaxFacts.IsEdgeKeyword(token.Type) || SyntaxFacts.IsContinuationEdgeKeyword(token.Type)) {
            end = token.End;
        }

        return TextExtent.FromBounds(start, end);
    }

    // Die gültigen Sprach-Versionsnummern (heute nur `1`) — Label ist der numerische Wert. Single Source of
    // Truth ist NavLanguageVersion.SupportedVersions, dieselbe Tabelle, die Nav5001 validiert.
    static IReadOnlyList<NavCompletionItem> VersionValueItems() {
        return NavLanguageVersion.SupportedVersions
                                 .OrderBy(v => v.Value)
                                 .Select(v => new NavCompletionItem(v.ToString(), NavCompletionItemKind.Keyword))
                                 .ToList();
    }

    static IReadOnlyList<NavCompletionItem> KeywordItems(params string[] keywords) {
        return keywords.OrderBy(k => k, StringComparer.Ordinal)
                       .Select(k => KeywordItem(k))
                       .ToList();
    }

    // Ein Keyword-Vorschlag samt seiner Bedeutung (die einzige Autorität ist SyntaxFacts; auch die
    // Edge-Operatoren sind dort hinterlegt). Der Code-Block-Host — sofern bekannt — wählt die kontextgenaue
    // Bedeutung host-abhängiger Keywords (`params`/`result`); ohne Host gilt die host-neutrale Fassung. Eine
    // fehlende Beschreibung wird zu null normalisiert, damit der Host das Doku-Panel gar nicht erst befüllt.
    static NavCompletionItem KeywordItem(string keyword, CodeBlockHost? host = null) {
        var description = host is { } h
                              ? SyntaxFacts.GetKeywordDescription(keyword, h)
                              : SyntaxFacts.GetKeywordDescription(keyword);
        return new NavCompletionItem(keyword, NavCompletionItemKind.Keyword,
                                     description: description.Length == 0 ? null : description);
    }

    // Erst alle Knoten ohne Referenzen, dann die übrigen — je alphabetisch. Über <paramref name="roleFilter"/>
    // wird auf die im jeweiligen Slot grammatisch mögliche Rolle eingegrenzt (Quelle bzw. Ziel einer Transition);
    // ohne Filter (Fallback) werden alle Knoten angeboten.
    static void AddNodeReferences(List<NavCompletionItem> items, ITaskDefinitionSymbol? task, Func<INodeSymbol, bool>? roleFilter = null) {

        if (task == null) {
            return;
        }

        var nodes = roleFilter == null
                        ? (IReadOnlyList<INodeSymbol>) task.NodeDeclarations
                        : task.NodeDeclarations.Where(roleFilter).ToList();

        foreach (var node in nodes
                             .Where(n => n.References.Count == 0)
                             .OrderBy(n => n.Name, StringComparer.Ordinal)) {
            items.Add(FromSymbol(node));
        }

        foreach (var node in nodes
                             .Where(n => n.References.Count != 0)
                             .OrderBy(n => n.Name, StringComparer.Ordinal)) {
            items.Add(FromSymbol(node));
        }
    }

    #endregion

    static NavCompletionItem FromSymbol(ISymbol symbol) {
        return new NavCompletionItem(symbol.Name, KindOf(symbol), symbol: symbol);
    }

    static NavCompletionItemKind KindOf(ISymbol symbol) => symbol switch {
        IChoiceNodeSymbol                                     => NavCompletionItemKind.Choice,
        IGuiNodeSymbol                                        => NavCompletionItemKind.GuiNode,
        ITaskNodeSymbol or ITaskDeclarationSymbol             => NavCompletionItemKind.Task,
        IInitNodeSymbol or IExitNodeSymbol or IEndNodeSymbol  => NavCompletionItemKind.ConnectionPoint,
        IConnectionPointSymbol                                => NavCompletionItemKind.ConnectionPoint,
        _                                                     => NavCompletionItemKind.Node
    };

    #region Pfad-Vervollständigung (Dateiname-basiert über die Solution)

    /// <summary>
    /// Vervollständigt Dateipfade innerhalb von <c>taskref "…"</c>. Liefert <c>null</c>, wenn die Position
    /// NICHT in einem solchen String-Kontext liegt (dann übernimmt die Nav-/Edge-Vervollständigung), sonst
    /// die (ggf. leere) Liste aller von der Solution bekannten Nav-Files. Es wird NICHT das Dateisystem
    /// durchsucht — die Solution kennt bereits alle <c>*.nav</c> unterhalb des Workspace-Roots. Gefiltert
    /// wird clientseitig über den <em>Dateinamen</em> (so findet „Messageb" auch ein tief verschachteltes
    /// „MessageBoxes.nav"); eingefügt wird der zum aktuellen Nav-File <em>relative</em> Pfad.
    /// </summary>
    static IReadOnlyList<NavCompletionItem>? GetPathCompletions(SourceText source, int position, NavSolution? solution) {

        if (position < 0 || position > source.Length) {
            return null;
        }

        var line         = source.GetTextLineAtPosition(position);
        var lineText     = source.Substring(line.ExtentWithoutLineEndings);
        var linePosition = position - line.Start;

        if (!lineText.IsInQuotation(linePosition)) {
            return null;
        }

        var quotedExtent = lineText.QuotedExtent(linePosition);
        if (quotedExtent.IsMissing) {
            return null;
        }

        var previousIdentifier = quotedExtent.Start > 0 ? lineText.GetPreviousIdentifier(quotedExtent.Start - 1) : string.Empty;
        if (previousIdentifier != SyntaxFacts.TaskrefKeyword) {
            return null;
        }

        var items        = new List<NavCompletionItem>();
        var navDirectory = source.FileInfo?.Directory;

        if (solution != null && navDirectory != null) {

            // Der Ersetzungsbereich ist der gesamte Inhalt zwischen den Anführungszeichen (absolute Offsets).
            var replacement   = TextExtent.FromBounds(line.Start + quotedExtent.Start, line.Start + quotedExtent.End);
            var directoryName = navDirectory.FullName + Path.DirectorySeparatorChar;
            var currentFile   = source.FileInfo?.FullName;

            foreach (var file in solution.SolutionFiles
                                         .Where(f => !string.Equals(f.FullName, currentFile, StringComparison.OrdinalIgnoreCase))
                                         .OrderBy(f => f.Name,     StringComparer.OrdinalIgnoreCase)
                                         .ThenBy(f  => f.FullName, StringComparer.OrdinalIgnoreCase)) {

                var relativePath = PathHelper.GetRelativePath(fromPath: directoryName, toPath: file.FullName);

                items.Add(new NavCompletionItem(
                              label: file.Name,
                              kind: NavCompletionItemKind.File,
                              insertText: relativePath,
                              replacementExtent: replacement,
                              detail: relativePath));
            }
        }

        return items;
    }

    #endregion

}
