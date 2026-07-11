using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Der Fehler-Toleranz-Vorpass des Formatters: entscheidet je Anweisung/Member, ob ihre Lücken
/// <b>verbatim</b> bleiben (Strukturbruch — dem Baum wird nicht getraut) oder ob es sich um eine
/// <b>hand-gelegte</b> (mehrzeilige, aber gültige) Anweisung handelt, deren äußerer Einzug per
/// Delta-Shift re-gesetzt und deren Inneres verbatim bleibt. Der Formatter fragt anschließend pro Lücke
/// nur noch nach.
/// </summary>
/// <remarks>
/// <para><b>Verbatim (Unterdrückung), Auslöser:</b> ein fehlendes Struktur-Token (fehlendes <c>;</c> einer
/// Anweisung, fehlende Klammer eines <c>task</c>/<c>taskref</c>-Blocks), eine <c>SkippedTokensTrivia</c>
/// oder Direktive in einer <b>inneren</b> Lücke, oder eine Error-Severity-Syntax-Diagnostik, die die
/// Anweisung überlappt. <b>BOM-<c>Nav0000</c> bei Offset 0</b> ist ausgenommen (führendes U+FEFF wird als
/// <c>Unknown</c> gelext, ist aber kein Strukturbruch). Unterdrückungs-Einheit ist die kleinste
/// umschließende Anweisung; ein fehlendes <c>}</c> unterdrückt den ganzen Task-Body (Containment unsicher),
/// alles außerhalb wird weiter formatiert. Eine <c>SkippedTokensTrivia</c> <b>zwischen</b> zwei Anweisungen
/// (kein gemeinsamer Anweisungs-Elter) braucht keinen Eintrag — der Renderer lässt solche Lücken ohnehin
/// byte-genau stehen.</para>
/// <para><b>Hand-gelegt (Delta-Shift):</b> eine strukturell gültige Anweisung, deren innere Lücke einen
/// Newline oder zeilen-erzwingenden Kommentar trägt (nie <c>;</c>-los, ohne Skiped/Direktive/Diagnostik).
/// Ihr Inneres bleibt verbatim; die Zeilen werden um dasselbe Einrück-Delta verschoben, das die erste
/// Zeile beim Neu-Einrücken auf den Block-Einzug wandert. Ausgenommen sind der <c>task</c>-/<c>taskref</c>-Kopf
/// (Kanonisierung, s. <see cref="TaskHeadLayoutRule"/>) und das mehrzeilige <c>[params]</c> (kanonisch
/// ausgerichtet) — beide sind keine Anweisungsknoten und werden hier nicht erfasst.</para>
/// <para><b>Idempotenz:</b> die Klassifikation liest nur formatierungs-invariante Fakten (fehlende Token,
/// Trivia-Klasse, Diagnostics); der Hand-gelegt-Delta ist im zweiten Lauf 0 (die erste Zeile sitzt dann
/// bereits auf dem Block-Einzug).</para>
/// </remarks>
sealed class FormatterSuppression {

    readonly IReadOnlyList<TextExtent>  _verbatimExtents;
    readonly IReadOnlyDictionary<int, int> _handLaidShiftByGapStart;

    FormatterSuppression(IReadOnlyList<TextExtent> verbatimExtents, IReadOnlyDictionary<int, int> handLaidShiftByGapStart, bool hasUsableMembers) {
        _verbatimExtents         = verbatimExtents;
        _handLaidShiftByGapStart = handLaidShiftByGapStart;
        HasUsableMembers         = hasUsableMembers;
    }

    /// <summary>
    /// Ob die Datei brauchbare Member trägt. Andernfalls (reiner Müll/leer) formatiert der Service nur die
    /// zwei konservativen Rand-Lücken (Datei-Anfang und Final-Newline/EOF-Trim) — Global-Fallback.
    /// </summary>
    public bool HasUsableMembers { get; }

    /// <summary>Ob die Lücke vollständig in einer unterdrückten (verbatim) Region liegt.</summary>
    public bool IsSuppressed(TextExtent gap) {

        foreach (var extent in _verbatimExtents) {
            if (extent.Contains(gap)) {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Das Einrück-Delta für die innere Lücke einer hand-gelegten Anweisung (identifiziert über ihre
    /// Startposition), oder <c>false</c>, wenn die Lücke nicht zu einer hand-gelegten Anweisung gehört.
    /// </summary>
    public bool TryGetHandLaidShift(int gapStart, out int delta) {
        return _handLaidShiftByGapStart.TryGetValue(gapStart, out delta);
    }

    public static FormatterSuppression Compute(SyntaxTree syntaxTree, NavFormattingOptions options, StatementFacts.Map statementFacts) {

        var verbatim = new List<TextExtent>();

        // (1) Fehlende Klammern eines Task-/taskref-Blocks: fehlendes '{' -> ganzer Task verbatim (Body
        //     nicht lokalisierbar), fehlendes '}' -> Body ab hinter dem '{' verbatim.
        foreach (var task in syntaxTree.Root.DescendantNodes<TaskDefinitionSyntax>()) {
            AddBrokenBlock(task.OpenBrace, task.CloseBrace, task.Extent, verbatim);
        }

        foreach (var taskref in syntaxTree.Root.DescendantNodes<TaskDeclarationSyntax>()) {
            AddBrokenBlock(taskref.OpenBrace, taskref.CloseBrace, taskref.Extent, verbatim);
        }

        // (2) Error-Severity-Syntax-Diagnostik -> kleinste umschließende Anweisung verbatim
        //     (BOM-Nav0000 bei Offset 0 ausgenommen).
        foreach (var diagnostic in syntaxTree.Diagnostics) {

            if (diagnostic.Severity != DiagnosticSeverity.Error) {
                continue;
            }

            if (diagnostic.Descriptor.Id == DiagnosticId.Nav0000 && diagnostic.Location.Start == 0) {
                continue;
            }

            var statement = FindEnclosingStatement(syntaxTree, diagnostic.Location.Start);
            if (statement != null) {
                verbatim.Add(statement.Extent);
            }
        }

        // (3) Anweisungen (Transitionen, Exit-Transitionen, Node-Deklarationen): fehlendes ';',
        //     Skiped/Direktive in einer inneren Lücke -> verbatim; sonst mehrzeilig -> hand-gelegt. Die
        //     Token-Liste und der Trivia-Befund stammen aus der einmal erhobenen, geteilten Messung.
        var handLaid           = new Dictionary<int, int>();
        var handLaidCandidates = new List<StatementFacts>();

        foreach (var facts in statementFacts.All) {

            switch (Classify(facts)) {
                case StatementClass.Suppressed:
                    verbatim.Add(facts.Statement.Extent);
                    break;
                case StatementClass.HandLaid:
                    handLaidCandidates.Add(facts);
                    break;
            }
        }

        var suppression = new FormatterSuppression(verbatim, handLaid, ComputeHasUsableMembers(syntaxTree));

        // Hand-gelegt-Deltas erst nach dem Sammeln aller Verbatim-Regionen: eine hand-gelegte Anweisung in
        // einem defekten Task-Body ist bereits unterdrückt und bekommt keinen (widersprüchlichen) Shift.
        foreach (var facts in handLaidCandidates) {

            if (suppression.IsSuppressed(facts.Statement.Extent)) {
                continue;
            }

            var tokens = facts.Tokens;
            var delta  = HandLaidDelta(syntaxTree, options, tokens[0]);
            for (var i = 1; i < tokens.Count; i++) {
                handLaid[tokens[i - 1].End] = delta;
            }
        }

        return suppression;
    }

    static void AddBrokenBlock(SyntaxToken openBrace, SyntaxToken closeBrace, TextExtent blockExtent, List<TextExtent> verbatim) {

        if (openBrace.IsMissing) {
            verbatim.Add(blockExtent);
        } else if (closeBrace.IsMissing) {
            verbatim.Add(TextExtent.FromBounds(openBrace.End, blockExtent.End));
        }
    }

    enum StatementClass {
        Normal,
        Suppressed,
        HandLaid,
    }

    /// <summary>
    /// Klassifiziert eine Anweisung aus ihren geteilten <see cref="StatementFacts"/> — rein
    /// formatierungs-invariant. Fehlendes <c>;</c> oder Skiped/Direktive im Inneren ⇒ Strukturbruch
    /// (verbatim; eine Direktive mitten in der Anweisung ließe sich nicht per Delta-Shift auf Spalte 0
    /// halten); sonst mehrzeilig ⇒ hand-gelegt, andernfalls normal.
    /// </summary>
    static StatementClass Classify(StatementFacts facts) {

        if (!facts.EndsWithSemicolon || facts.HasStructuralBreakTrivia) {
            return StatementClass.Suppressed;
        }

        return facts.SpansMultipleLines ? StatementClass.HandLaid : StatementClass.Normal;
    }

    /// <summary>
    /// Das Einrück-Delta einer hand-gelegten Anweisung: (Ziel-Einzug des Blocks) − (authored Einrückung
    /// des ersten Tokens), in Zeichen. Nur wenn das erste Token auf einer eigenen Zeile beginnt (davor nur
    /// Whitespace); sonst 0 (kein sinnvoller äußerer Einzug).
    /// </summary>
    static int HandLaidDelta(SyntaxTree syntaxTree, NavFormattingOptions options, SyntaxToken firstToken) {

        var authored = 0;
        var i        = firstToken.Start - 1;
        while (i >= 0 && (syntaxTree.SourceText[i] == ' ' || syntaxTree.SourceText[i] == '\t')) {
            authored++;
            i--;
        }

        if (i >= 0 && syntaxTree.SourceText[i] != '\n') {
            // Das erste Token steht nicht am Zeilenanfang (etwas Nicht-Whitespace davor) — kein Shift.
            return 0;
        }

        var depth      = NavFormattingService.ComputeIndentDepth(firstToken);
        var targetChars = options.IndentStyle == IndentStyle.Tabs ? depth : depth * options.IndentSize;

        return targetChars - authored;
    }

    /// <summary>
    /// Die kleinste umschließende Anweisung/Member der Position — für die Diagnostik-getriebene
    /// Unterdrückung. <c>null</c>, wenn die Position keinem tragenden Token zugeordnet ist oder wenn sie
    /// im <b>Inhalt eines Code-Blocks</b> (<see cref="CodeSyntax"/>) liegt: ein Fehler im eingebetteten
    /// C#-Fragment (z.B. <c>[code Foo]</c>, ein kaputter <c>[params]</c>-Typ) lässt die Nav-Struktur
    /// unangetastet — der Formatter fasst Code-Block-Inneres ohnehin nie an, echte Strukturbrüche fangen
    /// die Token-basierten Auslöser (fehlendes Token, Skiped) ab.
    /// </summary>
    static SyntaxNode? FindEnclosingStatement(SyntaxTree syntaxTree, int position) {

        var token = syntaxTree.Root.FindToken(position);
        if (token.IsMissing || token.Parent == null) {
            return null;
        }

        foreach (var node in token.Parent.AncestorsAndSelf()) {

            if (node is CodeSyntax) {
                // Fehler im Code-Block-Inhalt — nicht unterdrücken (Nav-Struktur bleibt gültig).
                return null;
            }

            if (node is TransitionDefinitionSyntax or ExitTransitionDefinitionSyntax or NodeDeclarationSyntax or
                        TaskDefinitionSyntax or TaskDeclarationSyntax) {
                return node;
            }
        }

        return null;
    }

    static bool ComputeHasUsableMembers(SyntaxTree syntaxTree) {
        return syntaxTree.Root is CodeGenerationUnitSyntax { } unit &&
               (unit.Members.Count > 0 || unit.CodeUsings.Count > 0 || unit.CodeNamespace != null);
    }

}
