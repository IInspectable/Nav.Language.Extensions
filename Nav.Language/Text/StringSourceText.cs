#region Using Directives

using System;
using System.IO;
using System.Threading;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// String-basierte Implementierung von <see cref="SourceText"/>. Der Zeilenindex (die Start-Offsets
/// aller Zeilen) wird verzögert und thread-sicher (<see cref="LazyThreadSafetyMode.PublicationOnly"/>)
/// beim ersten Zugriff aus dem Text berechnet.
/// </summary>
sealed class StringSourceText: SourceText {

    readonly string                    _text;
    readonly Lazy<ImmutableArray<int>> _textLines;

    public StringSourceText(string? text, string? filePath) {

        _text      = text ?? String.Empty;
        _textLines = new Lazy<ImmutableArray<int>>(() => _text.AsSpan().ParseLineStarts(), LazyThreadSafetyMode.PublicationOnly);
        FileInfo   = String.IsNullOrEmpty(filePath) ? null : new FileInfo(filePath);
        TextLines  = new StringTextLineList(this);
    }

    public override FileInfo?          FileInfo  { get; }
    public override SourceTextLineList TextLines { get; }
    public override string             Text      => _text;
    public override int                Length    => _text.Length;
    public override ReadOnlySpan<char> Span      => _text.AsSpan();

    // Bewusst direkt über den string-Indexer statt über Span/Memory: der Indexer wird zeichenweise
    // in Schleifen aufgerufen (z.B. Formatter), und jeder Memory→Span-Übergang kostete dort pro
    // Zeichen einen Typ-Check.
    public override char this[int index] => _text[index];

    /// <summary>
    /// Baut die <see cref="SourceTextLine"/> für die angegebene Zeilennummer. Der Zeilenanfang ist der
    /// Start-Offset aus <paramref name="lineStarts"/>; das Zeilenende ist der Start der Folgezeile bzw.
    /// <see cref="SourceText.Length"/> für die letzte Zeile (Ende exklusiv, inklusive Zeilenumbruch).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="line"/> liegt außerhalb von <c>[0, lineStarts.Length)</c>.
    /// </exception>
    SourceTextLine GetTextLine(int line, ImmutableArray<int> lineStarts) {

        if (line < 0 || line >= lineStarts.Length) {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        int start = lineStarts[line];
        int end   = line == lineStarts.Length - 1 ? Length : lineStarts[line + 1];

        return new SourceTextLine(this, line: line, lineStart: start, lineEnd: end);
    }

    /// <summary>
    /// <see cref="SourceTextLineList"/> über den verzögert berechneten Zeilenindex des <see cref="StringSourceText"/>.
    /// </summary>
    sealed class StringTextLineList: SourceTextLineList {

        private readonly StringSourceText _sourceText;

        public StringTextLineList(StringSourceText sourceText) {
            _sourceText = sourceText;

        }

        public override int Count => _sourceText._textLines.Value.Length;

        public override SourceTextLine this[int index] => _sourceText.GetTextLine(index, _sourceText._textLines.Value);

    }

}
