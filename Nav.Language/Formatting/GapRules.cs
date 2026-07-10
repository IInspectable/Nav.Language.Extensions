using System.Collections.Generic;
using System.Diagnostics;

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
        new VerbatimWhenSuppressedRule(), // unterdrückte Region -> Verbatim
        // Default
        new PreserveGapRule(),            // Catch-all: Lücke unverändert lassen
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
/// Der Catch-all des Grundzustands: reicht jede Lücke unverändert durch. Solange der Regelsatz nur aus
/// diesem Fallback (und der Safety-Regel) besteht, ist der Formatter die Identität — Layout-Regeln
/// ersetzen diesen Platzhalter durch echte Entscheidungen (zuletzt ein Single-Space-Catch-all).
/// </summary>
sealed class PreserveGapRule: IGapRule {

    public RulePriority Tier => RulePriority.Default;

    public GapLayout? Apply(in GapContext ctx) => GapLayout.Verbatim.Instance;

}
