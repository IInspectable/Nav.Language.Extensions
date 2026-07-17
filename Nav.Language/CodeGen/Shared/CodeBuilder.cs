#region Using Directives

using System;
using System.Collections.Generic;
using System.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Ein einrückungs- und spaltenbewusster Builder für die C#-Codegenerierung — der technische
/// Ersatz für die StringTemplate-Gruppen. Der Builder kennt nur strukturelle C#-Belange
/// (Einrückung, Spaltenausrichtung, Zeilen, Blöcke, Trennlisten); nav-spezifisches Wissen
/// (Annotations-Tags, Namensbildung) gehört bewusst in eine Emitter-Schicht darüber.
/// </summary>
/// <remarks>
/// <para>
/// Der Builder normalisiert das kosmetische Rauschen, das StringTemplate hinterlässt, und
/// reproduziert es nicht:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Trailing-Whitespace wird verworfen.</b> Leerzeichen/Tabs am Zeilenende (vor dem
///     Zeilenumbruch) landen nie in der Ausgabe.
///   </description></item>
///   <item><description>
///     <b>Leerzeilen bleiben leer.</b> Die Einrückung einer Zeile wird erst unmittelbar vor dem
///     ersten geschriebenen Zeichen ausgegeben (lazy); eine Zeile ohne Inhalt bekommt weder
///     Einrückung noch Trailing-Whitespace.
///   </description></item>
///   <item><description>
///     <b>Zeilenenden werden auf den konfigurierten Zeilenumbruch vereinheitlicht</b>
///     (Standard <see cref="DefaultNewLine"/>, CRLF).
///     Eingebettete <c>\r\n</c>, <c>\n</c> werden als Zeilenumbruch interpretiert.
///   </description></item>
/// </list>
/// <para>
/// Die <i>sichtbare</i> Formatierung — insbesondere die per <see cref="Align()"/> erreichte
/// Spaltenausrichtung umbrochener Parameterlisten — bleibt dagegen exakt steuerbar.
/// </para>
/// </remarks>
public sealed class CodeBuilder {

    // Vorab dimensioniert: eine generierte Artefakt-Datei ist typisch mehrere KB groß. Ohne
    // Startkapazität wächst der StringBuilder aus der Default-16 zeichenweise mit wiederholtem
    // Puffer-Neukopieren (Buffer.BulkMoveWithWriteBarrier) hoch; 8 KiB deckt die meisten Dateien
    // ohne Wachstum ab und ist pro Datei (sequentiell erzeugt, danach GC-frei) vernachlässigbar.
    readonly StringBuilder _sb        = new(8192);
    readonly StringBuilder _pendingWs = new();
    readonly Stack<int>    _anchors   = new();

    readonly string _indentUnit;
    readonly string _newLine;

    int  _indentDepth;
    int  _emittedLineLength;
    bool _atLineStart = true;

    /// <summary>
    /// Der Standard-Zeilenumbruch: CRLF, <b>bewusst plattformunabhängig fest</b> — <b>nicht</b>
    /// <see cref="Environment.NewLine"/>. Der generierte Code ist ein Artefakt mit stabilem
    /// Byte-Bild (Regression-Snapshots, eingecheckte Konsumenten-Dateien sind CRLF); ein
    /// system-abhängiger Zeilenumbruch würde je Host abweichende Bytes erzeugen.
    /// </summary>
    public const string DefaultNewLine = "\r\n";

    /// <summary>
    /// Erzeugt einen Builder.
    /// </summary>
    /// <param name="indentUnit">Die Zeichenfolge einer Einrück-Stufe. Standard sind vier Leerzeichen.</param>
    /// <param name="newLine">
    /// Der verwendete Zeilenumbruch. Standard ist <see cref="DefaultNewLine"/> (CRLF) — siehe dort,
    /// warum das absichtlich nicht der System-Zeilenumbruch ist.
    /// </param>
    public CodeBuilder(string indentUnit = "    ", string newLine = DefaultNewLine) {
        _indentUnit = indentUnit ?? throw new ArgumentNullException(nameof(indentUnit));
        _newLine    = newLine    ?? throw new ArgumentNullException(nameof(newLine));
    }

    /// <summary>Die aktuelle Einrück-Stufe (0 = keine Einrückung).</summary>
    public int IndentDepth => _indentDepth;

    /// <summary>
    /// Der Zeilenumbruch, mit dem der Builder Zeilen abschließt (Standard <see cref="DefaultNewLine"/>).
    /// Gedacht als newline-haltiger Separator für <see cref="WriteJoin{T}(IEnumerable{T}, Action{T}, string)"/>,
    /// damit im aufrufenden Emitter-Code kein literales <c>"\r\n"</c> mehr steht.
    /// </summary>
    public string NewLine => _newLine;

    /// <summary>
    /// Die aktuelle, 0-basierte Cursor-Spalte auf der laufenden Zeile — inklusive der Einrückung,
    /// die für diese Zeile noch aussteht. Grundlage für <see cref="Align()"/>.
    /// </summary>
    public int Column => (_atLineStart ? CurrentPadding().Length : _emittedLineLength) + _pendingWs.Length;

    /// <summary>Die Länge des bislang festgeschriebenen Inhalts (ohne ausstehende Einrückung/Trailing-Whitespace).</summary>
    public int Length => _sb.Length;

    /// <summary>Schreibt <paramref name="text"/>. <c>null</c> und Leerstring sind ein No-op.</summary>
    public CodeBuilder Write(string? text) {

        if (String.IsNullOrEmpty(text)) {
            return this;
        }

        foreach (var c in text!) {
            AppendChar(c);
        }

        return this;
    }

    /// <summary>Schreibt ein einzelnes Zeichen (Zeilenumbruch-Zeichen werden wie ein Umbruch behandelt).</summary>
    public CodeBuilder Write(char value) {
        AppendChar(value);
        return this;
    }

    /// <summary>Beginnt eine neue Zeile. Ohne vorangehenden Inhalt entsteht eine leere Zeile (kein Einzug).</summary>
    public CodeBuilder WriteLine() {
        NewLineCore();
        return this;
    }

    /// <summary>Schreibt <paramref name="text"/> und beginnt anschließend eine neue Zeile.</summary>
    public CodeBuilder WriteLine(string? text) {
        Write(text);
        NewLineCore();
        return this;
    }

    /// <summary>
    /// Erhöht die Einrückung um eine Stufe und liefert einen Scope, der sie beim
    /// <see cref="IDisposable.Dispose"/> wieder zurücknimmt (<c>using (builder.Indent()) { … }</c>).
    /// </summary>
    public IDisposable Indent() {
        PushIndent();
        return new Scope(() => PopIndent());
    }

    /// <summary>Erhöht die Einrückung um eine Stufe (explizite Alternative zum <see cref="Indent()"/>-Scope).</summary>
    public CodeBuilder PushIndent() {
        _indentDepth++;
        return this;
    }

    /// <summary>Verringert die Einrückung um eine Stufe.</summary>
    public CodeBuilder PopIndent() {
        if (_indentDepth == 0) {
            throw new InvalidOperationException("Die Einrückung ist bereits auf Stufe 0.");
        }

        _indentDepth--;
        return this;
    }

    /// <summary>
    /// Öffnet einen Block: schreibt <paramref name="opening"/> an das Ende der aktuellen Zeile,
    /// beginnt eine neue Zeile und erhöht die Einrückung. Beim <see cref="IDisposable.Dispose"/>
    /// wird die Einrückung zurückgenommen und <paramref name="closing"/> auf einer eigenen Zeile
    /// geschrieben (ohne abschließenden Zeilenumbruch — den setzt der umgebende Kontext).
    /// </summary>
    /// <remarks>
    /// Der Block-Körper sollte zeilenweise abgeschlossen sein; endet er ohne Zeilenumbruch, fügt
    /// der Scope vor <paramref name="closing"/> einen ein.
    /// </remarks>
    public IDisposable Block(string opening = "{", string closing = "}") {
        Write(opening);
        NewLineCore();
        PushIndent();

        return new Scope(() => {
            PopIndent();
            EnsureLineStart();
            Write(closing);
        });
    }

    /// <summary>
    /// Verankert die Ausrichtung an der aktuellen <see cref="Column"/>: solange der Scope offen ist,
    /// werden Folgezeilen bis zu dieser Spalte aufgefüllt statt anhand der Einrück-Stufe. So richten
    /// sich etwa umbrochene Parameterlisten am ersten Parameter aus. Anker sind schachtelbar; der
    /// innerste gilt.
    /// </summary>
    /// <example>
    /// Eine umbrochene Parameterliste, ausgerichtet an der öffnenden Klammer:
    /// <code>
    /// cb.Write("void Do(");
    /// using (cb.Align()) {
    ///     cb.WriteJoin(["int first", "string second"], p => cb.Write(p), separator: $",{cb.NewLine}");
    /// }
    /// cb.Write(");");
    /// // void Do(int first,
    /// //         string second);
    /// </code>
    /// Für genau diesen Ablauf (Anker öffnen, joinen, schließen) gibt es die Kurzform
    /// <see cref="WriteAlignedJoin{T}(IEnumerable{T}, Action{T}, string)"/>.
    /// </example>
    public IDisposable Align() {
        return Align(Column);
    }

    /// <summary>
    /// Wie <see cref="Align()"/>, jedoch mit einer explizit vorgegebenen Ausrichtungs-<paramref name="column"/>.
    /// </summary>
    public IDisposable Align(int column) {
        if (column < 0) {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        _anchors.Push(column);
        return new Scope(() => _anchors.Pop());
    }

    /// <summary>
    /// Schreibt <paramref name="items"/> und fügt zwischen je zwei Elementen <paramref name="separator"/>
    /// ein. <paramref name="writeItem"/> schreibt je Element über den bereits im Scope liegenden Builder
    /// (<c>x =&gt; cb.Write(x)</c>); der Builder wird bewusst nicht zusätzlich als Delegat-Parameter
    /// durchgereicht — das spart am Aufrufort einen zweiten Namen für ihn. Enthält der Separator einen
    /// Zeilenumbruch (typisch <c>$",{cb.NewLine}"</c>), beginnt je Element eine neue Zeile; für die
    /// Folgezeilen greift die aktuelle Einrückung bzw. der aktive <see cref="Align()"/>-Anker. Für den
    /// umbrochen-ausgerichteten Fall gibt es die Kurzform
    /// <see cref="WriteAlignedJoin{T}(IEnumerable{T}, Action{T}, string)"/>.
    /// </summary>
    /// <example>
    /// Eine komma-getrennte Liste in einer Zeile:
    /// <code>
    /// cb.WriteJoin(["a", "b", "c"], x => cb.Write(x), separator: ", ");
    /// // a, b, c
    /// </code>
    /// </example>
    public CodeBuilder WriteJoin<T>(IEnumerable<T> items, Action<T> writeItem, string separator) {

        if (items     == null) throw new ArgumentNullException(nameof(items));
        if (writeItem == null) throw new ArgumentNullException(nameof(writeItem));

        var first = true;
        foreach (var item in items) {
            if (!first) {
                Write(separator);
            }

            writeItem(item);
            first = false;
        }

        return this;
    }

    /// <summary>
    /// Kurzform für den häufigen Fall „umbrochene, ausgerichtete Liste": öffnet an der aktuellen
    /// <see cref="Column"/> einen <see cref="Align()"/>-Anker und schreibt darin <paramref name="items"/>,
    /// getrennt durch <paramref name="separator"/>. <paramref name="writeItem"/> schreibt je Element über
    /// den bereits im Scope liegenden Builder (<c>p =&gt; cb.Write(p)</c>) — ohne einen zweiten Namen für
    /// ihn. Enthält <paramref name="separator"/> einen Zeilenumbruch (typisch <c>$",{cb.NewLine}"</c>),
    /// richten sich die Folgezeilen an der Spalte des ersten Elements aus — der Aufrufer spart den
    /// expliziten <c>using (cb.Align()) { … }</c>-Rahmen.
    /// </summary>
    /// <example>
    /// <code>
    /// cb.Write("void Do(");
    /// cb.WriteAlignedJoin(["int first", "string second"], p => cb.Write(p), separator: $",{cb.NewLine}");
    /// cb.Write(");");
    /// // void Do(int first,
    /// //         string second);
    /// </code>
    /// </example>
    public CodeBuilder WriteAlignedJoin<T>(IEnumerable<T> items, Action<T> writeItem, string separator) {
        using (Align()) {
            return WriteJoin(items, writeItem, separator);
        }
    }

    /// <summary>Liefert den erzeugten Code. Ausstehender Trailing-Whitespace/Einzug ist nicht enthalten.</summary>
    public override string ToString() {
        return _sb.ToString();
    }

    // -- intern --------------------------------------------------------------------------------------

    void AppendChar(char c) {

        // CR wird verschluckt; der Umbruch läuft über LF — so werden CRLF, LF (und ein einzelnes CR
        // am Zeilenende) einheitlich zu NewLine.
        switch (c) {
            case '\r':
                return;
            case '\n':
                NewLineCore();
                return;
            case ' ':
            case '\t':
                _pendingWs.Append(c);
                return;
        }

        if (_atLineStart) {
            var padding = CurrentPadding();
            _sb.Append(padding);
            _emittedLineLength += padding.Length;
            _atLineStart       =  false;
        }

        if (_pendingWs.Length > 0) {
            _sb.Append(_pendingWs);
            _emittedLineLength += _pendingWs.Length;
            _pendingWs.Clear();
        }

        _sb.Append(c);
        _emittedLineLength++;
    }

    void NewLineCore() {
        _pendingWs.Clear(); // Trailing-Whitespace verwerfen
        _sb.Append(_newLine);
        _atLineStart       = true;
        _emittedLineLength = 0;
    }

    void EnsureLineStart() {
        if (!_atLineStart) {
            NewLineCore();
        }
    }

    string CurrentPadding() {

        if (_anchors.Count > 0) {
            return new string(' ', _anchors.Peek());
        }

        if (_indentDepth <= 0) {
            return String.Empty;
        }

        var sb = new StringBuilder(_indentUnit.Length * _indentDepth);
        for (var i = 0; i < _indentDepth; i++) {
            sb.Append(_indentUnit);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Die <see cref="IDisposable"/>-Klammer, die <see cref="Indent()"/>, <see cref="Block(string, string)"/>
    /// und <see cref="Align()"/> zurückgeben: führt die übergebene Rücknahme-Aktion beim <see cref="Dispose"/>
    /// genau einmal aus (mehrfaches <c>Dispose</c> ist ein No-op).
    /// </summary>
    sealed class Scope: IDisposable {

        Action? _onDispose;

        public Scope(Action onDispose) {
            _onDispose = onDispose;
        }

        public void Dispose() {
            var onDispose = _onDispose;
            _onDispose = null;
            onDispose?.Invoke();
        }

    }

}