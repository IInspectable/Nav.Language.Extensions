using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Pharmatechnik.Nav.Language.Formatting;

/// <summary>
/// Der Regel-Dispatcher des Formatters: eine feste, geordnete Regelliste, die pro Lücke befragt wird —
/// die <b>erste passende Regel gewinnt</b> (Short-Circuit) und liefert genau ein vollständiges
/// <see cref="GapLayout"/>. Es entsteht nie eine kombinierte Entscheidung; das ist die Umsetzung der
/// Ein-Change-pro-Lücke-Invariante auf Entscheidungsebene.
/// </summary>
/// <remarks>
/// Die Reihenfolge ist nicht beliebig, sondern folgt den <see cref="RulePriority"/>-Tiers
/// (Safety &gt; Structure &gt; TokenPair &gt; Alignment &gt; Default): Tier wählen ist eine semantische
/// Entscheidung, kein Listenindex-Raten. Cross-Tier-Overlaps sind gewollt (der höhere Tier preemptiert);
/// <b>innerhalb</b> eines Tiers darf höchstens eine Regel zuständig sein — im Debug-Lauf wird diese
/// Intra-Tier-Disjunktheit für jede Lücke geprüft, damit stille Ordnungs-Abhängigkeiten nicht entstehen.
/// </remarks>
static class GapRules {

    // Die geordnete Regelliste IST die Spezifikation — top-down lesbar, nach Tier geordnet.
    static readonly IGapRule[] Rules = {
        // Safety
        new VerbatimWhenSuppressedRule(),     // unterdrückte Region -> Verbatim
        // Structure
        new BraceOnOwnLineRule(),             // vor '{'/'}' und nach '{' -> eigene Zeile (Allman)
        new MemberBreakRule(),                // nach '}' und nach Top-Level-']' -> neuer Member auf Tiefe 0
        new BlankLineBeforeTransitionsRule(), // Blockgrenze Deklarationen -> Transitionen: mindestens eine Leerzeile
        new StatementBreakRule(),             // nach ';' -> nächste Anweisung auf eigener Zeile
        // TokenPair
        new TightColonRule(),                 // Node ':' Port -> tight
        new PunctuationRule(),                // tight vor ','/';', [-Innenränder, Typ-Interna
        new TaskHeadLayoutRule(),             // Task-/taskref-Kopf: Block 1 inline (Pull-up), Folgeblöcke gestapelt, mehrzeiliges [params]
        // Alignment
        new ArrowAlignmentRule(),             // Quell-Teil -> Edge-Keyword in Gruppe -> Pfeil-Spalte
        new NodeGridAlignmentRule(),          // keyword -> node bzw. node -> rest -> 3-Spalten-Raster
        // Default
        new DefaultSingleSpaceRule(),         // Catch-all -> genau ein Space
    };

    /// <summary>Die Layout-Entscheidung für die Lücke — genau eine, Totalität über den Catch-all garantiert.</summary>
    public static GapLayout Select(in GapContext ctx) {

        AssertIntraTierDisjoint(ctx);

        foreach (var rule in Rules) {
            var layout = rule.Apply(in ctx);
            if (layout != null) {
                return layout;
            }
        }

        // Unerreichbar, solange der Catch-all in der Liste steht — verbatim ist die sichere Antwort.
        return GapLayout.Verbatim.Instance;
    }

    /// <summary>
    /// Debug-Prüfung: wertet für die Lücke <b>alle</b> Prädikate aus und stellt sicher, dass innerhalb
    /// eines Tiers höchstens eine Regel matcht — macht die Prioritäts-Reihenfolge zur geprüften statt
    /// impliziten Eigenschaft. Ungewollter Intra-Tier-Overlap heißt: Prädikat verschärfen, nicht die
    /// Reihenfolge zurechtschieben.
    /// </summary>
    [Conditional("DEBUG")]
    static void AssertIntraTierDisjoint(in GapContext ctx) {

        var matchesPerTier = new Dictionary<RulePriority, IGapRule>();

        foreach (var rule in Rules) {
            if (rule.Apply(in ctx) == null) {
                continue;
            }

            if (matchesPerTier.TryGetValue(rule.Tier, out var first)) {
                Debug.Fail($"Intra-Tier-Overlap im Tier {rule.Tier}: " +
                           $"{first.GetType().Name} und {rule.GetType().Name} matchen beide die Lücke " +
                           $"[{ctx.Extent.Start}-{ctx.Extent.End}] ({ctx.Prev.Type} → {ctx.Next.Type}).");
            }

            matchesPerTier[rule.Tier] = rule;
        }
    }

}

/// <summary>Safety: Lücken in unterdrückten Regionen bleiben verbatim — preemptiert bewusst jede Layout-Regel.</summary>
sealed class VerbatimWhenSuppressedRule: IGapRule {

    public RulePriority Tier => RulePriority.Safety;

    public GapLayout? Apply(in GapContext ctx) => ctx.IsSuppressed ? GapLayout.Verbatim.Instance : null;

}

/// <summary>
/// Structure: Allman-Klammern. Vor <c>{</c> und vor <c>}</c> beginnt eine eigene Zeile (die Klammern
/// liegen an der Blockgrenze und stehen selbst auf Tiefe 0); nach <c>{</c> beginnt der Body auf eigener
/// Zeile. Autoren-Leerzeilen bleiben erhalten (kein Kollaps) — <c>BlankLinesBefore: 0</c> ist nur das
/// Minimum, der Renderer kappt nie.
/// </summary>
sealed class BraceOnOwnLineRule: IGapRule {

    public RulePriority Tier => RulePriority.Structure;

    public GapLayout? Apply(in GapContext ctx) =>
        ctx.Next.Type is SyntaxTokenType.OpenBrace or SyntaxTokenType.CloseBrace ||
        ctx.Prev.Type == SyntaxTokenType.OpenBrace
            ? new GapLayout.NewLine(BlankLinesBefore: 0, ctx.IndentDepth)
            : null;

}

/// <summary>
/// Structure: nach dem <c>}</c> eines Task-Blocks und nach dem schließenden <c>]</c> eines
/// Top-Level-Code-Members (<c>[namespaceprefix …]</c>/<c>[using …]</c>) beginnt der nächste Member auf
/// eigener Zeile. Bewusst ausgenommen: <c>Next == ';'</c> (das Idiom <c>};</c> bleibt tight, die
/// <see cref="PunctuationRule"/> ist zuständig) sowie Klammern (Intra-Tier-Disjunktheit zur
/// <see cref="BraceOnOwnLineRule"/>). Das <c>]</c> eines <c>[namespaceprefix …]</c> im
/// <c>taskref</c>-Kopf zählt nicht — nur direkte Kinder der <see cref="CodeGenerationUnitSyntax"/>
/// sind Member-Enden.
/// </summary>
sealed class MemberBreakRule: IGapRule {

    public RulePriority Tier => RulePriority.Structure;

    public GapLayout? Apply(in GapContext ctx) {

        if (ctx.Next.Type is SyntaxTokenType.Semicolon or SyntaxTokenType.OpenBrace or SyntaxTokenType.CloseBrace) {
            return null;
        }

        var isMemberEnd = ctx.Prev.Type == SyntaxTokenType.CloseBrace ||
                          (ctx.Prev.Type == SyntaxTokenType.CloseBracket && IsTopLevelCodeDeclaration(ctx.PrevParent));

        return isMemberEnd ? new GapLayout.NewLine(BlankLinesBefore: 0, ctx.IndentDepth) : null;
    }

    static bool IsTopLevelCodeDeclaration(SyntaxNode? node) =>
        node is CodeNamespaceDeclarationSyntax or CodeUsingDeclarationSyntax &&
        node.Parent is CodeGenerationUnitSyntax;

}

/// <summary>
/// Structure: an der Blockgrenze zwischen der letzten Node-Deklaration und der ersten Transition eines
/// Task-Bodys wird <b>mindestens eine</b> Leerzeile sichergestellt (die einzige Regel, die das
/// Leerzeilen-Minimum anhebt — vorhandene Autoren-Leerzeilen werden nie gekappt). Die Grenze ist rein
/// strukturell: <c>Prev</c> gehört zum <see cref="NodeDeclarationBlockSyntax"/>, <c>Next</c> zum
/// <see cref="TransitionDefinitionBlockSyntax"/> — da die Token im Strom benachbart sind, ist das genau
/// der eine Übergang je Task.
/// </summary>
sealed class BlankLineBeforeTransitionsRule: IGapRule {

    public RulePriority Tier => RulePriority.Structure;

    public GapLayout? Apply(in GapContext ctx) =>
        IsDeclarationToTransitionBoundary(in ctx)
            ? new GapLayout.NewLine(BlankLinesBefore: 1, ctx.IndentDepth)
            : null;

    internal static bool IsDeclarationToTransitionBoundary(in GapContext ctx) =>
        HasAncestor<NodeDeclarationBlockSyntax>(ctx.PrevParent) &&
        HasAncestor<TransitionDefinitionBlockSyntax>(ctx.NextParent);

    static bool HasAncestor<T>(SyntaxNode? node) where T: SyntaxNode =>
        node != null && node.AncestorsAndSelf().OfType<T>().Any();

}

/// <summary>
/// Structure: nach einem <c>;</c> beginnt die nächste Anweisung auf eigener Zeile (Autoren-Leerzeilen
/// bleiben erhalten). Ausgenommen sind Klammern (<see cref="BraceOnOwnLineRule"/> zuständig) und die
/// Blockgrenze Deklarationen → Transitionen (<see cref="BlankLineBeforeTransitionsRule"/> hebt dort das
/// Leerzeilen-Minimum an) — Intra-Tier-Disjunktheit per Prädikat, nicht per Reihenfolge.
/// </summary>
sealed class StatementBreakRule: IGapRule {

    public RulePriority Tier => RulePriority.Structure;

    public GapLayout? Apply(in GapContext ctx) {

        if (ctx.Prev.Type != SyntaxTokenType.Semicolon) {
            return null;
        }

        if (ctx.Next.Type is SyntaxTokenType.OpenBrace or SyntaxTokenType.CloseBrace) {
            return null;
        }

        if (BlankLineBeforeTransitionsRule.IsDeclarationToTransitionBoundary(in ctx)) {
            return null;
        }

        return new GapLayout.NewLine(BlankLinesBefore: 0, ctx.IndentDepth);
    }

}

/// <summary>TokenPair: <c>Node:Port</c> bleibt tight — kein Whitespace um den Doppelpunkt.</summary>
sealed class TightColonRule: IGapRule {

    public RulePriority Tier => RulePriority.TokenPair;

    public GapLayout? Apply(in GapContext ctx) =>
        ctx.Next.Type == SyntaxTokenType.Colon || ctx.Prev.Type == SyntaxTokenType.Colon
            ? GapLayout.Nothing.Instance
            : null;

}

/// <summary>
/// TokenPair: die Interpunktions-Grundwahrheiten, die sonst der Catch-all mit falschen Spaces fluten
/// würde — tight vor <c>,</c>/<c>;</c> (überall: <c>end;</c>, <c>};</c>, Listen), tight an den
/// Innenrändern der <c>[</c>…<c>]</c>-Code-Blöcke sowie die Typ-Interna in <c>[params]</c>-Typen
/// (Generik-Spitzklammern, Nullable-<c>?</c>, Array-Klammern kleben beidseitig).
/// </summary>
/// <remarks>
/// Bewusste Abgrenzungen: <i>nach</i> <c>,</c> liefert der Catch-all das Komma+Space-Idiom; die Lücke
/// Typ-Ende → Parametername (<c>&gt; name</c>, <c>] name</c>, <c>? name</c>) bleibt Single-Space (kein
/// Prev-seitiges Tight für <c>&gt;</c>/<c>]</c>/<c>?</c> — deren tight-Nachbarschaften sind alle über
/// die Next-Seite abgedeckt). Der Lückentyp <c>] → [</c> ist nur im Array-Rang tight (Eltern-Knoten
/// <see cref="ArrayRankSpecifierSyntax"/>) — der gleiche Lückentyp zwischen Task-Kopf-Blöcken gehört ab
/// S3 der TaskHeadLayoutRule (per Eltern-Knoten disjunkt). Doppelpunkt-Nachbarschaften gehören der
/// <see cref="TightColonRule"/> (Intra-Tier-Disjunktheit).
/// </remarks>
sealed class PunctuationRule: IGapRule {

    public RulePriority Tier => RulePriority.TokenPair;

    public GapLayout? Apply(in GapContext ctx) {

        if (ctx.Prev.Type == SyntaxTokenType.Colon || ctx.Next.Type == SyntaxTokenType.Colon) {
            return null;
        }

        return IsTight(in ctx) ? GapLayout.Nothing.Instance : null;
    }

    static bool IsTight(in GapContext ctx) {

        if (ctx.Next.Type is SyntaxTokenType.Comma or SyntaxTokenType.Semicolon) {
            return true;
        }

        if (ctx.Prev.Type == SyntaxTokenType.OpenBracket || ctx.Next.Type == SyntaxTokenType.CloseBracket) {
            return true;
        }

        if (ctx.Prev.Type == SyntaxTokenType.LessThan ||
            ctx.Next.Type is SyntaxTokenType.LessThan or SyntaxTokenType.GreaterThan or SyntaxTokenType.Questionmark) {
            return true;
        }

        if (ctx.Next.Type == SyntaxTokenType.OpenBracket && ctx.NextParent is ArrayRankSpecifierSyntax) {
            return true;
        }

        return false;
    }

}

/// <summary>
/// TokenPair: Kanonisierung des Task-/<c>taskref</c>-Kopfs. Im <b>Task</b>-Kopf steht der erste
/// Code-Block immer genau ein Space hinter dem Identifier (Hochziehen authored Umbrüche via Pull-up),
/// jeder weitere Block auf eigener Zeile linksbündig unter dem <c>[</c> des ersten
/// (<see cref="ColumnId.TaskHeadBlock"/>); ein vom Autor <b>mehrzeilig</b> gelegtes <c>[params …]</c>
/// richtet die Folgeparameter unter dem ersten aus (<see cref="ColumnId.ParamsList"/> — ob die Liste
/// mehrzeilig ist, hat der Vorpass entschieden: nur dann existiert der Spalten-Eintrag). Der
/// <b>taskref</b>-Kopf wird dagegen einzeilig normalisiert (kein Stapeln — die Blöcke sind
/// leichtgewichtig); erzwingt dort ein Kommentar den Umbruch, greift die Renderer-Schranke.
/// </summary>
/// <remarks>
/// Der Lückentyp <c>] → [</c> gehört hier nur den Kopf-Blöcken (Eltern-Knoten
/// <see cref="CodeSyntax"/> unter Task-Definition/-Deklaration) — der gleiche Lückentyp im Array-Rang
/// ist Sache der <see cref="PunctuationRule"/> (per Eltern-Knoten disjunkt, beide TokenPair). Die
/// Klammer vor dem <c>{</c> gehört weiter der <see cref="BraceOnOwnLineRule"/> (Structure, preemptiert).
/// Node-Deklarationen im Body (<c>[donotinject]</c>/<c>[abstractmethod]</c>/<c>[params]</c> an
/// <c>init</c>/<c>choice</c>/<c>task</c>-Knoten) bleiben unberührt (Wirt ist kein Task-/taskref-Kopf).
/// </remarks>
sealed class TaskHeadLayoutRule: IGapRule {

    public RulePriority Tier => RulePriority.TokenPair;

    public GapLayout? Apply(in GapContext ctx) {

        if (!ctx.Options.AlignTaskHeadBlocks) {
            return null;
        }

        // Lücken vor einem Kopf-Block-'[': Identifier → Block 1 bzw. Block.] → nächster Block.[.
        if (ctx.Next.Type == SyntaxTokenType.OpenBracket && ctx.NextParent is CodeSyntax block) {

            switch (block.Parent) {

                case TaskDefinitionSyntax task:
                    if (ctx.Prev == task.Identifier) {
                        // Block 1 immer genau ein Space hinter dem Identifier — auch wenn der Autor ihn
                        // umbrochen hatte (Pull-up). Erzwingt ein Kommentar den Umbruch, fällt Block 1
                        // wie ein Folgeblock auf die kanonische Kopf-Spalte.
                        return ForcesLineBreak(in ctx)
                            ? new GapLayout.NewLineAlignedColumn(BlankLinesBefore: 0, ColumnId.TaskHeadBlock)
                            : GapLayout.SingleSpace.PullUp;
                    }

                    if (ctx.Prev.Type == SyntaxTokenType.CloseBracket && ctx.PrevParent is CodeSyntax previousBlock &&
                        ReferenceEquals(previousBlock.Parent, task)) {
                        return new GapLayout.NewLineAlignedColumn(BlankLinesBefore: 0, ColumnId.TaskHeadBlock);
                    }

                    break;

                case TaskDeclarationSyntax taskref:
                    if (ctx.Prev == taskref.Identifier ||
                        (ctx.Prev.Type == SyntaxTokenType.CloseBracket && ctx.PrevParent is CodeSyntax previousRefBlock &&
                         ReferenceEquals(previousRefBlock.Parent, taskref))) {
                        return GapLayout.SingleSpace.PullUp;
                    }

                    break;
            }

            return null;
        }

        // Mehrzeiliges [params …] im Task-Kopf: ',' → nächster Parameter unter den ersten.
        if (ctx.Prev.Type == SyntaxTokenType.Comma && ctx.PrevParent is ParameterListSyntax { Parent: CodeParamsDeclarationSyntax { Parent: TaskDefinitionSyntax } } &&
            ctx.Alignment.TryGetSpaces(ctx.Extent.Start, out _)) {
            return new GapLayout.NewLineAlignedColumn(BlankLinesBefore: 0, ColumnId.ParamsList);
        }

        // Lücke params → erster Parameter im Task-Kopf: der erste Parameter klebt hinter "params "
        // (Pull-up — er definiert die Params-Spalte); erzwingt ein Kommentar den Umbruch, fällt er auf
        // die Params-Spalte. Ein leeres [params] hat keinen ersten Parameter — die Lücke params → ']'
        // gehört der PunctuationRule (tight; Intra-Tier-Disjunktheit).
        if (ctx.Prev.Type == SyntaxTokenType.ParamsKeyword && ctx.Next.Type != SyntaxTokenType.CloseBracket &&
            ctx.PrevParent is CodeParamsDeclarationSyntax { Parent: TaskDefinitionSyntax }) {
            return ForcesLineBreak(in ctx)
                ? new GapLayout.NewLineAlignedColumn(BlankLinesBefore: 0, ColumnId.ParamsList)
                : GapLayout.SingleSpace.PullUp;
        }

        return null;
    }

    static bool ForcesLineBreak(in GapContext ctx) =>
        ctx.Trivia.HasLineBreakingComment || ctx.Trivia.HasDirective;

}

/// <summary>
/// Alignment: die Lücke zwischen Quell-Teil und Edge-Keyword einer (Exit-)Transition nimmt an der
/// Pfeil-Spalte ihrer Gruppe teil. Ob und wie weit, hat der Vorpass entschieden
/// (<see cref="AlignmentMap"/>); ohne Eintrag (Gruppe der Größe 1, Ausschluss, Option aus) rendert die
/// Lücke als Single-Space. Alle Edge-Keywords sind 3 Zeichen breit — die Spalte hinter dem Pfeil
/// fluchtet automatisch mit. Fortsetzungs-Kanten (<c>--^</c>/<c>o-^</c>) sind kein
/// <see cref="EdgeSyntax"/> und bleiben beim Single-Space-Idiom des Catch-all.
/// </summary>
sealed class ArrowAlignmentRule: IGapRule {

    public RulePriority Tier => RulePriority.Alignment;

    public GapLayout? Apply(in GapContext ctx) =>
        ctx.Options.AlignArrows &&
        ctx.NextParent is EdgeSyntax { Parent: TransitionDefinitionSyntax or ExitTransitionDefinitionSyntax }
            ? new GapLayout.AlignedColumn(ColumnId.Arrow)
            : null;

}

/// <summary>
/// Alignment: das 3-Spalten-Raster der Node-Deklarationen (<c>keyword | node | rest</c>) — Spalte 2 über
/// die Lücke Keyword → node-Identifier, Spalte 3 über die Lücke node-Identifier → erstes Token des
/// Rests (nur der Start, nie der Inhalt). Teilnahme und Spaltenwerte kommen aus dem Vorpass; ohne
/// Eintrag rendert die Lücke als Single-Space. <c>end;</c> hat keinen Identifier und die Lücke vor dem
/// <c>;</c> gehört der <see cref="PunctuationRule"/> — kein Phantom-Padding.
/// </summary>
sealed class NodeGridAlignmentRule: IGapRule {

    public RulePriority Tier => RulePriority.Alignment;

    public GapLayout? Apply(in GapContext ctx) {

        if (!ctx.Options.AlignNodeGrid) {
            return null;
        }

        // Spalte 2: keyword → node (das Keyword ist das erste Token der Deklaration).
        if (ctx.NextParent is NodeDeclarationSyntax declaration &&
            ReferenceEquals(ctx.PrevParent, declaration) &&
            ctx.Prev.Start == declaration.Start &&
            ctx.Next == NodeIdentifier(declaration)) {
            return new GapLayout.AlignedColumn(ColumnId.Node);
        }

        // Spalte 3: node → rest (Next kann einem Kind-Knoten gehören, z.B. [params] oder do-Klausel).
        if (ctx.Prev.Type == SyntaxTokenType.Identifier && ctx.PrevParent is NodeDeclarationSyntax previousDeclaration &&
            ctx.Prev == NodeIdentifier(previousDeclaration) &&
            ctx.Next.Type != SyntaxTokenType.Semicolon && ctx.Next.Start < previousDeclaration.End) {
            return new GapLayout.AlignedColumn(ColumnId.DeclRest);
        }

        return null;
    }

    /// <summary>Der node-Identifier der Deklaration — <c>end;</c> hat keinen (Spalte 2 entfällt).</summary>
    static SyntaxToken NodeIdentifier(NodeDeclarationSyntax declaration) => declaration switch {
        TaskNodeDeclarationSyntax nodeDeclaration   => nodeDeclaration.Identifier,
        InitNodeDeclarationSyntax nodeDeclaration   => nodeDeclaration.Identifier,
        ChoiceNodeDeclarationSyntax nodeDeclaration => nodeDeclaration.Identifier,
        DialogNodeDeclarationSyntax nodeDeclaration => nodeDeclaration.Identifier,
        ViewNodeDeclarationSyntax nodeDeclaration   => nodeDeclaration.Identifier,
        ExitNodeDeclarationSyntax nodeDeclaration   => nodeDeclaration.Identifier,
        _                                           => SyntaxToken.Missing,
    };

}

/// <summary>Der Catch-all: genau ein Space — garantiert Totalität (jede Lücke bekommt eine Entscheidung).</summary>
sealed class DefaultSingleSpaceRule: IGapRule {

    public RulePriority Tier => RulePriority.Default;

    public GapLayout? Apply(in GapContext ctx) => GapLayout.SingleSpace.Instance;

}
