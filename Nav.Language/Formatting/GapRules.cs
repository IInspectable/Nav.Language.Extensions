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

/// <summary>Der Catch-all: genau ein Space — garantiert Totalität (jede Lücke bekommt eine Entscheidung).</summary>
sealed class DefaultSingleSpaceRule: IGapRule {

    public RulePriority Tier => RulePriority.Default;

    public GapLayout? Apply(in GapContext ctx) => GapLayout.SingleSpace.Instance;

}
