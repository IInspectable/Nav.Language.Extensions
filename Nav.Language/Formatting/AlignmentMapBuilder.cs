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

    public static AlignmentMap Build(SyntaxTree syntaxTree, NavFormattingOptions options) {

        if (!options.AlignArrows && !options.AlignNodeGrid && !options.AlignTaskHeadBlocks && !options.AlignConditions) {
            return AlignmentMap.Empty;
        }

        var spaces = new Dictionary<int, int>();

        foreach (var task in syntaxTree.Root.DescendantNodes<TaskDefinitionSyntax>()) {

            if (options.AlignTaskHeadBlocks) {
                AddTaskHeadColumns(syntaxTree, task, spaces);
            }

            if (options.AlignNodeGrid) {
                AddNodeGridColumns(syntaxTree, options, task.NodeDeclarationBlock.NodeDeclarations, spaces);
            }

            if (options.AlignArrows) {
                AddArrowColumns(syntaxTree, options, task.TransitionDefinitionBlock, spaces);
            }

            // Nach der Pfeil-Spalte: die Condition-Spalte baut auf die bereits aufgelösten Pfeil-Paddings
            // auf (sie steckt in derselben Zeile rechts vom Pfeil) — daher zwingend nach AddArrowColumns.
            if (options.AlignConditions) {
                AddConditionColumns(syntaxTree, options, task.TransitionDefinitionBlock, spaces);
            }
        }

        if (options.AlignNodeGrid) {
            foreach (var taskref in syntaxTree.Root.DescendantNodes<TaskDeclarationSyntax>()) {
                AddNodeGridColumns(syntaxTree, options, taskref.ConnectionPoints, spaces);
            }
        }

        return new AlignmentMap(spaces);
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

        var keyword    = task.TaskKeyword;
        var identifier = task.Identifier;

        if (keyword.IsMissing || identifier.IsMissing) {
            // Defekter Kopf — Sache der Fehler-Unterdrückung (S4), hier keine Kanonisierung.
            return;
        }

        var blocks = HeadBlocks(task);
        if (blocks.Count == 0) {
            return;
        }

        var headColumn = keyword.Length + 1 + identifier.Length + 1;

        spaces[identifier.End] = headColumn;
        for (var i = 1; i < blocks.Count; i++) {
            spaces[blocks[i - 1].End] = headColumn;
        }

        AddParamsListColumn(syntaxTree, task.CodeParamsDeclaration, headColumn, spaces);
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

    static void AddArrowColumns(SyntaxTree syntaxTree, NavFormattingOptions options, TransitionDefinitionBlockSyntax block, Dictionary<int, int> spaces) {

        var candidates = new List<ArrowCandidate>();

        foreach (var transition in block.TransitionDefinitions) {
            candidates.Add(CreateArrowCandidate(syntaxTree, options, transition, transition.Edge, transition.Semicolon));
        }

        foreach (var exitTransition in block.ExitTransitionDefinitions) {
            candidates.Add(CreateArrowCandidate(syntaxTree, options, exitTransition, exitTransition.Edge, exitTransition.Semicolon));
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
    static ArrowCandidate CreateArrowCandidate(SyntaxTree syntaxTree, NavFormattingOptions options,
                                               SyntaxNode statement, EdgeSyntax? edge, SyntaxToken semicolon) {

        var candidate   = new ArrowCandidate(statement);
        var edgeKeyword = edge?.Keyword ?? SyntaxToken.Missing;

        if (edgeKeyword.IsMissing || semicolon.IsMissing) {
            candidate.BreaksGroup = true;
            return candidate;
        }

        var tokens = syntaxTree.Tokens[statement.Extent].ToList();
        if (IsHandLaid(syntaxTree, tokens)) {
            candidate.BreaksGroup = true;
            return candidate;
        }

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

    // ---- Condition-Spalte (if / else if / else) --------------------------------------------------

    sealed class ConditionCandidate {

        public ConditionCandidate(SyntaxNode statement) {
            Statement = statement;
        }

        public SyntaxNode Statement   { get; }
        public bool       BreaksGroup { get; set; }
        public bool       IsAligned   { get; set; }
        public int        GapStart    { get; set; }
        public int        Width       { get; set; }

    }

    /// <summary>
    /// Richtet die <c>if</c>/<c>else if</c>/<c>else</c>-Bedingungen aufeinanderfolgender (Exit-)Transitionen
    /// spaltenweise aus — dieselbe Gruppenbildung wie die Pfeil-Spalte. Nur Transitionen <b>mit</b>
    /// <see cref="ConditionClauseSyntax"/> nehmen teil; eine bedingungslose Transition ist kein Teilnehmer,
    /// bricht die Gruppe aber nicht (im Korpus die häufige erste, unbedingte Kante). Ausrichtung nur bei
    /// ≥ 2 Teilnehmern je Gruppe — und <b>immer tight</b> (ein Space hinter der längsten Zeile, kein
    /// Tab-Stopp/keine <see cref="AlignmentColumnPolicy"/>, wie die <see cref="ColumnId.NodeParams"/>-Spalte):
    /// die nachgestellte Klausel soll minimal sitzen, nicht unnötig weit nach rechts.
    /// </summary>
    static void AddConditionColumns(SyntaxTree syntaxTree, NavFormattingOptions options, TransitionDefinitionBlockSyntax block, Dictionary<int, int> spaces) {

        var candidates = new List<ConditionCandidate>();

        foreach (var transition in block.TransitionDefinitions) {
            candidates.Add(CreateConditionCandidate(syntaxTree, options, transition, transition.Edge, transition.Semicolon, transition.ConditionClause, spaces));
        }

        foreach (var exitTransition in block.ExitTransitionDefinitions) {
            candidates.Add(CreateConditionCandidate(syntaxTree, options, exitTransition, exitTransition.Edge, exitTransition.Semicolon, exitTransition.ConditionClause, spaces));
        }

        candidates.Sort((a, b) => a.Statement.Start.CompareTo(b.Statement.Start));

        foreach (var group in GroupCandidates(syntaxTree, candidates, c => c.Statement, c => c.BreaksGroup)) {

            var participants = group.Where(c => c.IsAligned).ToList();
            if (participants.Count < 2) {
                continue;
            }

            var targetCol = participants.Max(c => c.Width) + 1;

            foreach (var participant in participants) {
                spaces[participant.GapStart] = targetCol - participant.Width;
            }
        }
    }

    /// <summary>
    /// Vermisst eine (Exit-)Transition für die Condition-Spalte: kanonische Breite ab Zeilenanfang bis zum
    /// führenden Klausel-Keyword — die inneren Lücken kommen aus der Regelentscheidung, die bereits
    /// aufgelöste Pfeil-Spalte aus <paramref name="spaces"/> (damit Pfeil- und Condition-Ausrichtung nicht
    /// auseinanderlaufen). Defekt (fehlende Kante / fehlendes <c>;</c>) oder hand-gelegt ⇒ bricht die
    /// Gruppe; keine Condition ⇒ kein Teilnehmer (bricht nicht); ein Kommentar/eine Direktive im
    /// Vor-Condition-Bereich ⇒ nur aus der Spalte ausgeschlossen.
    /// </summary>
    static ConditionCandidate CreateConditionCandidate(SyntaxTree syntaxTree, NavFormattingOptions options,
                                                       SyntaxNode statement, EdgeSyntax? edge, SyntaxToken semicolon,
                                                       ConditionClauseSyntax? conditionClause, Dictionary<int, int> spaces) {

        var candidate   = new ConditionCandidate(statement);
        var edgeKeyword = edge?.Keyword ?? SyntaxToken.Missing;

        if (edgeKeyword.IsMissing || semicolon.IsMissing) {
            candidate.BreaksGroup = true;
            return candidate;
        }

        var tokens = syntaxTree.Tokens[statement.Extent].ToList();
        if (IsHandLaid(syntaxTree, tokens)) {
            candidate.BreaksGroup = true;
            return candidate;
        }

        if (conditionClause == null) {
            // Bedingungslose Transition — kein Teilnehmer, aber die Gruppe bleibt bestehen.
            return candidate;
        }

        var conditionStart = conditionClause.Start;
        var width          = WidthUpToColumn(syntaxTree, options, tokens, conditionStart, spaces, out var gapStart);
        if (width < 0) {
            return candidate;
        }

        candidate.IsAligned = true;
        candidate.GapStart  = gapStart;
        candidate.Width     = width;

        return candidate;
    }

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

    static void AddNodeGridColumns(SyntaxTree syntaxTree, NavFormattingOptions options,
                                   IEnumerable<NodeDeclarationSyntax> declarations, Dictionary<int, int> spaces) {

        var candidates = declarations.OrderBy(d => d.Start)
                                     .Select(d => CreateNodeGridCandidate(syntaxTree, options, d))
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
    static NodeGridCandidate CreateNodeGridCandidate(SyntaxTree syntaxTree, NavFormattingOptions options, NodeDeclarationSyntax declaration) {

        var candidate = new NodeGridCandidate(declaration);
        var tokens    = syntaxTree.Tokens[declaration.Extent].ToList();

        if (tokens.Count == 0 || tokens[tokens.Count - 1].Type != SyntaxTokenType.Semicolon || IsHandLaid(syntaxTree, tokens)) {
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
    /// Partitioniert die Kandidaten in Ausrichtungsgruppen: neue Gruppe bei <c>interruptLines ≥ 2</c>
    /// oder an einem Gruppen-brechenden Kandidaten (der selbst nie Mitglied wird).
    /// </summary>
    static IEnumerable<List<T>> GroupCandidates<T>(SyntaxTree syntaxTree, IReadOnlyList<T> candidates,
                                                   Func<T, SyntaxNode> statementOf, Func<T, bool> breaksGroup) {

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

            if (previous != null && group.Count > 0 && InterruptLines(syntaxTree, previous, statement) >= 2) {
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
    /// Hand-gelegt-Primitive (dieselbe Erkennung wie die spätere S4-Unterdrückung): eine <b>innere</b>
    /// Lücke der Anweisung enthält einen Newline, einen zeilen-erzwingenden Kommentar, eine Direktive
    /// oder einen Skiped-Lauf — die Anweisung ist nie einzeilig-kanonisch.
    /// </summary>
    static bool IsHandLaid(SyntaxTree syntaxTree, IReadOnlyList<SyntaxToken> tokens) {

        for (var i = 1; i < tokens.Count; i++) {
            var trivia = GapTrivia.Create(tokens[i - 1], tokens[i], syntaxTree.SourceText);
            if (trivia.NewLineCount > 0 || trivia.HasLineBreakingComment || trivia.HasDirective || trivia.HasSkippedTokens) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Kanonische Breite einer inneren Lücke — über die Regelentscheidung selbst (<c>Nothing</c> = 0,
    /// <c>SingleSpace</c> = 1), damit Breitenmessung und späteres Rendering nie auseinanderlaufen.
    /// −1, wenn die Lücke nicht einzeilig-kanonisch ist (Kommentar/Direktive/Skiped bzw. ein
    /// Umbruch-Layout) — der Aufrufer schließt die Anweisung dann aus der Spalte aus.
    /// </summary>
    static int CanonicalGapWidth(SyntaxTree syntaxTree, NavFormattingOptions options, SyntaxToken prev, SyntaxToken next) {

        var trivia = GapTrivia.Create(prev, next, syntaxTree.SourceText);
        if (trivia.HasComment || trivia.HasDirective || trivia.HasSkippedTokens) {
            return -1;
        }

        var ctx = new GapContext(prev, next, indentDepth: 0, trivia, isSuppressed: false, AlignmentMap.Empty, options);

        return GapRules.Select(in ctx) switch {
            GapLayout.Nothing     => 0,
            GapLayout.SingleSpace => 1,
            _                     => -1,
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
