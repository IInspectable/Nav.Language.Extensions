using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;              // ISymbol, Syntax, CodeGenerationUnit, SyntaxFacts
using Pharmatechnik.Nav.Language.Completion;   // NavCompletionService, NavCompletionItem(Kind)
using Pharmatechnik.Nav.Language.Text;         // TextExtent

namespace Nav.Language.Tests.Completion;

/// <summary>
/// Ein in einer Completion-Erwartung genanntes Item — <paramref name="Label"/> UND
/// <paramref name="Kind"/> sind bewusst gekoppelt und werden stets gemeinsam geprüft.
/// Das behebt die Entkopplung des Alt-Stils, in dem Label und Kind getrennt behauptet wurden und
/// ein Vorschlag mit richtigem Label, aber falscher Kategorie unentdeckt durchrutschen konnte.
/// </summary>
/// <param name="Label">Der angezeigte Text des erwarteten Vorschlags (entspricht <see cref="NavCompletionItem.Label"/>).</param>
/// <param name="Kind">Die erwartete Kategorie des Vorschlags (entspricht <see cref="NavCompletionItem.Kind"/>).</param>
public readonly record struct ExpectedItem(string Label, NavCompletionItemKind Kind);

/// <summary>
/// Fluent-Einstieg in die Completion-Tests samt Kind-typisierter Fabriken für die erwarteten Items.
/// Ein Test bindet die Klasse per <c>using static … Completions;</c> ein und schreibt dadurch
/// z.&#160;B. <c>At("…|…").Offers(Keyword("task"), Task("Foo"))</c>.
/// </summary>
public static class Completions {

    /// <summary>Standard-Dateipfad für <see cref="At"/>, wenn der Test keinen eigenen Pfad braucht.</summary>
    const string DefaultPath = @"n:\av\a.nav";

    /// <summary>
    /// Einstiegspunkt: nimmt ein Markup mit genau einem Caret <c>'|'</c> entgegen, entfernt den Caret,
    /// parst den verbleibenden Quelltext zu einer <see cref="CodeGenerationUnit"/> und ruft die
    /// Completion-Engine an der Caret-Position auf.
    /// </summary>
    /// <param name="markup">Nav-Quelltext mit genau einem <c>'|'</c> als Caret-Markierung.</param>
    /// <param name="filePath">Dateipfad, unter dem geparst wird (Voreinstellung <see cref="DefaultPath"/>).</param>
    /// <returns>Das Ergebnis mit Quelltext, Caret-Position und den ermittelten Vorschlägen zum weiteren Prüfen.</returns>
    /// <exception cref="ArgumentException">Das Markup enthält keinen (oder nach <see cref="NavMarkup.Parse"/> keinen eindeutigen) Caret.</exception>
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

    /// <summary>
    /// Die vom Host anerkannten Trigger-Zeichen, unverändert aus <see cref="NavCompletionService.TriggerCharacters"/>
    /// durchgereicht — damit Tests dieselbe kanonische Autorität prüfen wie die Produktion.
    /// </summary>
    public static IReadOnlyList<char> TriggerChars => NavCompletionService.TriggerCharacters;

    /// <summary>
    /// Die vom Host anerkannten Commit-Zeichen, unverändert aus <see cref="NavCompletionService.CommitCharacters"/>
    /// durchgereicht — damit Tests dieselbe kanonische Autorität prüfen wie die Produktion.
    /// </summary>
    public static IReadOnlyList<char> CommitChars  => NavCompletionService.CommitCharacters;

    /// <summary>Erzeugt eine Erwartung auf ein Schlüsselwort-Item (<see cref="NavCompletionItemKind.Keyword"/>).</summary>
    public static ExpectedItem Keyword(string label)         => new(label, NavCompletionItemKind.Keyword);

    /// <summary>Erzeugt eine Erwartung auf ein Task-Item (<see cref="NavCompletionItemKind.Task"/>).</summary>
    public static ExpectedItem Task(string label)            => new(label, NavCompletionItemKind.Task);

    /// <summary>Erzeugt eine Erwartung auf ein Knoten-Item (<see cref="NavCompletionItemKind.Node"/>).</summary>
    public static ExpectedItem Node(string label)            => new(label, NavCompletionItemKind.Node);

    /// <summary>Erzeugt eine Erwartung auf ein Anschlusspunkt-Item (<see cref="NavCompletionItemKind.ConnectionPoint"/>).</summary>
    public static ExpectedItem ConnectionPoint(string label) => new(label, NavCompletionItemKind.ConnectionPoint);

    /// <summary>Erzeugt eine Erwartung auf ein Choice-Item (<see cref="NavCompletionItemKind.Choice"/>).</summary>
    public static ExpectedItem Choice(string label)          => new(label, NavCompletionItemKind.Choice);

    /// <summary>Erzeugt eine Erwartung auf ein GUI-Knoten-Item (<see cref="NavCompletionItemKind.GuiNode"/>).</summary>
    public static ExpectedItem GuiNode(string label)         => new(label, NavCompletionItemKind.GuiNode);

    /// <summary>Erzeugt eine Erwartung auf ein Datei-/Pfad-Item (<see cref="NavCompletionItemKind.File"/>).</summary>
    public static ExpectedItem File(string label)            => new(label, NavCompletionItemKind.File);
}

/// <summary>
/// Das Ergebnis eines <see cref="Completions.At"/>-Aufrufs und zugleich das fluent Prüf-Objekt:
/// hält Quelltext, Caret-Position und die ermittelten Vorschläge und bietet die Zusicherungen
/// (<see cref="Offers"/>, <see cref="Excludes"/>, <see cref="Commit"/>, …) an, die ein Test verkettet.
/// </summary>
public sealed class CompletionResult {

    /// <summary>Erzeugt das Ergebnis aus dem (caret-freien) Quelltext, der Caret-Position und den Vorschlägen.</summary>
    /// <param name="source">Der geparste Quelltext ohne Caret-Markierung.</param>
    /// <param name="caret">Die Caret-Position (Offset) im Quelltext.</param>
    /// <param name="items">Die von der Engine an dieser Position gelieferten Vorschläge.</param>
    public CompletionResult(string source, int caret, IReadOnlyList<NavCompletionItem> items) {
        Source = source; Caret = caret; Items = items;
    }

    /// <summary>Der geparste Quelltext ohne Caret-Markierung.</summary>
    public string Source { get; }

    /// <summary>Die Caret-Position (Offset) im <see cref="Source"/>, an der die Vorschläge ermittelt wurden.</summary>
    public int    Caret  { get; }

    /// <summary>Die von der Completion-Engine gelieferten Vorschläge.</summary>
    public IReadOnlyList<NavCompletionItem> Items { get; }

    /// <summary>
    /// Prüft die EXAKTE Vorschlagsmenge — reihenfolgeunabhängig, aber als MULTISET über <c>(Label, Kind)</c>.
    /// Als Multiset fängt die Prüfung Duplikate (z.&#160;B. <c>'end'</c> zweimal) automatisch als „überzählig"
    /// ab und ersetzt damit die früheren manuellen Doppel-Eintrag-Regressionstests. Die Fehlermeldung
    /// gliedert die Abweichung in drei Rubriken: fehlend, überzählig und — für Labels, die in beiden
    /// auftauchen — die konkrete Kind-Verwechslung („erwartet Kind X, war Y").
    /// </summary>
    /// <param name="expected">Die vollständig erwartete Menge an Items.</param>
    /// <returns>Dieses Ergebnis, damit weitere Zusicherungen verkettet werden können.</returns>
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

    /// <summary>
    /// Wie <see cref="Offers"/>, prüft aber zusätzlich die REIHENFOLGE: die Vorschläge müssen exakt
    /// und in genau dieser Abfolge geliefert werden (die Reihenfolge ist hier Teil des Vertrags).
    /// </summary>
    /// <param name="expected">Die erwartete Item-Folge in exakter Reihenfolge.</param>
    /// <returns>Dieses Ergebnis, damit weitere Zusicherungen verkettet werden können.</returns>
    public CompletionResult OffersInOrder(params ExpectedItem[] expected) {
        var actual = Items.Select(i => new ExpectedItem(i.Label, i.Kind)).ToArray();
        Assert.That(actual, Is.EqualTo(expected),
                    "Completion-Reihenfolge stimmt nicht (Reihenfolge ist Vertrag).");
        return this;
    }

    /// <summary>Zusicherung, dass an dieser Position überhaupt kein Vorschlag angeboten wird.</summary>
    /// <returns>Dieses Ergebnis, damit weitere Zusicherungen verkettet werden können.</returns>
    public CompletionResult OffersNothing() { Assert.That(Items, Is.Empty); return this; }

    /// <summary>
    /// Escape-Luke (bewusst selten einzusetzen — nur, wo die Vollmenge unbeschränkt ist): prüft, dass
    /// die genannten Items ENTHALTEN sind, ohne die restliche Menge festzuschreiben. Wo immer möglich
    /// ist die exakte <see cref="Offers"/>-Prüfung vorzuziehen.
    /// </summary>
    /// <param name="expected">Die Items, die mindestens vorhanden sein müssen.</param>
    /// <returns>Dieses Ergebnis, damit weitere Zusicherungen verkettet werden können.</returns>
    public CompletionResult Includes(params ExpectedItem[] expected) {
        var actual = Items.Select(i => new ExpectedItem(i.Label, i.Kind)).ToHashSet();
        var missing = expected.Where(e => !actual.Contains(e)).ToList();
        if (missing.Count > 0) {
            Assert.Fail("Erwartete Items fehlen:" + Environment.NewLine +
                        string.Join(Environment.NewLine, missing.Select(m => "    - " + Describe(m))));
        }
        return this;
    }

    /// <summary>
    /// Zusicherung, dass KEINES der genannten Labels angeboten wird (rein label-basiert, ohne Rücksicht
    /// auf die Kind). Gegenstück zu <see cref="Includes"/>.
    /// </summary>
    /// <param name="labels">Die Labels, die nicht vorkommen dürfen.</param>
    /// <returns>Dieses Ergebnis, damit weitere Zusicherungen verkettet werden können.</returns>
    public CompletionResult Excludes(params string[] labels) {
        var present = labels.Where(l => Items.Any(i => i.Label == l)).ToList();
        if (present.Count > 0) {
            Assert.Fail("Unerwünschte Labels vorhanden: " + string.Join(", ", present.Select(l => $"'{l}'")));
        }
        return this;
    }

    /// <summary>Sprechendes Alias für <see cref="Excludes"/> — liest sich am Test-Aufrufort natürlicher.</summary>
    /// <param name="labels">Die Labels, die nicht vorkommen dürfen.</param>
    /// <returns>Dieses Ergebnis, damit weitere Zusicherungen verkettet werden können.</returns>
    public CompletionResult DoesNotOffer(params string[] labels) => Excludes(labels);

    /// <summary>
    /// Einstieg in die Commit-Effekt-Prüfung nach Roslyn-Art: wählt den Vorschlag mit dem genannten
    /// Label aus und erlaubt anschließend, das Ergebnis seiner Anwendung zu prüfen (siehe <see cref="CommitAssertion"/>).
    /// </summary>
    /// <param name="label">Das Label des zu committenden Vorschlags (muss eindeutig sein).</param>
    /// <returns>Eine <see cref="CommitAssertion"/> für den gewählten Vorschlag.</returns>
    public CommitAssertion Commit(string label) => new(this, Single(label));

    /// <summary>
    /// Einstieg in den Einzel-Item-Drilldown: wählt den Vorschlag mit dem genannten Label aus und
    /// erlaubt, seine Eigenschaften einzeln zu prüfen (siehe <see cref="ItemAssertion"/>).
    /// </summary>
    /// <param name="label">Das Label des zu prüfenden Vorschlags (muss eindeutig sein).</param>
    /// <returns>Eine <see cref="ItemAssertion"/> für den gewählten Vorschlag.</returns>
    public ItemAssertion   Item(string label)   => new(Single(label));

    /// <summary>
    /// Wählt den Vorschlag mit dem genannten Label aus. Wirft aussagekräftig, wenn es keinen oder mehr
    /// als einen Treffer gibt — deckt damit zugleich Duplikate ab.
    /// </summary>
    /// <param name="label">Das gesuchte Label.</param>
    /// <returns>Der eindeutige Vorschlag mit diesem Label.</returns>
    internal NavCompletionItem Single(string label) =>
        Items.Single(i => i.Label == label);   // wirft aussagekräftig bei 0/≥2 -> deckt Duplikate mit ab

    /// <summary>Zählt die Items je <see cref="ExpectedItem"/>-Schlüssel <c>(Label, Kind)</c> für den Multiset-Vergleich.</summary>
    static Dictionary<ExpectedItem, int> CountBy(IEnumerable<ExpectedItem> items) {
        var counts = new Dictionary<ExpectedItem, int>();
        foreach (var item in items) {
            counts.TryGetValue(item, out var n);
            counts[item] = n + 1;
        }
        return counts;
    }

    /// <summary>
    /// Baut die Fehlermeldung für <see cref="Offers"/> in drei Rubriken auf: fehlend, überzählig und —
    /// wo ein Label sowohl fehlt als auch überzählig ist — die konkrete Kind-Verwechslung.
    /// </summary>
    /// <param name="missing">Erwartete, aber nicht (oft genug) gelieferte Items.</param>
    /// <param name="extra">Gelieferte, aber nicht (so oft) erwartete Items.</param>
    /// <returns>Die zusammengesetzte, mehrzeilige Fehlermeldung.</returns>
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

    /// <summary>Formatiert ein Item einzeilig als <c>Kind 'Label'</c> für die Fehlermeldungen.</summary>
    static string Describe(ExpectedItem item) => $"{item.Kind} '{item.Label}'";
}

/// <summary>
/// Prüft den Commit-Effekt eines Vorschlags: wendet dessen <see cref="NavCompletionItem.InsertText"/>
/// über den effektiven Ersetzungsbereich auf den Quelltext an und vergleicht das Resultat.
/// </summary>
public sealed class CommitAssertion {

    readonly CompletionResult _r;
    readonly NavCompletionItem _item;

    /// <summary>Erzeugt die Zusicherung für einen konkreten Vorschlag im Kontext seines Ergebnisses.</summary>
    /// <param name="r">Das Ergebnis, aus dem Quelltext und Caret stammen.</param>
    /// <param name="item">Der zu committende Vorschlag.</param>
    internal CommitAssertion(CompletionResult r, NavCompletionItem item) { _r = r; _item = item; }

    /// <summary>
    /// Wendet den Vorschlag an — <see cref="NavCompletionItem.InsertText"/> ersetzt den effektiven
    /// Bereich (eigener <see cref="NavCompletionItem.ReplacementExtent"/> oder, falls keiner gesetzt
    /// ist, der Host-Default aus <see cref="DefaultWordExtent"/>) — und prüft den entstehenden Quelltext.
    /// </summary>
    /// <param name="expectedText">Der nach dem Commit erwartete vollständige Quelltext.</param>
    public void Produces(string expectedText) {
        var extent = _item.ReplacementExtent ?? DefaultWordExtent(_r.Source, _r.Caret);
        var result = _r.Source.Substring(0, extent.Start)
                   + _item.InsertText
                   + _r.Source.Substring(extent.End);
        Assert.That(result, Is.EqualTo(expectedText));
    }

    /// <summary>
    /// Host-Default-Ersetzungsbereich für Vorschläge OHNE eigenen Extent: der maximale Bezeichner-Lauf
    /// um den Caret. Als Bezeichner-Zeichen gilt <see cref="SyntaxFacts.IsIdentifierCharacter"/> (Nav
    /// zählt u.&#160;a. <c>'.'</c> zu den Bezeichner-Zeichen, weshalb <c>'.'</c> auch kein Commit-Char ist).
    /// Vorschläge mit eigenem <see cref="NavCompletionItem.ReplacementExtent"/> (etwa Kanten-/Continuation-
    /// Vorschläge) verwenden diesen Zweig nicht.
    /// </summary>
    /// <param name="source">Der Quelltext.</param>
    /// <param name="caret">Die Caret-Position, um die der Bezeichner-Lauf ermittelt wird.</param>
    /// <returns>Der zu ersetzende Bereich rund um den Caret.</returns>
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

/// <summary>
/// Fluent-Drilldown auf einen einzelnen Vorschlag: prüft dessen Einzeleigenschaften (Kind, Einfügetext,
/// hinterlegtes Symbol und ob er einen eigenen Ersetzungsbereich trägt).
/// </summary>
public sealed class ItemAssertion {

    readonly NavCompletionItem _item;

    /// <summary>Erzeugt die Zusicherung für einen konkreten Vorschlag.</summary>
    /// <param name="item">Der zu prüfende Vorschlag.</param>
    internal ItemAssertion(NavCompletionItem item) { _item = item; }

    /// <summary>Prüft die Kategorie des Vorschlags.</summary>
    /// <param name="kind">Die erwartete <see cref="NavCompletionItemKind"/>.</param>
    /// <returns>Diese Zusicherung, damit weitere Prüfungen verkettet werden können.</returns>
    public ItemAssertion HasKind(NavCompletionItemKind kind) { Assert.That(_item.Kind, Is.EqualTo(kind));      return this; }

    /// <summary>Prüft den beim Commit eingefügten Text (<see cref="NavCompletionItem.InsertText"/>).</summary>
    /// <param name="text">Der erwartete Einfügetext.</param>
    /// <returns>Diese Zusicherung, damit weitere Prüfungen verkettet werden können.</returns>
    public ItemAssertion Inserts(string text)                { Assert.That(_item.InsertText, Is.EqualTo(text)); return this; }

    /// <summary>Prüft, dass am Vorschlag ein Symbol des Typs <typeparamref name="T"/> hinterlegt ist.</summary>
    /// <typeparam name="T">Der erwartete Symboltyp.</typeparam>
    /// <returns>Diese Zusicherung, damit weitere Prüfungen verkettet werden können.</returns>
    public ItemAssertion HasSymbol<T>() where T : class, ISymbol { Assert.That(_item.Symbol, Is.InstanceOf<T>()); return this; }

    /// <summary>Prüft, dass der Vorschlag einen eigenen <see cref="NavCompletionItem.ReplacementExtent"/> trägt.</summary>
    /// <returns>Diese Zusicherung, damit weitere Prüfungen verkettet werden können.</returns>
    public ItemAssertion CarriesOwnSpan()                    { Assert.That(_item.ReplacementExtent, Is.Not.Null); return this; }

    /// <summary>Prüft, dass der Vorschlag KEINEN eigenen Ersetzungsbereich trägt und ihn dem Host überlässt.</summary>
    /// <returns>Diese Zusicherung, damit weitere Prüfungen verkettet werden können.</returns>
    public ItemAssertion LeavesSpanToHost()                  { Assert.That(_item.ReplacementExtent, Is.Null);    return this; }

    /// <summary>Prüft die am Vorschlag hinterlegte Erläuterung (<see cref="NavCompletionItem.Description"/>).</summary>
    /// <param name="description">Die erwartete Beschreibung.</param>
    /// <returns>Diese Zusicherung, damit weitere Prüfungen verkettet werden können.</returns>
    public ItemAssertion HasDescription(string description)  { Assert.That(_item.Description, Is.EqualTo(description)); return this; }

    /// <summary>Prüft, dass der Vorschlag keine Erläuterung trägt (<see cref="NavCompletionItem.Description"/> ist <c>null</c>).</summary>
    /// <returns>Diese Zusicherung, damit weitere Prüfungen verkettet werden können.</returns>
    public ItemAssertion HasNoDescription()                  { Assert.That(_item.Description, Is.Null); return this; }
}
