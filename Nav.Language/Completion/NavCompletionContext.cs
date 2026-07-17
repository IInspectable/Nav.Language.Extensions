#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Completion;

/// <summary>
/// Die grammatische Situation an der Cursor-Position — abgeleitet aus dem (recovery-festen) Syntaxbaum statt
/// aus einem zeilenbegrenzten Text-Rückwärtsscan. Entscheidet, <em>welche</em> Kategorie von Vorschlägen an
/// dieser Stelle überhaupt sinnvoll ist.
/// </summary>
enum NavCompletionContextKind {

    /// <summary>
    /// Keine Vorschläge (Kommentar, Zeichenkette, C#-Inhalt eines Code-Blocks, Wert-Slot hinter <c>do</c>
    /// oder außerhalb des Texts).
    /// </summary>
    Suppress,

    /// <summary>
    /// Im Schlüsselwort-Slot einer Code-Deklaration direkt hinter <c>[</c> (z.B. <c>[using …]</c>,
    /// <c>[result …]</c>): die Code-Block-Schlüsselwörter. <em>Welche</em> zulässig sind, hängt vom Host des
    /// Blocks ab (<see cref="NavCompletionContext.Host"/>).
    /// </summary>
    CodeBlock,

    /// <summary>
    /// Direkt hinter dem <c>#</c> einer Präprozessor-Direktive (Schlüsselwort-Slot): das
    /// Direktiv-Schlüsselwort — derzeit ausschließlich <c>version</c>.
    /// </summary>
    DirectiveKeyword,

    /// <summary>
    /// Hinter <c>#version </c> im Wert-Slot: die gültigen Sprach-Versionsnummern
    /// (<see cref="NavLanguageVersion.SupportedVersions"/>).
    /// </summary>
    DirectiveVersionValue,

    /// <summary>Member-Ebene (außerhalb einer Task-Definition): nur <c>task</c> / <c>taskref</c>.</summary>
    MemberLevel,

    /// <summary>
    /// Satzanfang im Body einer <c>taskref</c>-Deklaration (<c>taskref Sub { … }</c>): die einzigen dort
    /// grammatisch zulässigen Deklarationen sind Connection-Point-Deklarationen — <c>init</c> / <c>exit</c> /
    /// <c>end</c>. KEINE Member-Keywords (<c>task</c>/<c>taskref</c>) und keine Knoten-/Transitions-Konstrukte.
    /// </summary>
    ConnectionPointDeclaration,

    /// <summary>Hinter <c>task</c> innerhalb eines Task-Bodies (Task-Knoten): die deklarierten Tasks.</summary>
    TaskNodeName,

    /// <summary>
    /// Satzanfang im <b>Knoten-Deklarations-Block</b> des Task-Bodys (direkt hinter <c>{</c> oder dem <c>;</c>
    /// einer Knoten-Deklaration): die Knoten-Deklarations-Keywords sowie die vorhandenen quellfähigen Knoten
    /// (als Beginn der ersten Transition). Der Deklarations-Block ist hier noch offen — beides ist möglich.
    /// </summary>
    StatementStart,

    /// <summary>
    /// Satzanfang im <b>Transitions-Block</b> (hinter dem <c>;</c> einer (Exit-)Transition): der
    /// Deklarations-Block ist abgeschlossen, nur noch eine weitere Transition kann folgen — die quellfähigen
    /// Knoten plus das <c>init</c>-Schlüsselwort (Init-Transition). KEINE Knoten-Deklarations-Keywords.
    /// </summary>
    TransitionStart,

    /// <summary>Hinter einem Quellknoten: die (sichtbaren) Edge-Keywords.</summary>
    EdgeSlot,

    /// <summary>Hinter einer Edge: die Knoten (als Ziel) sowie das Ziel-Keyword <c>end</c>.</summary>
    TargetSlot,

    /// <summary>
    /// Hinter einer Continuation-Kante (<c>o-^</c>/<c>--^</c>): das Continuation-Ziel — ausschließlich die
    /// Task-Knoten. Anders als beim regulären <see cref="TargetSlot"/> muss das Ziel ein Task sein (Analyzer
    /// Nav0121), daher weder die übrigen Zielknoten noch das <c>end</c>-Keyword.
    /// </summary>
    ContinuationTargetSlot,

    /// <summary>Hinter <c>Knoten:</c> einer Exit-Transition: die Exit-Connection-Points des Knotens.</summary>
    ExitConnectionPoint,

    /// <summary>Hinter einem vollständigen Ziel: die Folge-Klauseln <c>on</c> / <c>if</c> / <c>else</c> / <c>do</c>.</summary>
    AfterTarget,

    /// <summary>
    /// Hinter einem vollständigen Continuation-Ziel: die Folge-Klauseln <c>on</c> / <c>if</c> / <c>else</c> /
    /// <c>do</c> — im Unterschied zu <see cref="AfterTarget"/> OHNE die Continuation-Kanten <c>o-^</c>/<c>--^</c>,
    /// da eine Continuation nicht verkettbar ist.
    /// </summary>
    AfterContinuationTarget,

    /// <summary>Innerhalb/hinter einem Trigger (<c>on …</c>): <c>if</c> / <c>else</c> / <c>do</c>.</summary>
    AfterTrigger,

    /// <summary>Innerhalb/hinter einer Bedingung (<c>if …</c>): <c>do</c>.</summary>
    AfterCondition,

    /// <summary>
    /// Im Tail einer <c>init</c>-Knoten-Deklaration (hinter Schlüsselwort/Name bzw. den Code-Blöcken, vor
    /// dem <c>;</c>): das einzige dort grammatisch noch mögliche Schlüsselwort <c>do</c> (die optionale
    /// <c>do</c>-Klausel). Bei allen übrigen schlüsselwort-eingeleiteten Knoten-Deklarationen folgt an dieser
    /// Stelle nur noch das <c>;</c> → dort <see cref="Suppress"/>.
    /// </summary>
    InitNodeTail,

    /// <summary>
    /// Nicht eindeutig klassifizierbar — der Aufrufer fällt auf das konservative Alt-Verhalten zurück
    /// (vorhandene Knoten + sichtbare Keywords), damit nie weniger als bisher angeboten wird.
    /// </summary>
    Fallback

}

/// <summary>
/// Die Art des Quellknotens einer Transition — bestimmt, welche Folge-Klauseln (Trigger <c>on</c>,
/// Bedingungen <c>if</c>/<c>else</c>) hinter Ziel bzw. Trigger überhaupt zulässig sind. Spiegelt die
/// Analyzer Nav0200 (kein Signal-Trigger nach init), Nav0203 (kein Trigger nach choice), Nav0220 (keine
/// Bedingungen in Trigger-Transitionen) und Nav0221 (nur <c>if</c> in Exit-Transitionen), damit die
/// Completion keine Klausel anbietet, die sofort ein Diagnostic auslöste.
/// </summary>
enum TransitionSourceKind {

    /// <summary>Quelle nicht auflösbar → der Aufrufer bietet konservativ die volle Klauselmenge.</summary>
    Unknown,

    /// <summary>init-Knoten: kein Signal-Trigger <c>on</c> (Nav0200); Bedingungen zulässig.</summary>
    Init,

    /// <summary>choice-Knoten: kein Trigger (Nav0203); Bedingungen zulässig.</summary>
    Choice,

    /// <summary>view/dialog (GUI): Trigger <c>on</c> zulässig, keine Bedingungen (Nav0220).</summary>
    Gui,

    /// <summary>Exit-Transition (<c>Knoten:exit</c>): kein Trigger (Grammatik), nur <c>if</c> (Nav0221).</summary>
    Exit

}

/// <summary>
/// Bestimmt die <see cref="NavCompletionContextKind"/> an einer Cursor-Position. Trägt zusätzlich die
/// für die jeweilige Kategorie nötigen Bezugspunkte: die umschließende Task-Definition, — bei
/// <see cref="NavCompletionContextKind.ExitConnectionPoint"/> — den Namen des Exit-Knotens, — bei
/// <see cref="NavCompletionContextKind.CodeBlock"/> — den <see cref="Host"/> des Code-Blocks, und — bei den
/// Folge-Klausel-Situationen (<see cref="NavCompletionContextKind.AfterTarget"/>/<see cref="NavCompletionContextKind.AfterTrigger"/>)
/// — die <see cref="SourceKind"/> der Transition.
/// </summary>
sealed class NavCompletionContext {

    NavCompletionContext(NavCompletionContextKind kind, ITaskDefinitionSymbol? task, string? exitNodeName,
                         CodeBlockHost host, ISet<string>? presentCodeKeywords, TransitionSourceKind sourceKind) {
        Kind               = kind;
        Task               = task;
        ExitNodeName       = exitNodeName;
        Host               = host;
        PresentCodeKeywords = presentCodeKeywords ?? EmptyKeywords;
        SourceKind         = sourceKind;
    }

    static readonly ISet<string> EmptyKeywords = new HashSet<string>(StringComparer.Ordinal);

    public NavCompletionContextKind Kind { get; }

    public ITaskDefinitionSymbol? Task { get; }

    public string? ExitNodeName { get; }

    /// <summary>Der Host eines Code-Blocks — nur für <see cref="NavCompletionContextKind.CodeBlock"/> aussagekräftig.</summary>
    public CodeBlockHost Host { get; }

    /// <summary>
    /// Die am Host bereits vorhandenen Code-Deklarations-Schlüsselwörter (den gerade bearbeiteten Block
    /// ausgenommen) — nur für <see cref="NavCompletionContextKind.CodeBlock"/> gefüllt. Damit filtert die
    /// Completion bereits deklarierte Singletons heraus (siehe <see cref="CodeBlockFacts.AvailableDeclarationKeywords"/>).
    /// </summary>
    public ISet<string> PresentCodeKeywords { get; }

    /// <summary>
    /// Die Art des Quellknotens der Transition — nur für die Folge-Klausel-Situationen
    /// (<see cref="NavCompletionContextKind.AfterTarget"/>/<see cref="NavCompletionContextKind.AfterTrigger"/>)
    /// aussagekräftig; sonst <see cref="TransitionSourceKind.Unknown"/>.
    /// </summary>
    public TransitionSourceKind SourceKind { get; }

    static NavCompletionContext Of(NavCompletionContextKind kind, ITaskDefinitionSymbol? task = null,
                                   string? exitNodeName = null, CodeBlockHost host = CodeBlockHost.CompilationUnit,
                                   ISet<string>? presentCodeKeywords = null,
                                   TransitionSourceKind sourceKind = TransitionSourceKind.Unknown)
        => new(kind, task, exitNodeName, host, presentCodeKeywords, sourceKind);

    public static NavCompletionContext Classify(CodeGenerationUnit unit, int position) {

        var tree   = unit.Syntax.SyntaxTree;
        var source = tree.SourceText;

        if (position < 0 || position > source.Length) {
            return Of(NavCompletionContextKind.Suppress);
        }

        // Kommentare über die angehängte Trivia (Roslyn-Modell), nicht über einen Text-Scan.
        if (tree.IsPositionInComment(position)) {
            return Of(NavCompletionContextKind.Suppress);
        }

        // Präprozessor-Direktiven (`#…`) liegen als strukturierte Trivia NEBEN dem flachen Token-Strom; die
        // Token-Binärsuche unten erreicht sie nicht. Daher zuerst prüfen, ob der Cursor in einer Direktive steht.
        if (DirectiveContext(tree, position) is { } directiveKind) {
            return Of(directiveKind);
        }

        // Keine Vorschläge in Zeichenketten ("…"). Bewusst zeilenbasiert: Nav-Zeichenketten sind per Definition
        // einzeilig — der Lexer beendet ein Literal an jedem Zeilenende —, es gibt hier also keine mehrzeilige
        // Lücke; und eine unterminierte Zeichenkette zerfällt in ein Unknown-`"` plus normal gelexte Token und
        // bildet KEIN StringLiteral-Token, das der Baum tragen könnte. Die taskref-Pfad-Vervollständigung läuft
        // separat (vor uns).
        var line         = source.GetTextLineAtPosition(position);
        var lineText     = source.Substring(line.ExtentWithoutLineEndings);
        var linePosition = position - line.Start;

        if (lineText.IsInQuotation(linePosition)) {
            return Of(NavCompletionContextKind.Suppress);
        }

        // Das signifikante Token LINKS der Position bzw. links des gerade getippten Wortes — der eigentliche
        // Kontext-Anker. Trägt über seinen Parent-Knoten die grammatische Rolle (Quelle/Ziel/Edge/Klausel).
        var contextToken = ContextToken(tree, position);

        // Code-Blöcke ([ … ]): direkt hinter `[` (Schlüsselwort-Slot) die Code-Block-Keywords — auch beim frisch
        // getippten `[`/`[]`, dessen `[` als übersprungenes Token noch ohne CodeSyntax-Knoten dasteht, ist der
        // Kontext-Anker das `[` selbst.
        if (contextToken.Type == SyntaxTokenType.OpenBracket) {
            var (host, hostNode) = CodeBlockHostAt(tree, contextToken);
            var present          = CollectPresentCodeKeywords(hostNode ?? unit.Syntax, contextToken);
            return Of(NavCompletionContextKind.CodeBlock, host: host, presentCodeKeywords: present);
        }

        // Im C#-Inhalt eines Code-Blocks nichts. Zwei einander ergänzende Erkennungen, weil ein Code-Block je
        // nach Wohlgeformtheit UNTERSCHIEDLICH im Baum liegt:
        //   • baumbasiert (InCodeBlock): der Kontext-Anker steckt in einem tatsächlich geparsten
        //     CodeSyntax-Knoten (wohlgeformter, an einem Host hängender Block wie `init i [params …]`). Das
        //     trägt auch über MEHRERE ZEILEN — der zeilenbegrenzte Scan sähe das öffnende `[` einer Vorzeile
        //     nicht und streute dort fälschlich Knoten/Keywords ein.
        //   • zeilenbasiert (IsInTextBlock): fängt zusätzlich die NICHT als CodeSyntax geparsten Blöcke ab —
        //     ein malformter oder unvollständiger Block (etwa ein Datei-`[using …]` ohne vorangehendes
        //     `[namespaceprefix …]`), dessen Klammern der Parser in die SkippedTokensTrivia gefaltet hat. Dort
        //     gibt es keinen CodeSyntax-Knoten, den der Baum tragen könnte; diese Recovery-Läufe sind aber
        //     üblicherweise einzeilig, sodass der Zeilen-Scan sie zuverlässig deckt.
        if (InCodeBlock(contextToken, position) ||
            lineText.IsInTextBlock(linePosition, SyntaxFacts.OpenBracket, SyntaxFacts.CloseBracket)) {
            return Of(NavCompletionContextKind.Suppress);
        }

        // Kein tragendes Token (leere Datei, Position hinter dem letzten Member): Member-Ebene.
        if (contextToken.IsMissing || contextToken.Parent == null) {
            return Of(NavCompletionContextKind.MemberLevel);
        }

        // Eine schließende Klammer beendet den Task-Body — dahinter sind wir wieder auf Member-Ebene.
        if (contextToken.Type == SyntaxTokenType.CloseBrace) {
            return Of(NavCompletionContextKind.MemberLevel);
        }

        var task = EnclosingTask(unit, contextToken);

        // Hinter `task`: im Body referenziert das einen deklarierten Task (Task-Knoten); auf Member-Ebene
        // (Task-Definition) tippt der Nutzer dagegen einen neuen Namen → dort nichts anbieten.
        if (contextToken.Type == SyntaxTokenType.TaskKeyword) {
            return contextToken.Parent is TaskNodeDeclarationSyntax
                ? Of(NavCompletionContextKind.TaskNodeName, task)
                : Of(NavCompletionContextKind.Suppress);
        }

        // taskref-Body: er enthält ausschließlich Connection-Point-Deklarationen (init/exit/end). Ein
        // taskref ist eine TaskDeclaration (kein ITaskDefinitionSymbol) und fiele daher unten auf die
        // Member-Ebene zurück — dort böte die Completion fälschlich task/taskref an. Stattdessen: am
        // Satzanfang im Body (direkt hinter `{` oder dem `;` eines Connection-Points) die Connection-Point-
        // Keywords; an jeder anderen Stelle im Body (der Connector-Name ist ein freier Bezeichner) nichts.
        if (EnclosingTaskDeclaration(contextToken) != null) {
            var atStatementStart = contextToken.Type == SyntaxTokenType.OpenBrace ||
                                    (contextToken.Type  == SyntaxTokenType.Semicolon &&
                                     contextToken.Parent is ConnectionPointNodeSyntax);

            return Of(atStatementStart
                          ? NavCompletionContextKind.ConnectionPointDeclaration
                          : NavCompletionContextKind.Suppress);
        }

        // Außerhalb jeder Task-Definition → Member-Ebene.
        if (task == null) {
            return Of(NavCompletionContextKind.MemberLevel);
        }

        // Hinter `Knoten:` einer Exit-Transition → deren Exit-Connection-Points.
        if (contextToken.Type == SyntaxTokenType.Colon &&
            contextToken.Parent is ExitTransitionDefinitionSyntax exitColon) {
            return Of(NavCompletionContextKind.ExitConnectionPoint, task, exitColon.SourceNode.Name);
        }

        // Hinter einer Edge → Ziel-Position.
        if (contextToken.Parent is EdgeSyntax) {
            return Of(NavCompletionContextKind.TargetSlot, task);
        }

        // Hinter einer Continuation-Kante (o-^/--^) → deren Ziel-Position (nur Task, Nav0121).
        // ContinuationEdgeSyntax leitet sich NICHT von EdgeSyntax ab, daher ein eigener Zweig.
        if (contextToken.Parent is ContinuationEdgeSyntax) {
            return Of(NavCompletionContextKind.ContinuationTargetSlot, task);
        }

        // Satzanfang im Body. Der Task-Body ist grammatisch zweigeteilt und geordnet: erst der
        // Knoten-Deklarations-Block, dann der Transitions-Block (siehe NavParser.ParseTaskDefinition).
        // Direkt hinter `{` (Body-Öffnung) stehen wir am Anfang — der Deklarations-Block ist noch offen.
        if (contextToken.Type == SyntaxTokenType.OpenBrace) {
            return Of(NavCompletionContextKind.StatementStart, task);
        }

        // Hinter einem `;` entscheidet die Art der abgeschlossenen Anweisung über die Region: nach einer
        // Knoten-Deklaration ist der Deklarations-Block noch offen (Deklaration ODER erste Transition möglich →
        // StatementStart); nach einer (Exit-)Transition ist er abgeschlossen — es kann nur noch eine weitere
        // Transition folgen (TransitionStart), Deklarations-Keywords ergeben hier keinen Sinn mehr.
        if (contextToken.Type == SyntaxTokenType.Semicolon) {
            return contextToken.Parent is TransitionDefinitionSyntax or ExitTransitionDefinitionSyntax
                       ? Of(NavCompletionContextKind.TransitionStart, task)
                       : Of(NavCompletionContextKind.StatementStart, task);
        }

        // Rolle des Tokens über den INNERSTEN tragenden Knoten seiner Ancestor-Kette. Der Kontext-Anker steckt
        // je nach Konstrukt direkt in diesem Knoten (Quell-/Ziel-Token, Trigger-/Klausel-Keyword) ODER — bei
        // gefülltem Wert-Slot — eine Ebene tiefer im Identifier(OrString)-Wert (`on Signal`, `if Bedingung`,
        // `do Aufruf`). Nur den direkten Parent zu betrachten verfehlt den gefüllten Fall (Parent ist dann der
        // Wert, nicht die Klausel) und ließe ihn auf den pauschalen Fallback fallen.
        switch (ClassificationNode(contextToken.Parent)) {

            // Quellknoten (auch der Connector-Name `se` in `Sub:se`) → als Nächstes folgt die Edge.
            case SourceNodeSyntax:
            case ExitTransitionDefinitionSyntax when contextToken.Type == SyntaxTokenType.Identifier:
                return Of(NavCompletionContextKind.EdgeSlot, task);

            // Zielknoten → danach die Folge-Klauseln. Ein Continuation-Ziel (Ziel einer
            // ContinuationTransitionSyntax) bietet dieselben Klauseln, aber KEINE weitere Continuation an.
            // Welche Klauseln bei einem regulären Ziel zulässig sind, hängt am Quellknoten (SourceKind) —
            // die Continuation-Klauseln bleiben davon unberührt und behalten ihr bestehendes Verhalten.
            case TargetNodeSyntax target:
                return target.Parent is ContinuationTransitionSyntax
                           ? Of(NavCompletionContextKind.AfterContinuationTarget, task)
                           : Of(NavCompletionContextKind.AfterTarget, task, sourceKind: SourceKindOf(target, task));

            // Trigger (`on …` / `spontaneous`) — auch mit gefülltem Signal → danach if/else/do bzw. (bei
            // GUI-Quelle) nur do, da Bedingungen in Trigger-Transitionen unzulässig sind (Nav0220).
            case TriggerSyntax trigger:
                return Of(NavCompletionContextKind.AfterTrigger, task, sourceKind: SourceKindOf(trigger, task));

            // Bedingung (`if …` / `else …`) — auch mit gefülltem Wert → danach do.
            case ConditionClauseSyntax:
                return Of(NavCompletionContextKind.AfterCondition, task);

            // Hinter `do` steht der Wert-Slot: ein freier C#-Aufruf (identifierOrString), kein Nav-Konstrukt —
            // auch mit gefülltem Wert. Nichts anbieten, statt über den Fallback pauschal Knoten/Keywords zu streuen.
            case DoClauseSyntax:
                return Of(NavCompletionContextKind.Suppress);
        }

        // Tail einer schlüsselwort-eingeleiteten Knoten-Deklaration (hinter Schlüsselwort und/oder Name,
        // vor dem `;`): grammatisch folgt nur noch das `;` — einzig beim init-Knoten zusätzlich eine optionale
        // `do`-Klausel (die Code-Blöcke über `[` sind bereits weiter oben gesondert behandelt). Statt über den
        // Fallback pauschal Knoten/Keywords/Edges einzustreuen: nichts (bzw. beim init-Knoten ohne bereits
        // vorhandene `do`-Klausel das `do`-Keyword). Ein gefüllter `do`-Wert-Slot wurde eben schon als Suppress
        // erkannt (DoClauseSyntax), landet also nicht hier.
        if (EnclosingNodeDeclaration(contextToken) is { } nodeDeclaration) {
            return nodeDeclaration is InitNodeDeclarationSyntax { DoClause: null }
                       ? Of(NavCompletionContextKind.InitNodeTail, task)
                       : Of(NavCompletionContextKind.Suppress);
        }

        return Of(NavCompletionContextKind.Fallback, task);
    }

    /// <summary>
    /// Der innerste Knoten der Ancestor-Kette (inkl. <paramref name="node"/> selbst), der eine für die
    /// Completion tragende grammatische Rolle hat — Quell-/Ziel-Knoten, Exit-Transition, Trigger, Bedingung
    /// oder <c>do</c>-Klausel. Anders als ein Blick nur auf den direkten Parent erfasst das auch den Fall, dass
    /// der Kontext-Anker im <em>Wert-Slot</em> einer Klausel steckt (<c>on Signal</c>, <c>if Bedingung</c>,
    /// <c>do Aufruf</c>): dessen direkter Parent ist der Identifier(OrString)-Wert, die tragende Rolle erst
    /// dessen Elternklausel. „Innerster zuerst" ist entscheidend — in einer Exit-Transition liegen Ziel/Trigger/
    /// Bedingung/do INNERHALB der <see cref="ExitTransitionDefinitionSyntax"/>; deren spezifischere Rolle muss
    /// gewinnen, die Exit-Transition selbst bleibt nur der Anker für ihren Connector-Namen.
    /// </summary>
    static SyntaxNode? ClassificationNode(SyntaxNode? node) {

        if (node == null) {
            return null;
        }

        foreach (var ancestor in node.AncestorsAndSelf()) {
            switch (ancestor) {
                case SourceNodeSyntax:
                case TargetNodeSyntax:
                case TriggerSyntax:
                case ConditionClauseSyntax:
                case DoClauseSyntax:
                case ExitTransitionDefinitionSyntax:
                    return ancestor;
            }
        }

        return null;
    }

    /// <summary>
    /// Die <see cref="TransitionSourceKind"/> der Transition, in der <paramref name="node"/> steht — Grundlage
    /// dafür, welche Folge-Klauseln hinter Ziel bzw. Trigger zulässig sind. Eine Exit-Transition
    /// (<c>Knoten:exit --> …</c>) wird direkt am Syntaxknoten erkannt; bei einer regulären Transition
    /// entscheidet der <em>aufgelöste</em> Quellknoten (init/choice/gui). Lässt sich die Quelle nicht auflösen,
    /// bleibt es <see cref="TransitionSourceKind.Unknown"/> — der Aufrufer bietet dann konservativ die volle Menge.
    /// </summary>
    static TransitionSourceKind SourceKindOf(SyntaxNode node, ITaskDefinitionSymbol? task) {

        // Exit-Transition: die Quelle ist immer ein Task-Exit; grammatisch kein Trigger, nur `if` (Nav0221).
        if (node.AncestorsAndSelf().OfType<ExitTransitionDefinitionSyntax>().Any()) {
            return TransitionSourceKind.Exit;
        }

        var transition = node.AncestorsAndSelf().OfType<TransitionDefinitionSyntax>().FirstOrDefault();
        if (transition == null || task == null) {
            return TransitionSourceKind.Unknown;
        }

        var name       = transition.SourceNode.Name;
        var sourceNode = task.TryFindNode(name);

        // Sonderfall init: als Quelle ist — wie im SemanticModel-Builder — auch die Großschreibung `Init` erlaubt.
        if (sourceNode == null && name == SyntaxFacts.InitKeyword) {
            sourceNode = task.TryFindNode(SyntaxFacts.InitKeywordAlt);
        }

        return sourceNode switch {
            IInitNodeSymbol   => TransitionSourceKind.Init,
            IChoiceNodeSymbol => TransitionSourceKind.Choice,
            IGuiNodeSymbol    => TransitionSourceKind.Gui,
            _                 => TransitionSourceKind.Unknown
        };
    }

    /// <summary>
    /// Die umschließende Task-Definition (Symbol) zum gegebenen Token — oder <c>null</c> auf Member-Ebene.
    /// </summary>
    static ITaskDefinitionSymbol? EnclosingTask(CodeGenerationUnit unit, SyntaxToken token) {

        var taskSyntax = token.Parent?
                              .AncestorsAndSelf()
                              .OfType<TaskDefinitionSyntax>()
                              .FirstOrDefault();

        if (taskSyntax == null) {
            return null;
        }

        return unit.TaskDefinitions.FirstOrDefault(t => t.Syntax.Extent == taskSyntax.Extent);
    }

    /// <summary>
    /// Die umschließende <c>taskref</c>-Deklaration (<see cref="TaskDeclarationSyntax"/>) zum gegebenen Token —
    /// oder <c>null</c>, wenn das Token nicht innerhalb einer solchen liegt. Anders als eine
    /// <see cref="TaskDefinitionSyntax"/> trägt sie kein <see cref="ITaskDefinitionSymbol"/>; für die Completion
    /// im taskref-Body genügen die statischen Connection-Point-Keywords, ein Symbol wird nicht benötigt.
    /// </summary>
    static TaskDeclarationSyntax? EnclosingTaskDeclaration(SyntaxToken token) {
        return token.Parent?
                    .AncestorsAndSelf()
                    .OfType<TaskDeclarationSyntax>()
                    .FirstOrDefault();
    }

    /// <summary>
    /// Die umschließende Knoten-Deklaration (<see cref="NodeDeclarationSyntax"/>) zum gegebenen Token — oder
    /// <c>null</c>, wenn das Token nicht innerhalb einer solchen liegt. Deckt den Tail hinter Schlüsselwort
    /// und Name ab: dort folgt grammatisch nur noch das <c>;</c> (einzig beim <c>init</c>-Knoten zusätzlich die
    /// optionale <c>do</c>-Klausel). Ein gefüllter <c>do</c>-Wert-Slot wird vorher schon über die Ancestor-Kette
    /// (<see cref="DoClauseSyntax"/>) als <see cref="NavCompletionContextKind.Suppress"/> erkannt.
    /// </summary>
    static NodeDeclarationSyntax? EnclosingNodeDeclaration(SyntaxToken token) {
        return token.Parent?
                    .AncestorsAndSelf()
                    .OfType<NodeDeclarationSyntax>()
                    .FirstOrDefault();
    }

    /// <summary>
    /// Bestimmt den <see cref="CodeBlockHost"/> eines Code-Blocks anhand seines öffnenden <c>[</c>. Ein gerade
    /// getippter, leerer <c>[]</c> wird NICHT als Code-Deklaration geparst (kein Lookahead-Match auf <c>[</c> +
    /// Schlüsselwort); sein <c>[</c> ist ein übersprungenes Token (Skip-Trivia) ohne Host-Knoten — der
    /// verlässliche Anker ist stattdessen das <b>konsumierte</b> Token <b>links</b> des <c>[</c>: das
    /// schließende <c>]</c> der vorigen Code-Deklaration bzw. der Name/das einleitende Schlüsselwort des Hosts.
    /// Über dessen Ancestor-Kette wird der <em>innerste</em> Host-Knoten bestimmt (Knoten-Deklarationen liegen
    /// in der Task-Definition geschachtelt); ohne tragenden Knoten (Datei-Kopf) ist es die Datei-Ebene.
    /// </summary>
    static (CodeBlockHost Host, SyntaxNode? HostNode) CodeBlockHostAt(SyntaxTree tree, SyntaxToken openBracket) {

        var tokens = tree.Tokens;
        var index  = LastIndexStartingBefore(tokens, openBracket.Start);
        var anchor = index >= 0 ? tokens[index].Parent : null;

        if (anchor != null) {
            foreach (var node in anchor.AncestorsAndSelf()) {
                // Knotentyp → Host ist die Autorität von CodeBlockFacts (dieselbe, die die kontextabhängige
                // Keyword-Bedeutung nutzt) — hier nur um den innersten tragenden Knoten angereichert.
                if (CodeBlockFacts.HostKindOf(node) is { } host) {
                    return (host, node);
                }
            }
        }

        // Kein tragender Host-Knoten → Datei-Ebene; der Host ist dann die CodeGenerationUnit (Root), deren
        // Aufrufer über den Fallback beisteuert.
        return (CodeBlockHost.CompilationUnit, null);
    }

    /// <summary>
    /// Die am Host bereits vorhandenen Code-Deklarations-Schlüsselwörter — die <c>Keyword</c>-Token seiner
    /// <em>direkten</em> <see cref="CodeSyntax"/>-Kinder. Bewusst nur direkte Kinder: verschachtelte Knoten
    /// (etwa die <c>[params]</c> eines <c>init</c>-Knotens innerhalb einer Task-Definition) sind eigene Hosts
    /// und dürfen die äußere Ebene nicht verunreinigen. Der gerade bearbeitete Block selbst (<paramref
    /// name="currentOpenBracket"/> als sein öffnendes <c>[</c>) zählt NICHT als „schon vorhanden" — sonst
    /// filterte man das Schlüsselwort weg, das der Nutzer an genau dieser Stelle tippt.
    /// </summary>
    static ISet<string> CollectPresentCodeKeywords(SyntaxNode host, SyntaxToken currentOpenBracket) {

        var present = new HashSet<string>(StringComparer.Ordinal);

        foreach (var code in host.ChildNodes().OfType<CodeSyntax>()) {
            if (code.OpenBracket.Start == currentOpenBracket.Start) {
                continue;
            }

            var keyword = code.Keyword;
            if (!keyword.IsMissing) {
                present.Add(keyword.ToString());
            }
        }

        return present;
    }

    /// <summary>
    /// Ob die <paramref name="position"/> im C#-Inhalt eines tatsächlich geparsten Code-Blocks (<c>[ … ]</c>)
    /// liegt — der Kontext-Anker steckt (über seine Ancestor-Kette) in einem <see cref="CodeSyntax"/>-Knoten und
    /// die Position liegt vor dessen schließender <c>]</c> (bzw. diese fehlt noch, weil der Block unvollständig
    /// ist). Baumbasiert und dadurch über mehrere Zeilen tragfähig — anders als der zeilenbegrenzte Klammer-Scan,
    /// der das öffnende <c>[</c> einer Vorzeile nicht sieht. Fängt aber NUR wohlgeformte, an einem Host hängende
    /// Blöcke: ein malformter Block, dessen Klammern in der SkippedTokensTrivia liegen, bildet keinen
    /// CodeSyntax-Knoten und wird weiterhin über den (dort einzeiligen) Zeilen-Scan gedeckt. Der Schlüsselwort-
    /// Slot direkt hinter <c>[</c> wird VORHER gesondert behandelt (dort ist der Kontext-Anker das <c>[</c>
    /// selbst).
    /// </summary>
    static bool InCodeBlock(SyntaxToken contextToken, int position) {

        if (contextToken.Parent == null) {
            return false;
        }

        var codeSyntax = contextToken.Parent.AncestorsAndSelf().OfType<CodeSyntax>().FirstOrDefault();
        if (codeSyntax == null) {
            return false;
        }

        var close = codeSyntax.CloseBracket;
        return close.IsMissing || position <= close.Start;
    }

    /// <summary>
    /// Klassifiziert eine Position innerhalb einer Präprozessor-Direktive (<c>#…</c>) — oder liefert
    /// <c>null</c>, wenn die Position in keiner Direktive liegt (dann greift die reguläre, Token-basierte
    /// Klassifikation). Direktiven sind strukturierte <see cref="SyntaxTokenType.DirectiveTrivia"/> und nicht
    /// Teil des flachen <see cref="SyntaxTree.Tokens"/>-Stroms; ihre lokalen Token liefert
    /// <see cref="StructuredTriviaSyntax.ChildTokens()"/>. Der Cursor sitzt entweder im Schlüsselwort-Slot direkt
    /// hinter dem <c>#</c> (<see cref="NavCompletionContextKind.DirectiveKeyword"/>) oder — bei einer erkannten
    /// <c>#version</c>-Direktive — im Wert-Slot dahinter (<see cref="NavCompletionContextKind.DirectiveVersionValue"/>).
    /// </summary>
    static NavCompletionContextKind? DirectiveContext(SyntaxTree tree, int position) {

        var directive = DirectiveAt(tree, position);
        if (directive == null) {
            return null;
        }

        var hash = directive.HashToken;

        // Vor bzw. genau am `#` beginnt die Direktive erst — noch keine Direktiv-Situation.
        if (hash.IsMissing || position <= hash.Start) {
            return null;
        }

        // Erkannte Versions-Direktive: bis einschließlich des Endes von `version` wird noch das Schlüsselwort
        // getippt (das Wort ist das Filter-Präfix); erst dahinter beginnt der Wert-Slot.
        if (directive is VersionDirectiveSyntax version && !version.VersionKeyword.IsMissing) {
            return position <= version.VersionKeyword.End
                       ? NavCompletionContextKind.DirectiveKeyword
                       : NavCompletionContextKind.DirectiveVersionValue;
        }

        // Noch kein erkanntes Schlüsselwort (`#`, `#v`, `#pragma`, …): das Schlüsselwort nur anbieten, solange
        // der Cursor im ersten Wort-Slot direkt hinter dem `#` sitzt (kein Wort, oder im/am Ende des Wortes) —
        // weiter hinten in der Zeile gibt es nichts anzubieten.
        var firstWord = directive.ChildTokens()
                                 .Where(t => t.Type is SyntaxTokenType.PreprocessorKeyword
                                                    or SyntaxTokenType.PragmaKeyword
                                                    or SyntaxTokenType.VersionKeyword)
                                 .DefaultIfEmpty(SyntaxToken.Missing)
                                 .First();

        if (firstWord.IsMissing || position <= firstWord.End) {
            return NavCompletionContextKind.DirectiveKeyword;
        }

        return NavCompletionContextKind.Suppress;
    }

    /// <summary>
    /// Die Direktive, deren Inhalts-Extent die <paramref name="position"/> abdeckt — <b>einschließlich</b> der
    /// Endposition, damit der gerade fertig getippte Fall (<c>#version </c> mit Caret am Trivia-Ende) noch
    /// erfasst wird. <see cref="SyntaxTree.FindTrivia"/> nutzt hingegen das Halbintervall <c>[Start, End)</c> und
    /// liefert dort <c>default</c>. Der Extent einer Direktive endet vor ihrem Zeilenende (siehe
    /// <see cref="NavDirectiveParser"/>), daher kollidiert die inklusive Endgrenze nicht mit der Folgezeile.
    /// </summary>
    static DirectiveTriviaSyntax? DirectiveAt(SyntaxTree tree, int position) {
        foreach (var directive in tree.Directives()) {
            if (position >= directive.Start && position <= directive.End) {
                return directive;
            }
        }

        return null;
    }

    /// <summary>
    /// Das signifikante Token, das den Kontext links der Position bestimmt — über den flachen, nach
    /// <see cref="SyntaxToken.Start"/> sortierten Tokenstrom (NICHT <see cref="SyntaxToken.PreviousToken()"/>,
    /// das nur innerhalb desselben Parent-Knotens navigiert), ergänzt um die vom Parser übersprungenen Token
    /// aus der Skip-Trivia (siehe <see cref="TokenLeftOf"/>). Tippt der Nutzer gerade ein Wort
    /// (Identifier/Keyword, in dessen Mitte oder an dessen Ende der Cursor klebt), ist der Kontext dessen
    /// <em>Vorgänger</em> (das Wort selbst ist nur das Filter-Präfix); steht zwischen Token und Cursor ein
    /// Whitespace, ist das Wort abgeschlossen und selbst der Kontext.
    /// </summary>
    static SyntaxToken ContextToken(SyntaxTree tree, int position) {

        var token = TokenLeftOf(tree, position);
        if (token.IsMissing) {
            return SyntaxToken.Missing;
        }

        // Klebt der Cursor in oder am Ende eines gerade getippten Wortes, ist dieses Wort das Präfix — der
        // eigentliche Kontext ist sein Vorgänger.
        if (IsWordToken(token) && position <= token.End) {
            return TokenLeftOf(tree, token.Start);
        }

        // Klebt der Cursor an einer gerade angefangenen Edge (`-`, `--`, `==`, `*` …), sind deren Zeichen nur
        // das Präfix des Edge-Keywords. Solche unvollständigen Edge-Zeichen sind unbekannte Zeichen und liegen
        // in der Skip-Trivia — nicht an einem Task-Knoten; sie tragen daher weder den Task- noch den
        // Edge-Kontext. Wie beim Wort-Präfix ist der eigentliche Kontext ihr Vorgänger, der Quellknoten →
        // EdgeSlot. (Die mehrzeichigen Edges werden zeichenweise als eigene Unknown-Token gelext, daher der
        // Rücklauf über den ganzen zusammenhängenden Lauf.)
        if (IsPartialEdgeToken(token) && position <= token.End) {
            var leftmost = token;
            var previous = TokenLeftOf(tree, token.Start);
            while (IsPartialEdgeToken(previous)) {
                leftmost = previous;
                previous = TokenLeftOf(tree, previous.Start);
            }

            // Das führende `o` von `o->`/`o-^` lext der Lexer als Identifier (ein `o` allein bildet kein
            // Edge-Token) und der Parser hängt es — lückenlos an die Edge-Zeichen geklebt — als Zielknoten in
            // den Baum. Zählt es so zum angefangenen Edge-Lauf, ist es dessen Auftakt und KEIN Ziel: mit
            // überspringen, damit der Kontext der Quellknoten links davon bleibt (→ EdgeSlot) statt fälschlich
            // AfterTarget am vermeintlichen Ziel `o`. Die Lückenlosigkeit (End == Start) grenzt das gegen einen
            // echten, durch Whitespace getrennten Knoten `o` ab.
            if (IsEdgeStarterIdentifier(previous) && previous.End == leftmost.Start) {
                return TokenLeftOf(tree, previous.Start);
            }

            return previous;
        }

        return token;
    }

    /// <summary>
    /// Das letzte Token, das echt links der <paramref name="position"/> beginnt (<c>Start &lt; position</c>) —
    /// aus dem flachen Strom <b>und</b> den vom Parser übersprungenen Token. Letztere stehen als strukturierte
    /// <see cref="SyntaxTokenType.SkippedTokensTrivia"/> neben dem Strom, bestimmen den Kontext links der
    /// Position aber mit: das <c>[</c> eines noch unvollständigen <c>[]</c> oder die Zeichen einer gerade
    /// angefangenen Edge sind übersprungene Token. Von beiden Kandidaten gewinnt der näher an der Position
    /// beginnende; <see cref="SyntaxToken.Missing"/>, wenn links der Position kein Token liegt.
    /// </summary>
    static SyntaxToken TokenLeftOf(SyntaxTree tree, int position) {

        var tokens = tree.Tokens;
        var index  = LastIndexStartingBefore(tokens, position);
        var token  = index >= 0 ? tokens[index] : SyntaxToken.Missing;

        var skipped = LastSkippedTokenStartingBefore(tree, position);
        if (!skipped.IsMissing && (token.IsMissing || skipped.Start > token.Start)) {
            return skipped;
        }

        return token;
    }

    /// <summary>
    /// Das letzte <b>übersprungene</b> Token (aus der Skip-Trivia, siehe <see cref="SyntaxTree.SkippedTokens"/>),
    /// das echt links der <paramref name="position"/> beginnt — oder <see cref="SyntaxToken.Missing"/>.
    /// </summary>
    static SyntaxToken LastSkippedTokenStartingBefore(SyntaxTree tree, int position) {

        var result = SyntaxToken.Missing;

        foreach (var run in tree.SkippedTokens()) {
            if (run.Start >= position) {
                break; // Die Läufe kommen in Quelltext-Reihenfolge — ab hier beginnt keiner mehr links der Position.
            }

            foreach (var token in run.ChildTokens()) {
                if (token.Start >= position) {
                    break;
                }

                result = token;
            }
        }

        return result;
    }

    // Ein Präfix einer noch unvollständigen Edge: ein unbekanntes Token, dessen Text ausschließlich aus
    // Edge-Zeichen besteht (`-`, `>`, `o`, `*`). Vollständige Edge-Keywords (`-->`, `o->`, …) tragen einen
    // eigenen Token-Typ und fallen bewusst heraus — sie bleiben ihr eigener Kontext (→ Ziel-Slot).
    static bool IsPartialEdgeToken(SyntaxToken token) {

        if (token.IsMissing || token.Type != SyntaxTokenType.Unknown) {
            return false;
        }

        var text = token.ToString();
        return text.Length > 0 && text.All(SyntaxFacts.IsEdgeCharacter);
    }

    // Das als Identifier gelexte führende Edge-Zeichen einer angefangenen `o->`/`o-^`-Kante: `o` ist zugleich
    // ein Bezeichner-Zeichen, ein `o` allein bildet daher kein Edge-Token, sondern einen Identifier. Nur als
    // lückenlos geklebter Auftakt eines Edge-Laufs relevant (siehe ContextToken) — die reguläre Kontext-
    // Bestimmung eines freistehenden Knotens `o` bleibt unberührt.
    static bool IsEdgeStarterIdentifier(SyntaxToken token) {

        if (token.IsMissing || token.Type != SyntaxTokenType.Identifier) {
            return false;
        }

        var text = token.ToString();
        return text.Length > 0 && text.All(SyntaxFacts.IsEdgeCharacter);
    }

    // Index des letzten Tokens mit Start &lt; position (Binärsuche über die nach Start sortierte Liste), bzw. -1.
    static int LastIndexStartingBefore(SyntaxTokenList tokens, int position) {

        int lo = 0, hi = tokens.Count - 1, result = -1;

        while (lo <= hi) {
            int mid = lo + (hi - lo) / 2;
            if (tokens[mid].Start < position) {
                result = mid;
                lo     = mid + 1;
            } else {
                hi = mid - 1;
            }
        }

        return result;
    }

    // Ein „Wort" im Sinne der Präfix-Vervollständigung: nicht-leer und ausschließlich aus Identifier-Zeichen
    // (deckt Identifier wie Keywords ab; Edges `-->`/`o->` und Punctuation fallen heraus).
    static bool IsWordToken(SyntaxToken token) {

        if (token.IsMissing) {
            return false;
        }

        var text = token.ToString();
        return text.Length > 0 && text.All(SyntaxFacts.IsIdentifierCharacter);
    }

}
