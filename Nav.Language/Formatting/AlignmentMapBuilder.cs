using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Der Ausrichtungs-Vorpass des Formatters — die einzige nicht-lokale Zutat: eine Spalte hängt von
/// Nachbarzeilen ab. Der Vorpass partitioniert die Anweisungen block-weit in Gruppen, misst je Zeile die
/// <b>kanonische</b> Vor-Spalten-Breite (nie den Ist-Text — der wird vom Formatter selbst normalisiert),
/// löst die Zielspalte über die <see cref="NavFormattingOptions.AlignmentColumnPolicy"/> auf und legt in
/// der <see cref="AlignmentMap"/> nur das Ergebnis ab (Lücke → aufgelöste Space-Zahl). Die
/// Ausrichtungs-Regeln und der Renderer schlagen ausschließlich nach und bleiben pur.
/// </summary>
/// <remarks>
/// <para><b>Gruppenbildung:</b> Trenn-Kriterium ist die Zeilenanzahl strikt zwischen zwei
/// aufeinanderfolgenden Anweisungen (<c>interruptLines</c> = Newline-Trivia der Lücke − 1; Newlines im
/// Inneren mehrzeiliger Kommentare zählen nicht). Neue Gruppe ⟺ <c>interruptLines ≥ 2</c> — eine
/// Leerzeile oder eine eigene Kommentarzeile bricht die Gruppe also <b>nicht</b>. Zusätzlich bricht eine
/// hand-gelegte (mehrzeilige) oder defekte Anweisung die Gruppe; eine Anweisung mit Inline-Block-Kommentar
/// in einer Raster-Lücke wird nur aus der Spalte ausgeschlossen (die Spaltenbreite hinge sonst an der
/// Kommentar-Textlänge), bricht die Gruppe aber nicht. Gruppen mit weniger als zwei Teilnehmern werden
/// nie ausgerichtet (Ausrichtung ist ein Gruppen-Phänomen) — ohne Tabellen-Eintrag rendert die Lücke als
/// Single-Space.</para>
/// <para><b>Idempotenz:</b> Zielspalten sind reine Funktionen kanonischer Token-Breiten (+
/// <see cref="NavFormattingOptions.IndentSize"/>); Leerzeilen werden nie kollabiert, also ist auch
/// <c>interruptLines</c> formatierungs-invariant. Die Task-Kopf-Spalten sind lokale, kanonische
/// Funktionen des Identifier-Textes. Einzige Ausnahme ist
/// <see cref="AlignmentColumnPolicy.PreserveDominant"/>, das bewusst den Ist-Whitespace liest (nach dem
/// ersten Lauf sitzt die Mehrheit exakt auf der Zielspalte → derselbe dominante Wert).</para>
/// </remarks>
static class AlignmentMapBuilder {

    public static AlignmentMap Build(SyntaxTree syntaxTree, NavFormattingOptions options, StatementFacts.Map facts) {

        if (!options.AlignArrows && !options.AlignNodeGrid && !options.AlignTaskHeadBlocks &&
            !options.AlignTriggers && !options.AlignConditions && !options.AlignTrailingComments) {
            return AlignmentMap.Empty;
        }

        var spaces                = new Dictionary<int, int>();
        var trailingCommentSpaces = new Dictionary<int, int>();

        foreach (var task in syntaxTree.Root.DescendantNodes<TaskDefinitionSyntax>()) {

            if (options.AlignTaskHeadBlocks) {
                AddTaskHeadColumns(syntaxTree, task, spaces);
            }

            if (options.AlignNodeGrid) {
                AddNodeGridColumns(syntaxTree, options, facts, task.NodeDeclarationBlock.NodeDeclarations, spaces);
            }

            if (options.AlignArrows) {
                AddArrowColumns(syntaxTree, options, facts, task.TransitionDefinitionBlock, spaces);
            }

            // Nach der Pfeil-Spalte: die Trigger-Spalte baut auf die bereits aufgelösten Pfeil-Paddings
            // auf (sie steckt in derselben Zeile rechts vom Pfeil) — daher zwingend nach AddArrowColumns.
            // Nur TransitionDefinitionSyntax trägt einen Trigger; Exit-Transitionen laufen als
            // Nicht-Teilnehmer mit (brechen die Gruppe nicht), damit die Gruppierung deckungsgleich zur
            // Pfeil-/Condition-Spalte bleibt.
            if (options.AlignTriggers) {
                AddTightClauseColumns(syntaxTree, Transitions(task.TransitionDefinitionBlock),
                                      s => MeasureTransitionClause(syntaxTree, options, facts, s, TriggerOf, spaces), spaces);
            }

            // Nach der Trigger-Spalte: die Condition-Spalte steht in Quellreihenfolge rechts vom Trigger
            // und baut auf dessen (sowie auf das Pfeil-) Padding auf — daher nach der Trigger-Spalte. Eine
            // bedingungslose Transition ist kein Teilnehmer, bricht die Gruppe aber nicht.
            if (options.AlignConditions) {
                AddTightClauseColumns(syntaxTree, Transitions(task.TransitionDefinitionBlock),
                                      s => MeasureTransitionClause(syntaxTree, options, facts, s, ConditionOf, spaces), spaces);
            }

            // Trailing-Kommentare zuletzt: ihre Zeilenbreite baut auf allen bereits aufgelösten Spalten
            // (Pfeil/Condition/Node-Grid) auf. Node-Deklarationen und Transitionen werden getrennt
            // gruppiert — die grammatikalisch erzwungene Leerzeile dazwischen (BlankLineBeforeTransitions)
            // trennt sie ohnehin, aber die getrennten Sequenzen machen die Gruppierung auch dann
            // idempotent, wenn der Autor keine Leerzeile gesetzt hatte.
            if (options.AlignTrailingComments) {
                AddTightClauseColumns(syntaxTree, task.NodeDeclarationBlock.NodeDeclarations,
                                      s => MeasureTrailingComment(syntaxTree, options, facts, s, spaces), trailingCommentSpaces);
                AddTightClauseColumns(syntaxTree, Transitions(task.TransitionDefinitionBlock),
                                      s => MeasureTrailingComment(syntaxTree, options, facts, s, spaces), trailingCommentSpaces);
            }
        }

        foreach (var taskref in syntaxTree.Root.DescendantNodes<TaskDeclarationSyntax>()) {

            if (options.AlignTaskHeadBlocks) {
                AddTaskrefHeadColumns(taskref, spaces);
            }

            if (options.AlignNodeGrid) {
                AddNodeGridColumns(syntaxTree, options, facts, taskref.ConnectionPoints, spaces);
            }

            if (options.AlignTrailingComments) {
                AddTightClauseColumns(syntaxTree, taskref.ConnectionPoints,
                                      s => MeasureTrailingComment(syntaxTree, options, facts, s, spaces), trailingCommentSpaces);
            }
        }

        return new AlignmentMap(spaces, trailingCommentSpaces);
    }

    /// <summary>Transitionen und Exit-Transitionen eines Blocks als einheitliche Anweisungs-Sequenz.</summary>
    static IEnumerable<SyntaxNode> Transitions(TransitionDefinitionBlockSyntax block) {

        foreach (var transition in block.TransitionDefinitions) {
            yield return transition;
        }

        foreach (var exitTransition in block.ExitTransitionDefinitions) {
            yield return exitTransition;
        }
    }

    // ---- Task-Kopf (Blöcke stapeln + mehrzeiliges [params]) -------------------------------------

    /// <summary>
    /// Die kanonischen, pro Task-Definition lokalen Kopf-Spalten: <see cref="ColumnId.TaskHeadBlock"/> =
    /// Spalte des <c>[</c> von Block 1 (<c>"task " + Identifier + " "</c>, Tiefe 0 → reine
    /// Space-Ausrichtung ab Spalte 0) für die Stapel-Lücken <c>] → [</c> und den Kommentar-Grenzfall der
    /// Lücke Identifier → Block 1; <see cref="ColumnId.ParamsList"/> für vom Autor mehrzeilig gelegte
    /// <c>[params …]</c>. Beide sind reine Funktionen kanonischer Token-Breiten und keiner
    /// <see cref="AlignmentColumnPolicy"/> unterworfen.
    /// </summary>
    static void AddTaskHeadColumns(SyntaxTree syntaxTree, TaskDefinitionSyntax task, Dictionary<int, int> spaces) {

        var headColumn = AddHeadBlockColumns(task.TaskKeyword, task.Identifier, HeadBlocks(task), spaces);
        if (headColumn < 0) {
            return;
        }

        AddParamsListColumn(syntaxTree, task.CodeParamsDeclaration, headColumn, spaces);
    }

    /// <summary>
    /// Die Kopf-Spalte einer <c>taskref</c>-Deklaration — symmetrisch zum Task-Kopf: Block 1 inline
    /// hinter dem Identifier, Folgeblöcke (<c>[namespaceprefix …]</c>/<c>[notimplemented]</c>/
    /// <c>[result …]</c>) gestapelt unter dem <c>[</c> des ersten. Ein <c>taskref</c> hat kein
    /// <c>[params …]</c>, daher keine Params-Spalte.
    /// </summary>
    static void AddTaskrefHeadColumns(TaskDeclarationSyntax taskref, Dictionary<int, int> spaces) {
        AddHeadBlockColumns(taskref.TaskrefKeyword, taskref.Identifier, HeadBlocks(taskref), spaces);
    }

    /// <summary>
    /// Legt die <see cref="ColumnId.TaskHeadBlock"/>-Spalte eines Task-/<c>taskref</c>-Kopfs an: Spalte
    /// des <c>[</c> von Block 1 (<c>keyword + " " + Identifier + " "</c>, Tiefe 0 → reine
    /// Space-Ausrichtung ab Spalte 0) für die Stapel-Lücken <c>] → [</c> und den Kommentar-Grenzfall der
    /// Lücke Identifier → Block 1. Liefert die Kopf-Spalte zurück (Basis für die Params-Spalte) bzw.
    /// <c>−1</c>, wenn der Kopf defekt (fehlendes Keyword/Identifier) oder blocklos ist.
    /// </summary>
    static int AddHeadBlockColumns(SyntaxToken keyword, SyntaxToken identifier, List<CodeSyntax> blocks, Dictionary<int, int> spaces) {

        if (keyword.IsMissing || identifier.IsMissing) {
            // Defekter Kopf — Sache der Fehler-Unterdrückung (S4), hier keine Kanonisierung.
            return -1;
        }

        if (blocks.Count == 0) {
            return -1;
        }

        var headColumn = keyword.Length + 1 + identifier.Length + 1;

        spaces[identifier.End] = headColumn;
        for (var i = 1; i < blocks.Count; i++) {
            spaces[blocks[i - 1].End] = headColumn;
        }

        return headColumn;
    }

    /// <summary>
    /// Nur vom Autor <b>mehrzeilig</b> gelegte <c>[params …]</c> werden unter dem ersten Parameter
    /// ausgerichtet (Einzeiler bleiben einzeilig — kein Tabellen-Eintrag, die Regel fällt auf das
    /// Komma+Space-Idiom). Params-Spalte = Kopf-Spalte + <c>"[params "</c>.
    /// </summary>
    static void AddParamsListColumn(SyntaxTree syntaxTree, CodeParamsDeclarationSyntax? paramsDeclaration, int headColumn, Dictionary<int, int> spaces) {

        var parameterList = paramsDeclaration?.ParameterList;
        if (parameterList == null || paramsDeclaration!.ParamsKeyword.IsMissing) {
            return;
        }

        if (syntaxTree.SourceText.Substring(paramsDeclaration.Extent).IndexOf('\n') < 0) {
            return;
        }

        var paramsColumn = headColumn + "[params ".Length;

        spaces[paramsDeclaration.ParamsKeyword.End] = paramsColumn;
        foreach (var token in parameterList.ChildTokens()) {
            if (token.Type == SyntaxTokenType.Comma) {
                spaces[token.End] = paramsColumn;
            }
        }
    }

    /// <summary>Die vorhandenen Kopf-Blöcke der Task-Definition in Quellreihenfolge.</summary>
    static List<CodeSyntax> HeadBlocks(TaskDefinitionSyntax task) {

        var blocks = new List<CodeSyntax>(capacity: 5);

        void Add(CodeSyntax? block) {
            if (block != null) {
                blocks.Add(block);
            }
        }

        Add(task.CodeDeclaration);
        Add(task.CodeBaseDeclaration);
        Add(task.CodeGenerateToDeclaration);
        Add(task.CodeParamsDeclaration);
        Add(task.CodeResultDeclaration);

        blocks.Sort((a, b) => a.Start.CompareTo(b.Start));

        return blocks;
    }

    /// <summary>Die vorhandenen Kopf-Blöcke der <c>taskref</c>-Deklaration in Quellreihenfolge.</summary>
    static List<CodeSyntax> HeadBlocks(TaskDeclarationSyntax taskref) {

        var blocks = new List<CodeSyntax>(capacity: 3);

        void Add(CodeSyntax? block) {
            if (block != null) {
                blocks.Add(block);
            }
        }

        Add(taskref.CodeNamespaceDeclaration);
        Add(taskref.CodeNotImplementedDeclaration);
        Add(taskref.CodeResultDeclaration);

        blocks.Sort((a, b) => a.Start.CompareTo(b.Start));

        return blocks;
    }

    // ---- Pfeil-Spalte ----------------------------------------------------------------------------

    sealed class ArrowCandidate {

        public ArrowCandidate(SyntaxNode statement) {
            Statement = statement;
        }

        public SyntaxNode Statement      { get; }
        public bool       BreaksGroup    { get; set; }
        public bool       IsAligned      { get; set; }
        public int        GapStart       { get; set; }
        public int        Width          { get; set; }
        public int        AuthoredColumn { get; set; }

    }

    static void AddArrowColumns(SyntaxTree syntaxTree, NavFormattingOptions options, StatementFacts.Map facts, TransitionDefinitionBlockSyntax block, Dictionary<int, int> spaces) {

        var candidates = new List<ArrowCandidate>();

        foreach (var transition in block.TransitionDefinitions) {
            candidates.Add(CreateArrowCandidate(syntaxTree, options, facts, transition, transition.Edge, transition.Semicolon));
        }

        foreach (var exitTransition in block.ExitTransitionDefinitions) {
            candidates.Add(CreateArrowCandidate(syntaxTree, options, facts, exitTransition, exitTransition.Edge, exitTransition.Semicolon));
        }

        candidates.Sort((a, b) => a.Statement.Start.CompareTo(b.Statement.Start));

        foreach (var group in GroupCandidates(syntaxTree, candidates, c => c.Statement, c => c.BreaksGroup)) {

            var participants = group.Where(c => c.IsAligned).ToList();
            if (participants.Count < 2) {
                continue;
            }

            var tightMin  = participants.Max(c => c.Width) + 1;
            var targetCol = ResolveTargetColumn(tightMin, participants.Select(c => c.AuthoredColumn), options);

            foreach (var participant in participants) {
                spaces[participant.GapStart] = targetCol - participant.Width;
            }
        }
    }

    /// <summary>
    /// Vermisst eine (Exit-)Transition für die Pfeil-Spalte: kanonische Vor-Pfeil-Breite (Token-Texte +
    /// Regel-entschiedene Lücken — nie <c>ToString()</c>, der Ist-Whitespace wird ja gerade normalisiert).
    /// Defekt (fehlende Kante / fehlendes <c>;</c>) oder hand-gelegt ⇒ bricht die Gruppe; ein
    /// Inline-Block-Kommentar im Vor-Pfeil-Bereich ⇒ nur aus der Spalte ausgeschlossen.
    /// </summary>
    static ArrowCandidate CreateArrowCandidate(SyntaxTree syntaxTree, NavFormattingOptions options, StatementFacts.Map facts,
                                               SyntaxNode statement, EdgeSyntax? edge, SyntaxToken semicolon) {

        var candidate   = new ArrowCandidate(statement);
        var edgeKeyword = edge?.Keyword ?? SyntaxToken.Missing;

        if (edgeKeyword.IsMissing || semicolon.IsMissing) {
            candidate.BreaksGroup = true;
            return candidate;
        }

        var statementFacts = facts.For(statement);
        if (statementFacts.BreaksSingleLineForm) {
            candidate.BreaksGroup = true;
            return candidate;
        }

        var tokens = statementFacts.Tokens;

        var preArrow = tokens.TakeWhile(t => t.Start < edgeKeyword.Start).ToList();
        if (preArrow.Count == 0) {
            candidate.BreaksGroup = true;
            return candidate;
        }

        var width = preArrow[0].Length;
        for (var i = 1; i < preArrow.Count; i++) {
            var gapWidth = CanonicalGapWidth(syntaxTree, options, preArrow[i - 1], preArrow[i]);
            if (gapWidth < 0) {
                return candidate;
            }

            width += gapWidth + preArrow[i].Length;
        }

        if (GapTrivia.Create(preArrow[preArrow.Count - 1], edgeKeyword, syntaxTree.SourceText).HasComment) {
            return candidate;
        }

        candidate.IsAligned      = true;
        candidate.GapStart       = preArrow[preArrow.Count - 1].End;
        candidate.Width          = width;
        candidate.AuthoredColumn = AuthoredColumn(syntaxTree, preArrow[0], edgeKeyword, options);

        return candidate;
    }

    // ---- Nachgestellte tight-Spalten (Trigger / Condition / Trailing-Kommentar) ------------------

    /// <summary>
    /// Der gemeinsame Kandidat der drei <b>tight</b> ausgerichteten Spalten (Trigger, Condition,
    /// Trailing-Kommentar): eine auszurichtende Anweisung mit ihrer kanonischen Vor-Spalten-Breite und der
    /// Startposition der zu paddenden Lücke. <see cref="BreaksGroup"/> = defekt/hand-gelegt (bricht die
    /// Ausrichtungsgruppe), <see cref="IsAligned"/> = trägt die Spalte tatsächlich (Nicht-Teilnehmer laufen
    /// als Gruppen-erhaltende Mitläufer mit).
    /// </summary>
    sealed class ClauseCandidate {

        public ClauseCandidate(SyntaxNode statement) {
            Statement = statement;
        }

        public SyntaxNode Statement   { get; }
        public bool       BreaksGroup { get; set; }
        public bool       IsAligned   { get; set; }
        public int        GapStart    { get; set; }
        public int        Width       { get; set; }

    }

    /// <summary>
    /// Der generische Tight-Spalten-Baustein für die drei nachgestellten Klausel-Spalten (Trigger,
    /// Condition, Trailing-<c>//</c>-Kommentar). Er vermisst je Anweisung einen <see cref="ClauseCandidate"/>
    /// über den übergebenen <paramref name="measure"/>-Selektor, partitioniert bei
    /// <c>interruptThreshold: 1</c> (anders als Pfeil/Node-Grid bricht schon <b>eine einzelne</b> Leerzeile
    /// bzw. Kommentarzeile die Gruppe) und löst jede Gruppe mit ≥ 2 Teilnehmern <b>tight</b> auf: ein Space
    /// hinter der längsten Zeile (<c>max(Breite) + 1</c>, kein Tab-Stopp/keine
    /// <see cref="AlignmentColumnPolicy"/>). Das Padding landet in <paramref name="target"/> — für Trigger/
    /// Condition ist das die Haupt-<c>spaces</c>-Tabelle, für den Trailing-Kommentar die eigene
    /// <c>trailingCommentSpaces</c>-Tabelle (der Renderer schlägt ihn dort direkt nach).
    /// </summary>
    static void AddTightClauseColumns(SyntaxTree syntaxTree, IEnumerable<SyntaxNode> statements,
                                      Func<SyntaxNode, ClauseCandidate> measure, Dictionary<int, int> target) {

        var candidates = statements.OrderBy(s => s.Start).Select(measure).ToList();

        foreach (var group in GroupCandidates(syntaxTree, candidates, c => c.Statement, c => c.BreaksGroup, interruptThreshold: 1)) {

            var participants = group.Where(c => c.IsAligned).ToList();
            if (participants.Count < 2) {
                continue;
            }

            var targetCol = participants.Max(c => c.Width) + 1;

            foreach (var participant in participants) {
                target[participant.GapStart] = targetCol - participant.Width;
            }
        }
    }

    /// <summary>
    /// Vermisst eine (Exit-)Transition für eine nachgestellte Klausel-Spalte (Trigger bzw. Condition,
    /// gewählt über <paramref name="clauseOf"/>): kanonische Breite ab Zeilenanfang bis zur führenden
    /// Klausel — die inneren Lücken kommen aus der Regelentscheidung, eine bereits aufgelöste Pfeil-/
    /// Trigger-Spalte aus <paramref name="spaces"/> (damit die Ausrichtungen nicht auseinanderlaufen).
    /// Defekt (fehlende Kante / fehlendes <c>;</c>) oder hand-gelegt ⇒ bricht die Gruppe; fehlt die Klausel
    /// (triggerlose bzw. bedingungslose Transition, jede Exit-Transition beim Trigger) ⇒ kein Teilnehmer,
    /// bricht die Gruppe aber nicht; ein Kommentar/eine Direktive im Vor-Klausel-Bereich ⇒ nur aus der
    /// Spalte ausgeschlossen.
    /// </summary>
    static ClauseCandidate MeasureTransitionClause(SyntaxTree syntaxTree, NavFormattingOptions options, StatementFacts.Map facts,
                                                   SyntaxNode statement, Func<SyntaxNode, SyntaxNode?> clauseOf, Dictionary<int, int> spaces) {

        var candidate                = new ClauseCandidate(statement);
        var (edgeKeyword, semicolon) = TransitionHead(statement);

        if (edgeKeyword.IsMissing || semicolon.IsMissing) {
            candidate.BreaksGroup = true;
            return candidate;
        }

        var statementFacts = facts.For(statement);
        if (statementFacts.BreaksSingleLineForm) {
            candidate.BreaksGroup = true;
            return candidate;
        }

        var tokens = statementFacts.Tokens;

        var clause = clauseOf(statement);
        if (clause == null) {
            // Keine Klausel (triggerlose/bedingungslose Transition, Exit beim Trigger) — kein Teilnehmer,
            // aber die Gruppe bleibt bestehen.
            return candidate;
        }

        var width = WidthUpToColumn(syntaxTree, options, tokens, clause.Start, spaces, out var gapStart);
        if (width < 0) {
            return candidate;
        }

        candidate.IsAligned = true;
        candidate.GapStart  = gapStart;
        candidate.Width     = width;

        return candidate;
    }

    /// <summary>Kante-Keyword (bzw. <see cref="SyntaxToken.Missing"/>) und <c>;</c> einer (Exit-)Transition.</summary>
    static (SyntaxToken EdgeKeyword, SyntaxToken Semicolon) TransitionHead(SyntaxNode statement) => statement switch {
        TransitionDefinitionSyntax t     => (t.Edge?.Keyword ?? SyntaxToken.Missing, t.Semicolon),
        ExitTransitionDefinitionSyntax e => (e.Edge?.Keyword ?? SyntaxToken.Missing, e.Semicolon),
        _                                => (SyntaxToken.Missing, SyntaxToken.Missing),
    };

    /// <summary>Der Trigger einer Transition (nur <see cref="TransitionDefinitionSyntax"/> trägt einen).</summary>
    static SyntaxNode? TriggerOf(SyntaxNode statement) => (statement as TransitionDefinitionSyntax)?.Trigger;

    /// <summary>Die Bedingungsklausel einer (Exit-)Transition.</summary>
    static SyntaxNode? ConditionOf(SyntaxNode statement) => statement switch {
        TransitionDefinitionSyntax t     => t.ConditionClause,
        ExitTransitionDefinitionSyntax e => e.ConditionClause,
        _                                => null,
    };

    /// <summary>
    /// Kanonische Breite ab Zeilenanfang (erstes Token) bis zum Token, das bei
    /// <paramref name="columnStart"/> beginnt (exklusiv), Summe der Token-Textlängen + der inneren Lücken.
    /// Eine Lücke, die bereits eine aufgelöste Ausrichtung trägt (z.B. die Pfeil-Spalte), wird aus
    /// <paramref name="spaces"/> übernommen — so bleibt die Messung mit dem späteren Rendering konsistent;
    /// sonst über die Regelentscheidung (<see cref="CanonicalGapWidth"/>). <c>−1</c>, wenn eine Lücke nicht
    /// einzeilig-kanonisch ist (Kommentar/Direktive/Umbruch). <paramref name="gapStart"/> ist die
    /// Startposition der auszurichtenden Lücke (Ende des letzten Tokens vor <paramref name="columnStart"/>).
    /// </summary>
    static int WidthUpToColumn(SyntaxTree syntaxTree, NavFormattingOptions options, IReadOnlyList<SyntaxToken> tokens,
                               int columnStart, Dictionary<int, int> spaces, out int gapStart) {

        gapStart = tokens[0].End;
        var width = tokens[0].Length;

        for (var i = 1; i < tokens.Count && tokens[i].Start < columnStart; i++) {

            var start = tokens[i - 1].End;
            if (!spaces.TryGetValue(start, out var gapWidth)) {
                gapWidth = CanonicalGapWidth(syntaxTree, options, tokens[i - 1], tokens[i]);
            }

            if (gapWidth < 0) {
                return -1;
            }

            width  += gapWidth + tokens[i].Length;
            gapStart = tokens[i].End;
        }

        return width;
    }

    // ---- Trailing-//-Kommentar-Spalte -----------------------------------------------------------

    /// <summary>
    /// Vermisst eine Anweisung für die Trailing-<c>//</c>-Kommentar-Spalte (tight über
    /// <see cref="AddTightClauseColumns"/> aufgelöst): kanonische Zeilenbreite bis zum letzten Token (die
    /// inneren Lücken kommen aus den bereits aufgelösten Spalten bzw. der Regelentscheidung). Hand-gelegt/
    /// leer ⇒ bricht die Gruppe; kein sauberer Trailing-<c>//</c> (nur Whitespace davor) ⇒ kein Teilnehmer
    /// (bricht nicht); eine nicht einzeilig-kanonische innere Lücke (z.B. Inline-Block-Kommentar) ⇒ aus der
    /// Spalte ausgeschlossen (die Spaltenbreite darf nie an einer Kommentar-Textlänge hängen).
    /// </summary>
    static ClauseCandidate MeasureTrailingComment(SyntaxTree syntaxTree, NavFormattingOptions options, StatementFacts.Map facts,
                                                  SyntaxNode statement, Dictionary<int, int> spaces) {

        var candidate      = new ClauseCandidate(statement);
        var statementFacts = facts.For(statement);
        var tokens         = statementFacts.Tokens;

        if (tokens.Count == 0 || statementFacts.BreaksSingleLineForm) {
            candidate.BreaksGroup = true;
            return candidate;
        }

        if (!HasCleanTrailingLineComment(tokens[tokens.Count - 1])) {
            return candidate;
        }

        var width = WidthUpToColumn(syntaxTree, options, tokens, int.MaxValue, spaces, out var gapStart);
        if (width < 0) {
            return candidate;
        }

        candidate.IsAligned = true;
        candidate.GapStart  = gapStart;
        candidate.Width     = width;

        return candidate;
    }

    /// <summary>
    /// Ob das Token einen sauberen Trailing-<c>//</c>-Kommentar trägt: die erste nicht-Whitespace-Trivia
    /// seiner Trailing-Trivia ist ein <see cref="SyntaxTokenType.SingleLineComment"/> (ein vorangestellter
    /// Block-Kommentar, ein Zeilenende oder eine Direktive schließen aus — die Kommentar-Spalte hinge sonst
    /// an fremdem Trivia).
    /// </summary>
    static bool HasCleanTrailingLineComment(SyntaxToken token) {

        foreach (var trivia in token.TrailingTrivia) {
            switch (trivia.Type) {
                case SyntaxTokenType.Whitespace:
                    continue;
                case SyntaxTokenType.SingleLineComment:
                    return true;
                default:
                    return false;
            }
        }

        return false;
    }

    // ---- Node-Deklarations-Raster (keyword | node | rest) ----------------------------------------

    sealed class NodeGridCandidate {

        public NodeGridCandidate(SyntaxNode declaration) {
            Declaration = declaration;
        }

        public SyntaxNode  Declaration        { get; }
        public bool        BreaksGroup        { get; set; }
        public bool        IsAligned          { get; set; }
        public SyntaxToken Keyword            { get; set; } = SyntaxToken.Missing;
        public SyntaxToken Node               { get; set; } = SyntaxToken.Missing;
        public SyntaxToken Rest               { get; set; } = SyntaxToken.Missing;
        public bool        RestIsParams       { get; set; }
        public int         AuthoredNodeColumn { get; set; }
        public int         AuthoredRestColumn { get; set; }

    }

    static void AddNodeGridColumns(SyntaxTree syntaxTree, NavFormattingOptions options, StatementFacts.Map facts,
                                   IEnumerable<NodeDeclarationSyntax> declarations, Dictionary<int, int> spaces) {

        var candidates = declarations.OrderBy(d => d.Start)
                                     .Select(d => CreateNodeGridCandidate(syntaxTree, options, facts, d))
                                     .ToList();

        foreach (var group in GroupCandidates(syntaxTree, candidates, c => c.Declaration, c => c.BreaksGroup)) {

            // Spalte 2 (node): Teilnehmer sind die Zeilen mit node-Identifier (end; nimmt nicht teil).
            var nodeParticipants = group.Where(c => c.IsAligned).ToList();
            if (nodeParticipants.Count < 2) {
                continue;
            }

            var nodeTightMin = nodeParticipants.Max(c => c.Keyword.Length) + 1;
            var nodeCol      = ResolveTargetColumn(nodeTightMin, nodeParticipants.Select(c => c.AuthoredNodeColumn), options);

            foreach (var participant in nodeParticipants) {
                spaces[participant.Keyword.End] = nodeCol - participant.Keyword.Length;
            }

            // Spalte 3 (Alias-Rest): nur Zeilen mit Rest, aber ohne [params] — kein Phantom-Padding;
            // ausgerichtet wird der Start des Rests, nie sein Inhalt. Policy-gesteuert (wie Node/Pfeil).
            var restParticipants = nodeParticipants.Where(c => !c.Rest.IsMissing && !c.RestIsParams).ToList();
            if (restParticipants.Count >= 2) {
                var restTightMin = restParticipants.Max(c => nodeCol + c.Node.Length) + 1;
                var restCol      = ResolveTargetColumn(restTightMin, restParticipants.Select(c => c.AuthoredRestColumn), options);

                foreach (var participant in restParticipants) {
                    spaces[participant.Node.End] = restCol - (nodeCol + participant.Node.Length);
                }
            }

            // Eigene [params]-Spalte: aufeinanderfolgende [params]-Blöcke untereinander, aber immer
            // TIGHT (ein Space hinter dem längsten Vor-params-Präfix der Gruppe, kein Tab-Stopp/keine
            // Policy) — der schwergewichtige Block soll minimal, nicht unnötig weit nach rechts sitzen.
            // Bei nur einem params-Teilnehmer entsteht kein Eintrag → Single-Space (kein Wandern).
            var paramsParticipants = nodeParticipants.Where(c => c.RestIsParams).ToList();
            if (paramsParticipants.Count >= 2) {
                var paramsCol = paramsParticipants.Max(c => nodeCol + c.Node.Length) + 1;

                foreach (var participant in paramsParticipants) {
                    spaces[participant.Node.End] = paramsCol - (nodeCol + participant.Node.Length);
                }
            }
        }
    }

    /// <summary>
    /// Vermisst eine Node-Deklaration für das 3-Spalten-Raster: Spalte 2 ist das erste Identifier nach
    /// dem Keyword, Spalte 3 das erste Token danach (sofern vorhanden und nicht das <c>;</c>).
    /// Hand-gelegt/defekt ⇒ bricht die Gruppe (fällt wie eine hand-gelegte Anweisung aus dem Raster);
    /// ein Inline-Block-Kommentar in einer Raster-Lücke ⇒ nur aus der jeweiligen Spalte ausgeschlossen.
    /// </summary>
    static NodeGridCandidate CreateNodeGridCandidate(SyntaxTree syntaxTree, NavFormattingOptions options, StatementFacts.Map facts, NodeDeclarationSyntax declaration) {

        var candidate      = new NodeGridCandidate(declaration);
        var statementFacts = facts.For(declaration);
        var tokens         = statementFacts.Tokens;

        if (tokens.Count == 0 || !statementFacts.EndsWithSemicolon || statementFacts.BreaksSingleLineForm) {
            candidate.BreaksGroup = true;
            return candidate;
        }

        candidate.Keyword = tokens[0];

        if (tokens.Count < 3 || tokens[1].Type != SyntaxTokenType.Identifier || !ReferenceEquals(tokens[1].Parent, declaration)) {
            // Nur das Keyword (end;) bzw. kein node-Identifier — keine Spalten, bricht aber nicht.
            return candidate;
        }

        if (GapTrivia.Create(tokens[0], tokens[1], syntaxTree.SourceText).HasComment) {
            return candidate;
        }

        candidate.Node               = tokens[1];
        candidate.IsAligned          = true;
        candidate.AuthoredNodeColumn = AuthoredColumn(syntaxTree, tokens[0], tokens[1], options);

        if (tokens[2].Type != SyntaxTokenType.Semicolon &&
            !GapTrivia.Create(tokens[1], tokens[2], syntaxTree.SourceText).HasComment) {
            // Ein [params]-Block bekommt eine eigene, tight ausgerichtete Spalte (ColumnId.NodeParams),
            // getrennt vom Alias-Rest — er soll durch einen langen Alias/Node nicht nach rechts wandern.
            candidate.Rest               = tokens[2];
            candidate.RestIsParams       = tokens[2].Parent is CodeParamsDeclarationSyntax;
            candidate.AuthoredRestColumn = AuthoredColumn(syntaxTree, tokens[0], tokens[2], options);
        }

        return candidate;
    }

    // ---- Gemeinsame Bausteine ---------------------------------------------------------------------

    /// <summary>
    /// Partitioniert die Kandidaten in Ausrichtungsgruppen: neue Gruppe bei
    /// <c>interruptLines ≥ interruptThreshold</c> oder an einem Gruppen-brechenden Kandidaten (der selbst
    /// nie Mitglied wird). <paramref name="interruptThreshold"/> ist standardmäßig <c>2</c> (eine
    /// Leerzeile oder Kommentarzeile bricht nicht — Pfeil-/Node-Grid-Spalte); die Trigger-, Condition- und
    /// Trailing-Kommentar-Ausrichtung übergeben <c>1</c> (bereits eine einzelne Leerzeile bricht den Block).
    /// </summary>
    static IEnumerable<List<T>> GroupCandidates<T>(SyntaxTree syntaxTree, IReadOnlyList<T> candidates,
                                                   Func<T, SyntaxNode> statementOf, Func<T, bool> breaksGroup,
                                                   int interruptThreshold = 2) {

        var group = new List<T>();
        SyntaxNode? previous = null;

        foreach (var candidate in candidates) {

            var statement = statementOf(candidate);

            if (breaksGroup(candidate)) {
                if (group.Count > 0) {
                    yield return group;
                    group = new List<T>();
                }

                previous = statement;
                continue;
            }

            if (previous != null && group.Count > 0 && InterruptLines(syntaxTree, previous, statement) >= interruptThreshold) {
                yield return group;
                group = new List<T>();
            }

            group.Add(candidate);
            previous = statement;
        }

        if (group.Count > 0) {
            yield return group;
        }
    }

    /// <summary>
    /// Zeilen strikt zwischen zwei aufeinanderfolgenden Anweisungen (leere Zeilen + eigene
    /// Kommentarzeilen) = Newline-Trivia der Lücke − 1. Newlines im Inneren mehrzeiliger Kommentare
    /// zählen nicht (<see cref="GapTrivia"/>).
    /// </summary>
    static int InterruptLines(SyntaxTree syntaxTree, SyntaxNode previous, SyntaxNode next) {

        var prevToken = syntaxTree.Tokens.FindAtPosition(previous.End - 1);
        var nextToken = syntaxTree.Tokens.FindAtPosition(next.Start);

        return GapTrivia.Create(prevToken, nextToken, syntaxTree.SourceText).NewLineCount - 1;
    }

    /// <summary>
    /// Kanonische Breite einer inneren Lücke — über die Regelentscheidung selbst (<c>Nothing</c> = 0,
    /// <c>SingleSpace</c> = 1), damit Breitenmessung und späteres Rendering nie auseinanderlaufen.
    /// Eine <see cref="GapLayout.AlignedColumn"/> ohne eigenen Tabellen-Eintrag rendert der
    /// <see cref="GapRenderer"/> als Single-Space-Fallback (ein Leerzeichen) — dieser Fall wird ohne
    /// Tabellen-Eintrag erreicht (der Aufrufer schlägt die Tabelle vorher nach) und zählt deshalb hier als
    /// <c>1</c>. −1, wenn die Lücke nicht einzeilig-kanonisch ist (Kommentar/Direktive/Skiped bzw. ein
    /// Umbruch-Layout) — der Aufrufer schließt die Anweisung dann aus der Spalte aus.
    /// </summary>
    static int CanonicalGapWidth(SyntaxTree syntaxTree, NavFormattingOptions options, SyntaxToken prev, SyntaxToken next) {

        var trivia = GapTrivia.Create(prev, next, syntaxTree.SourceText);
        if (trivia.HasComment || trivia.HasDirective || trivia.HasSkippedTokens) {
            return -1;
        }

        var ctx = new GapContext(prev, next, indentDepth: 0, trivia, isSuppressed: false, AlignmentMap.Empty, options);

        return GapRules.Select(in ctx) switch {
            GapLayout.Nothing       => 0,
            GapLayout.SingleSpace   => 1,
            GapLayout.AlignedColumn => 1,
            _                       => -1,
        };
    }

    /// <summary>
    /// Ist-Spalte des auszurichtenden Tokens ab Inhaltsbeginn der Zeile (= erstes Token der Anweisung;
    /// der Einzug geht nie in die Breite ein), Tabs bei <see cref="NavFormattingOptions.IndentSize"/>
    /// aufgelöst. Wird nur von <see cref="AlignmentColumnPolicy.PreserveDominant"/> ausgewertet.
    /// </summary>
    static int AuthoredColumn(SyntaxTree syntaxTree, SyntaxToken firstToken, SyntaxToken alignedToken, NavFormattingOptions options) {

        var indentSize = Math.Max(1, options.IndentSize);
        var text       = syntaxTree.SourceText.Substring(TextExtent.FromBounds(firstToken.Start, alignedToken.Start));

        var col = 0;
        foreach (var ch in text) {
            col = ch == '\t' ? (col / indentSize + 1) * indentSize : col + 1;
        }

        return col;
    }

    /// <summary>
    /// Löst die Zielspalte (0-basierte Startspalte ab Inhaltsbeginn) aus <paramref name="tightMin"/> und
    /// der konfigurierten <see cref="AlignmentColumnPolicy"/> auf — <paramref name="tightMin"/> ist der
    /// Boden aller Policies (Padding nie &lt; 1 Space).
    /// </summary>
    static int ResolveTargetColumn(int tightMin, IEnumerable<int> authoredColumns, NavFormattingOptions options) {

        switch (options.AlignmentColumnPolicy) {

            case AlignmentColumnPolicy.Tight:
                return tightMin;

            case AlignmentColumnPolicy.PreserveDominant:
                return Math.Max(tightMin, DominantColumn(authoredColumns));

            default: {
                // NextTabStop: nächster Tab-Stopp ≥ tightMin (Vielfaches von IndentSize) — per
                // Korpus-Kalibrierung bestätigt (~91% der uniform ausgerichteten Pfeil-Spalten liegen
                // auf einem Tab-Vielfachen).
                var indentSize = Math.Max(1, options.IndentSize);
                return (tightMin + indentSize - 1) / indentSize * indentSize;
            }
        }
    }

    /// <summary>Die häufigste Ist-Spalte (bei Gleichstand die kleinste).</summary>
    static int DominantColumn(IEnumerable<int> authoredColumns) {

        var counts = new Dictionary<int, int>();
        foreach (var col in authoredColumns) {
            counts.TryGetValue(col, out var n);
            counts[col] = n + 1;
        }

        var dominant      = 0;
        var dominantCount = 0;
        foreach (var pair in counts) {
            if (pair.Value > dominantCount || (pair.Value == dominantCount && pair.Key < dominant)) {
                dominant      = pair.Key;
                dominantCount = pair.Value;
            }
        }

        return dominant;
    }

}
