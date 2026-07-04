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
/// hinter einer Edge die Zielknoten (plus <c>end</c>); hinter <c>knoten:</c> die Exit-Connection-Points; hinter
/// einem Ziel die Folge-Klauseln <c>on</c>/<c>if</c>/<c>else</c>/<c>do</c>; im Schlüsselwort-Slot eines Code-Blocks
/// (direkt hinter <c>[</c>) die Code-Block-Keywords. Keine Vorschläge in Kommentaren, Zeichenketten
/// (<c>"…"</c>), im C#-Inhalt eines Code-Blocks oder im Wert-Slot hinter <c>do</c> (freier C#-Aufruf); innerhalb
/// von <c>taskref "…"</c> die Pfad-Vervollständigung.
/// </summary>
public static class NavCompletionService {

    /// <summary>
    /// Die kanonische Menge der Auslöser-Zeichen (Trigger-Chars) der Completion — die Vereinigung aller
    /// Situationen, in denen ein einzelnes Sonderzeichen (also KEIN Bezeichner-Zeichen) automatisch eine
    /// Vervollständigung eröffnen soll. Einzige Autorität für beide Hosts: der LSP-Server speist damit
    /// <c>CompletionOptions.TriggerCharacters</c>, die VS-Quellen leiten daraus ihr Auslöse-Verhalten ab.
    /// Buchstaben lösen zusätzlich immer aus (Client- bzw. <c>char.IsLetter</c>-seitig) und sind hier daher
    /// bewusst NICHT enthalten. Die Pfadtrenner <c>/</c> und <c>\</c> sind bewusst KEINE Auslöser: das
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

    /// <summary>Ob <paramref name="c"/> ein kanonisches Auslöser-Zeichen der Completion ist (siehe <see cref="TriggerCharacters"/>).</summary>
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
        var pathItems = GetPathCompletions(source, position, solution);
        if (pathItems != null) {
            return pathItems;
        }

        var context = NavCompletionContext.Classify(unit, position);

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

            // Im Schlüsselwort-Slot eines Code-Blocks (`[ … ]`): die im jeweiligen Wirt zulässigen Code-Keywords.
            case NavCompletionContextKind.CodeBlock:
                return CodeBlockKeywordItems(context);

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
                return VisibleEdgeKeywordItems(EdgeReplacementExtent(unit.Syntax.SyntaxTree, position));

            case NavCompletionContextKind.TargetSlot:
                return TargetItems(context);

            case NavCompletionContextKind.StatementStart:
                return StatementStartItems(context);

            case NavCompletionContextKind.TransitionStart:
                return TransitionStartItems(context);

            case NavCompletionContextKind.AfterTarget:
                return KeywordItems(SyntaxFacts.OnKeyword, SyntaxFacts.IfKeyword, SyntaxFacts.ElseKeyword, SyntaxFacts.DoKeyword);

            case NavCompletionContextKind.AfterTrigger:
                return KeywordItems(SyntaxFacts.IfKeyword, SyntaxFacts.ElseKeyword, SyntaxFacts.DoKeyword);

            case NavCompletionContextKind.AfterCondition:
                return KeywordItems(SyntaxFacts.DoKeyword);

            // Im „Schwanz" einer init-Knoten-Deklaration folgt grammatisch nur noch die optionale `do`-Klausel.
            case NavCompletionContextKind.InitNodeTail:
                return KeywordItems(SyntaxFacts.DoKeyword);

            default:
                return FallbackItems(context, unit.Syntax.SyntaxTree, position);
        }
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
    static List<NavCompletionItem> TargetItems(NavCompletionContext context) {
        var items = new List<NavCompletionItem>();
        AddNodeReferences(items, context.Task, n => n is ITargetNodeSymbol and not IEndNodeSymbol);
        items.Add(new NavCompletionItem(SyntaxFacts.EndKeyword, NavCompletionItemKind.Keyword));
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
            items.Add(new NavCompletionItem(keyword, NavCompletionItemKind.Keyword));
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
            items.Add(new NavCompletionItem(keyword, NavCompletionItemKind.Keyword));
        }

        return items;
    }

    // Konservatives Alt-Verhalten für nicht eindeutig klassifizierbare Stellen: vorhandene Knoten +
    // sichtbare Nav-Keywords (ohne Edge-Keywords) + sichtbare Edge-Keywords. So wird nie weniger angeboten.
    static List<NavCompletionItem> FallbackItems(NavCompletionContext context, SyntaxTree tree, int position) {
        var items = new List<NavCompletionItem>();
        AddNodeReferences(items, context.Task);

        foreach (var keyword in SyntaxFacts.NavKeywords
                                .Where(k => !SyntaxFacts.IsHiddenKeyword(k) && !SyntaxFacts.IsEdgeKeyword(k))
                                .OrderBy(k => k, StringComparer.Ordinal)) {
            items.Add(new NavCompletionItem(keyword, NavCompletionItemKind.Keyword));
        }

        items.AddRange(VisibleEdgeKeywordItems(EdgeReplacementExtent(tree, position)));
        return items;
    }

    // Die im jeweiligen Wirt noch anbietbaren, sichtbaren Code-Block-Keywords (siehe CodeBlockFacts) — die
    // gemeinsame Autorität mit der Parser-Recovery, hier alphabetisch für die Anzeige sortiert. Der Wirt
    // entscheidet, WELCHE Deklarationen die Grammatik dort erlaubt: Datei-Kopf nur `using`/`namespaceprefix`,
    // ein task-Kopf `code`/`base`/…, ein init-Knoten `abstractmethod`/`params` usw. — statt pauschal aller
    // Code-Keywords. Am Wirt bereits vorhandene Singletons (alle Deklarationen außer dem wiederholbaren
    // `using`) werden zusätzlich herausgefiltert (context.PresentCodeKeywords).
    static List<NavCompletionItem> CodeBlockKeywordItems(NavCompletionContext context) {
        var items = new List<NavCompletionItem>();
        foreach (var keyword in CodeBlockFacts.AvailableDeclarationKeywords(context.Host, context.PresentCodeKeywords)
                                              .OrderBy(k => k, StringComparer.Ordinal)) {
            items.Add(new NavCompletionItem(keyword, NavCompletionItemKind.Keyword));
        }

        return items;
    }

    // Die sichtbaren Edge-Keywords (`-->`, `o->`, …). Jedes Item trägt denselben Ersetzungsbereich
    // (<paramref name="replacement"/>) — die bereits getippten Edge-Zeichen —, damit der Host beim Commit
    // die angefangene Edge komplett ersetzt (Edge-Keywords bestehen aus Nicht-Bezeichner-Zeichen, die der
    // Standard-Wortersatz des Clients nicht abdeckt).
    static List<NavCompletionItem> VisibleEdgeKeywordItems(TextExtent replacement) {
        var items = new List<NavCompletionItem>();
        foreach (var keyword in SyntaxFacts.EdgeKeywords
                                .Where(k => !SyntaxFacts.IsHiddenKeyword(k))
                                .OrderBy(k => k, StringComparer.Ordinal)) {
            items.Add(new NavCompletionItem(keyword, NavCompletionItemKind.Keyword, replacementExtent: replacement));
        }

        return items;
    }

    // Der Ersetzungsbereich einer Edge um die Cursor-Position — er umfasst zwei Anteile:
    //
    //  • Rückwärts: der Lauf über die bereits getippten Edge-Zeichen bis zum Zeilenanfang (Port des
    //    VS-`GetStartOfEdge`) — deckt die gerade von links getippte (Teil-)Edge ab, damit ihr Commit die
    //    Zeichen ersetzt statt zu verdoppeln (`i o|` → `o->` statt `oo->`).
    //
    //  • Vorwärts: NUR eine bereits VOLLSTÄNDIGE Edge (Lexer-Token, `IsEdgeKeyword`), die der Cursor berührt.
    //    Das behebt den Fall, dass der Cursor VOR einer vorhandenen Edge steht (`i |--> Sub`): ohne diesen
    //    Anteil fügte der Commit eine zweite Edge ein (`i -->--> Sub`); mit ihm wird die vorhandene ersetzt.
    //    Bewusst KEIN roher Zeichen-Vorlauf wie beim Rückwärtslauf: `o`/`*`/`=`/`-` können auch einen
    //    Zielknoten beginnen (`-->order`), ein Zeichen-Vorlauf fräse ins Ziel. Die Token-Grenze des Lexers
    //    (`-->` ist ein Token, `order` das nächste) ist hier die einzige verlässliche Autorität.
    //
    // Berührt der Cursor keine Edge, ist der Bereich leer (Start == End == position) → reines Einfügen.
    static TextExtent EdgeReplacementExtent(SyntaxTree tree, int position) {
        var source = tree.SourceText;
        var line   = source.GetTextLineAtPosition(position);

        var start = position;
        while (start > line.Start && SyntaxFacts.IsEdgeCharacter(source[start - 1])) {
            start--;
        }

        var end   = position;
        var token = tree.Tokens.FindAtPosition(position);
        if (SyntaxFacts.IsEdgeKeyword(token.Type)) {
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
                       .Select(k => new NavCompletionItem(k, NavCompletionItemKind.Keyword))
                       .ToList();
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
