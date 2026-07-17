using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Pharmatechnik.Nav.Language.Text;

namespace Nav.Language.Tests;

/// <summary>
/// Ergebnis des Parsens einer <em>Markup</em>-Nav-Quelle: der um alle Marker bereinigte
/// <see cref="Source"/> plus die daraus extrahierten Positionen und Spans. Damit lassen sich Caret-
/// und Erwartungs-Positionen <em>im</em> Nav-Code verankern, statt sie über wiederholte
/// Substring-Suche (<c>IndexOf</c>) nachträglich zu berechnen.
/// </summary>
/// <remarks>
/// <para><b>Marker-Vokabular</b> — einziges Trennzeichen ist die Pipe <c>|</c>:</para>
/// <list type="table">
///   <listheader><term>Marker</term><description>Bedeutung</description></listheader>
///   <item><term><c>|</c></term>
///         <description>Caret (eine Position) — höchstens einer je Markup, abrufbar über
///         <see cref="Caret"/>.</description></item>
///   <item><term><c>|name:…|</c></term>
///         <description>benannter Span über den Text zwischen Doppelpunkt und schließendem <c>|</c>;
///         gleiche Namen dürfen sich wiederholen (<see cref="Span"/> / <see cref="Spans"/> /
///         <see cref="Position"/>).</description></item>
///   <item><term><c>|:…|</c></term>
///         <description>anonymer Span (leerer Name), abrufbar über
///         <see cref="AnonymousSpan"/> / <see cref="AnonymousSpans"/>.</description></item>
///   <item><term><c>|name:|</c></term>
///         <description>Sonderfall „leerer Inhalt" — ein Span der Länge 0, also eine <em>benannte
///         Position</em>.</description></item>
///   <item><term><c>||</c></term>
///         <description>Escape für ein literales <c>|</c> im Quelltext.</description></item>
/// </list>
/// <para><b>Warum steht der Name vorne?</b> Der Doppelpunkt <em>unmittelbar hinter</em> dem öffnenden
/// <c>|</c> ist das Signal „hier beginnt ein Span, kein Caret" — die Entscheidung fällt lokal am
/// Öffner, ohne Vorausschau. Ein <c>|</c>, dem kein <c>[name]:</c> folgt, ist zweifelsfrei ein Caret;
/// ein <c>|:</c> bzw. <c>|name:</c> zweifelsfrei ein Öffner. Alle Offsets beziehen sich auf den
/// bereinigten <see cref="Source"/> (der Marker-Index <em>im</em> Rohtext ist der Offset im
/// bereinigten Text).</para>
/// <para><b>Bekannte Grenze:</b> Nav nutzt <c>:</c> selbst (Connection-Points wie <c>Sub:se</c>). Ein
/// Caret <em>direkt vor</em> so einem <c>name:</c>-Token würde als Span-Öffner gelesen. Der Fall ist
/// im Test-Korpus praktisch nicht vorhanden; träte er auf, scheitert <see cref="Parse"/> wegen des
/// fehlenden schließenden <c>|</c> <em>laut</em> (statt still danebenzuliegen). Spans verschachteln
/// nicht und enthalten selbst keinen Caret.</para>
/// <para><b>Test-lokal vor geteilt:</b> Positionen bevorzugt in eine <em>test-lokale</em>
/// Markup-Quelle mit einem einzelnen <see cref="Caret"/> setzen — dort ist der Bezug direkt am Code
/// sichtbar. Ein über mehrere Tests <em>geteilter</em> <c>NavMarkup</c> mit benannten Markern lohnt
/// nur, wenn der Share inhaltlich trägt: mehrere Tests prüfen denselben Sachverhalt an <em>derselben</em>
/// Quelle (z.B. „from declaration" und „from reference", die aufeinander auflösen). Andernfalls häuft
/// er die Marker unabhängiger Tests in einer Quelle und erzwingt ein Namens-Nachschlagen aus der Ferne
/// — genau die Indirektion, die die Marker eigentlich beseitigen.</para>
/// </remarks>
public sealed record NavMarkup {

    const int NoCaret = -1;

    readonly ImmutableDictionary<string, ImmutableArray<TextExtent>> _spans;

    NavMarkup(string source, int caret, ImmutableDictionary<string, ImmutableArray<TextExtent>> spans) {
        Source = source;
        Caret  = caret;
        _spans = spans;
    }

    /// <summary>Die um alle Marker bereinigte Nav-Quelle — genau das, was an den Parser geht.</summary>
    public string Source { get; }

    /// <summary>
    /// Offset des primären Carets (<c>|</c>) im <see cref="Source"/>; <c>-1</c>, wenn keiner gesetzt ist
    /// (dann adressieren die Tests ihre Caret-Position typischerweise über einen benannten Marker via
    /// <see cref="Position"/>).
    /// </summary>
    public int Caret { get; }

    /// <summary>Ob ein primärer Caret gesetzt wurde.</summary>
    public bool HasCaret => Caret != NoCaret;

    /// <summary>Der (einzige) Span eines benannten Markers. Wirft, wenn der Name 0-mal oder mehrfach vorkommt.</summary>
    public TextExtent Span(string name) => Spans(name).Single();

    /// <summary>Start-Offset eines benannten Markers — die häufigste Caret-/Erwartungs-Quelle.</summary>
    public int Position(string name) => Span(name).Start;

    /// <summary>Alle gleichnamigen Marker in Reihenfolge ihres Auftretens (leer, wenn keiner existiert).</summary>
    public ImmutableArray<TextExtent> Spans(string name) =>
        _spans.TryGetValue(name, out var extents) ? extents : ImmutableArray<TextExtent>.Empty;

    /// <summary>Alle anonymen Spans (<c>|:…|</c>).</summary>
    public ImmutableArray<TextExtent> AnonymousSpans => Spans(name: "");

    /// <summary>Der (einzige) anonyme Span. Wirft, wenn 0 oder mehrere existieren.</summary>
    public TextExtent AnonymousSpan => AnonymousSpans.Single();

    /// <summary>Zerlegt eine Markup-Quelle in <see cref="Source"/>, <see cref="Caret"/> und Spans.</summary>
    /// <exception cref="FormatException">Bei mehr als einem Caret oder unbalanciertem Span-Öffner.</exception>
    public static NavMarkup Parse(string markup) {

        if (markup == null) {
            throw new ArgumentNullException(nameof(markup));
        }

        var source = new StringBuilder(markup.Length);
        var spans  = new Dictionary<string, ImmutableArray<TextExtent>.Builder>();
        int caret  = NoCaret;

        // Zustand des offenen Spans (es verschachtelt nicht — höchstens einer ist offen).
        bool   inSpan    = false;
        string spanName  = "";
        int    spanStart = 0;

        int i = 0, n = markup.Length;
        while (i < n) {

            char c = markup[i];

            if (c != '|') {
                // Gewöhnliches Zeichen — inklusive Span-Inhalt — landet unverändert im Source.
                source.Append(c);
                i++;
                continue;
            }

            if (inSpan) {
                // Innerhalb eines Spans schließt das nächste '|' den Span; der Offset zählt gegen den
                // bereits bereinigten Source, deckt also exakt den Inhalt ab.
                AddSpan(spans, spanName, TextExtent.FromBounds(start: spanStart, end: source.Length));
                inSpan = false;
                i++;
                continue;
            }

            // Außerhalb eines Spans: Escape, Span-Öffner oder Caret?
            if (i + 1 < n && markup[i + 1] == '|') {
                // '||' → literales '|'
                source.Append('|');
                i += 2;
                continue;
            }

            if (TryReadSpanOpen(markup, i, out spanName, out int afterColon)) {
                inSpan    = true;
                spanStart = source.Length;
                i         = afterColon;
                continue;
            }

            // Bloßes '|' → Caret (höchstens einer).
            if (caret != NoCaret) {
                throw Malformed(markup, i, "Mehr als ein Caret ('|') im Markup.");
            }

            caret = source.Length;
            i++;
        }

        if (inSpan) {
            throw Malformed(markup, i,
                            $"Unbalancierter Span-Öffner (Name '{spanName}') — schließendes '|' fehlt.");
        }

        return new NavMarkup(
            source: source.ToString(),
            caret : caret,
            spans : spans.ToImmutableDictionary(kv => kv.Key, kv => kv.Value.ToImmutable()));
    }

    /// <summary>
    /// Prüft, ob an <paramref name="pipe"/> ein Span-Öffner <c>|name:</c> bzw. <c>|:</c> steht. Nur ein
    /// <c>|</c>, dem ein (optionaler) Bezeichner und ein Doppelpunkt folgen, ist ein Öffner — jedes
    /// andere <c>|</c> ist ein Caret.
    /// </summary>
    static bool TryReadSpanOpen(string s, int pipe, out string name, out int afterColon) {

        name       = "";
        afterColon = -1;

        int n = s.Length;
        int j = pipe + 1;

        if (j >= n) {
            return false;
        }

        if (s[j] == ':') {
            // '|:' — anonymer Span (leerer Name).
            afterColon = j + 1;
            return true;
        }

        if (!IsIdentStart(s[j])) {
            return false;
        }

        int k = j + 1;
        while (k < n && IsIdentPart(s[k])) {
            k++;
        }

        if (k >= n || s[k] != ':') {
            // Bezeichner ohne folgenden Doppelpunkt → kein Öffner (also Caret).
            return false;
        }

        name       = s.Substring(j, k - j);
        afterColon = k + 1;
        return true;
    }

    static void AddSpan(Dictionary<string, ImmutableArray<TextExtent>.Builder> spans,
                        string name, TextExtent extent) {

        if (!spans.TryGetValue(name, out var builder)) {
            builder     = ImmutableArray.CreateBuilder<TextExtent>();
            spans[name] = builder;
        }

        builder.Add(extent);
    }

    static bool IsIdentStart(char c) => char.IsLetter(c) || c == '_';
    static bool IsIdentPart(char c)  => char.IsLetterOrDigit(c) || c == '_';

    static FormatException Malformed(string markup, int index, string message) =>
        new($"Ungültiges NavMarkup an Position {index}: {message}{Environment.NewLine}Markup: {markup}");

}
