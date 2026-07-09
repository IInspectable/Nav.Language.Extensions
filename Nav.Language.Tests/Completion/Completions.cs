using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;              // ISymbol, Syntax, CodeGenerationUnit, SyntaxFacts
using Pharmatechnik.Nav.Language.Completion;   // NavCompletionService, NavCompletionItem(Kind)
using Pharmatechnik.Nav.Language.Text;         // TextExtent

namespace Nav.Language.Tests.Completion;

/// <summary>Erwartetes Item — Label UND Kind gekoppelt (behebt die Entkopplung des Alt-Stils).</summary>
public readonly record struct ExpectedItem(string Label, NavCompletionItemKind Kind);

/// <summary>Fluent-Einstieg + Kind-typisierte Fabriken. Test nutzt <c>using static … Completions;</c>.</summary>
public static class Completions {

    const string DefaultPath = @"n:\av\a.nav";

    // Entry: Markup (genau ein '|'-Caret) -> parsen -> GetCompletions.
    public static CompletionResult At(string markup, string filePath = DefaultPath) {
        var m = NavMarkup.Parse(markup);
        if (!m.HasCaret) {
            throw new ArgumentException("Markup braucht genau einen Caret '|'.");
        }
        var syntax = Syntax.ParseCodeGenerationUnit(text: m.Source, filePath: filePath);
        var unit   = CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
        var items  = NavCompletionService.GetCompletions(unit, m.Caret);
        return new CompletionResult(m.Source, m.Caret, items);
    }

    // Kanonische Autoritäten dünn durchgereicht (Konsistenz mit dem Rest der DSL).
    public static IReadOnlyList<char> TriggerChars => NavCompletionService.TriggerCharacters;
    public static IReadOnlyList<char> CommitChars  => NavCompletionService.CommitCharacters;

    // Kind-typisierte Fabriken.
    public static ExpectedItem Keyword(string label)         => new(label, NavCompletionItemKind.Keyword);
    public static ExpectedItem Task(string label)            => new(label, NavCompletionItemKind.Task);
    public static ExpectedItem Node(string label)            => new(label, NavCompletionItemKind.Node);
    public static ExpectedItem ConnectionPoint(string label) => new(label, NavCompletionItemKind.ConnectionPoint);
    public static ExpectedItem Choice(string label)          => new(label, NavCompletionItemKind.Choice);
    public static ExpectedItem GuiNode(string label)         => new(label, NavCompletionItemKind.GuiNode);
    public static ExpectedItem File(string label)            => new(label, NavCompletionItemKind.File);
}

public sealed class CompletionResult {

    public CompletionResult(string source, int caret, IReadOnlyList<NavCompletionItem> items) {
        Source = source; Caret = caret; Items = items;
    }

    public string Source { get; }
    public int    Caret  { get; }
    public IReadOnlyList<NavCompletionItem> Items { get; }

    // EXAKTE Menge, order-unabhängig, als MULTISET über (Label, Kind).
    // Multiset fängt Duplikate (z.B. 'end' zweimal) automatisch als 'überzählig' -> ersetzt die
    // manuellen Doppel-Eintrag-Regressionstests. Diff-Meldung getrennt nach fehlend/überzählig/
    // falsche-Kind (Labels, die in fehlend UND überzählig auftauchen -> "erwartet Kind X, war Y").
    public CompletionResult Offers(params ExpectedItem[] expected) {

        var actual = Items.Select(i => new ExpectedItem(i.Label, i.Kind)).ToList();

        var expectedCounts = CountBy(expected);
        var actualCounts   = CountBy(actual);

        // Multiset-Differenz je (Label, Kind).
        var missing = new List<ExpectedItem>();   // erwartet, aber nicht (oft genug) vorhanden
        var extra   = new List<ExpectedItem>();    // vorhanden, aber nicht (so oft) erwartet

        foreach (var key in expectedCounts.Keys.Union(actualCounts.Keys)) {
            expectedCounts.TryGetValue(key, out var want);
            actualCounts.TryGetValue(key, out var have);
            for (var k = have; k < want; k++) { missing.Add(key); }
            for (var k = want; k < have; k++) { extra.Add(key); }
        }

        if (missing.Count == 0 && extra.Count == 0) {
            return this;
        }

        Assert.Fail(BuildDiffMessage(missing, extra));
        return this;
    }

    public CompletionResult OffersInOrder(params ExpectedItem[] expected) {
        var actual = Items.Select(i => new ExpectedItem(i.Label, i.Kind)).ToArray();
        Assert.That(actual, Is.EqualTo(expected),
                    "Completion-Reihenfolge stimmt nicht (Reihenfolge ist Vertrag).");
        return this;
    }

    public CompletionResult OffersNothing() { Assert.That(Items, Is.Empty); return this; }

    // Escape-Luken (bewusst selten: nur wo die Vollmenge unbeschränkt ist).
    public CompletionResult Includes(params ExpectedItem[] expected) {
        var actual = Items.Select(i => new ExpectedItem(i.Label, i.Kind)).ToHashSet();
        var missing = expected.Where(e => !actual.Contains(e)).ToList();
        if (missing.Count > 0) {
            Assert.Fail("Erwartete Items fehlen:" + Environment.NewLine +
                        string.Join(Environment.NewLine, missing.Select(m => "    - " + Describe(m))));
        }
        return this;
    }

    public CompletionResult Excludes(params string[] labels) {
        var present = labels.Where(l => Items.Any(i => i.Label == l)).ToList();
        if (present.Count > 0) {
            Assert.Fail("Unerwünschte Labels vorhanden: " + string.Join(", ", present.Select(l => $"'{l}'")));
        }
        return this;
    }

    public CompletionResult DoesNotOffer(params string[] labels) => Excludes(labels);

    // Commit-Effekt (Roslyn-Art) und Einzel-Item-Drilldown.
    public CommitAssertion Commit(string label) => new(this, Single(label));
    public ItemAssertion   Item(string label)   => new(Single(label));

    internal NavCompletionItem Single(string label) =>
        Items.Single(i => i.Label == label);   // wirft aussagekräftig bei 0/≥2 -> deckt Duplikate mit ab

    static Dictionary<ExpectedItem, int> CountBy(IEnumerable<ExpectedItem> items) {
        var counts = new Dictionary<ExpectedItem, int>();
        foreach (var item in items) {
            counts.TryGetValue(item, out var n);
            counts[item] = n + 1;
        }
        return counts;
    }

    // Meldung in drei Rubriken: fehlend, überzählig und — wo ein Label in beiden auftaucht —
    // die konkrete Kind-Verwechslung.
    static string BuildDiffMessage(List<ExpectedItem> missing, List<ExpectedItem> extra) {

        var sb = new StringBuilder();
        sb.AppendLine("Completion-Menge stimmt nicht.");

        var missingLabels = missing.Select(m => m.Label).ToHashSet();
        var extraLabels   = extra.Select(e => e.Label).ToHashSet();
        var wrongKind     = missingLabels.Intersect(extraLabels).ToHashSet();

        var missingOnly = missing.Where(m => !wrongKind.Contains(m.Label)).ToList();
        var extraOnly   = extra.Where(e => !wrongKind.Contains(e.Label)).ToList();

        if (missingOnly.Count > 0) {
            sb.AppendLine("  Fehlend:");
            foreach (var m in missingOnly) {
                sb.AppendLine("    - " + Describe(m));
            }
        }

        if (extraOnly.Count > 0) {
            sb.AppendLine("  Überzählig:");
            foreach (var e in extraOnly) {
                sb.AppendLine("    - " + Describe(e));
            }
        }

        if (wrongKind.Count > 0) {
            sb.AppendLine("  Falsche Kind (Label in beiden):");
            foreach (var label in wrongKind) {
                var want = string.Join("/", missing.Where(m => m.Label == label).Select(m => m.Kind.ToString()).Distinct());
                var have = string.Join("/", extra.Where(e => e.Label == label).Select(e => e.Kind.ToString()).Distinct());
                sb.AppendLine($"    - '{label}': erwartet Kind {want}, war {have}");
            }
        }

        return sb.ToString();
    }

    static string Describe(ExpectedItem item) => $"{item.Kind} '{item.Label}'";
}

/// <summary>Committen = InsertText über den effektiven Ersetzungsbereich anwenden, Resultat prüfen.</summary>
public sealed class CommitAssertion {

    readonly CompletionResult _r;
    readonly NavCompletionItem _item;
    internal CommitAssertion(CompletionResult r, NavCompletionItem item) { _r = r; _item = item; }

    public void Produces(string expectedText) {
        var extent = _item.ReplacementExtent ?? DefaultWordExtent(_r.Source, _r.Caret);
        var result = _r.Source.Substring(0, extent.Start)
                   + _item.InsertText
                   + _r.Source.Substring(extent.End);
        Assert.That(result, Is.EqualTo(expectedText));
    }

    // Host-Default für Items OHNE eigenen Extent: maximaler Bezeichner-Lauf um den Caret.
    // Bezeichner-Zeichen laut SyntaxFacts.IsIdentifierCharacter (Nav zählt u.a. '.' als Bezeichner-Zeichen,
    // deshalb ist '.' auch kein Commit-Char). Die Edge-/Continuation-Span-Tests (Step 3) tragen ALLE
    // einen eigenen Extent -> dieser Zweig wird dort nicht gebraucht.
    static TextExtent DefaultWordExtent(string source, int caret) {
        int start = caret;
        while (start > 0 && SyntaxFacts.IsIdentifierCharacter(source[start - 1])) {
            start--;
        }
        int end = caret;
        while (end < source.Length && SyntaxFacts.IsIdentifierCharacter(source[end])) {
            end++;
        }
        return TextExtent.FromBounds(start, end);
    }
}

public sealed class ItemAssertion {

    readonly NavCompletionItem _item;
    internal ItemAssertion(NavCompletionItem item) { _item = item; }

    public ItemAssertion HasKind(NavCompletionItemKind kind) { Assert.That(_item.Kind, Is.EqualTo(kind));      return this; }
    public ItemAssertion Inserts(string text)                { Assert.That(_item.InsertText, Is.EqualTo(text)); return this; }
    public ItemAssertion HasSymbol<T>() where T : class, ISymbol { Assert.That(_item.Symbol, Is.InstanceOf<T>()); return this; }
    public ItemAssertion CarriesOwnSpan()                    { Assert.That(_item.ReplacementExtent, Is.Not.Null); return this; }
    public ItemAssertion LeavesSpanToHost()                  { Assert.That(_item.ReplacementExtent, Is.Null);    return this; }
}
