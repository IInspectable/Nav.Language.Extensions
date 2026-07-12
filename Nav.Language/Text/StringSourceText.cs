#region Using Directives

using System;
using System.IO;
using System.Threading;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.Text;

sealed class StringSourceText: SourceText {

    readonly string                    _text;
    readonly ReadOnlyMemory<char>      _memory;
    readonly Lazy<ImmutableArray<int>> _textLines;

    public StringSourceText(string? text, string? filePath) {

        _text      = text ?? String.Empty;
        _memory    = _text.AsMemory();
        _textLines = new Lazy<ImmutableArray<int>>(() => _memory.Span.ParseLineStarts(), LazyThreadSafetyMode.PublicationOnly);
        FileInfo   = String.IsNullOrEmpty(filePath) ? null : new FileInfo(filePath);
        TextLines  = new StringTextLineList(this);
    }

    public override FileInfo?          FileInfo  { get; }
    public override SourceTextLineList TextLines { get; }
    public override string             Text      => _text;
    public override int                Length    => _memory.Length;
    public override ReadOnlySpan<char> Span      => _memory.Span;

    public override char this[int index] => _memory.Span[index];

    SourceTextLine GetTextLine(int line, ImmutableArray<int> lineStarts) {

        if (line < 0 || line >= lineStarts.Length) {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        int start = lineStarts[line];
        int end   = line == lineStarts.Length - 1 ? Length : lineStarts[line + 1];

        return new SourceTextLine(this, line: line, lineStart: start, lineEnd: end);
    }

    sealed class StringTextLineList: SourceTextLineList {

        private readonly StringSourceText _sourceText;

        public StringTextLineList(StringSourceText sourceText) {
            _sourceText = sourceText;

        }

        public override int Count => _sourceText._textLines.Value.Length;

        public override SourceTextLine this[int index] => _sourceText.GetTextLine(index, _sourceText._textLines.Value);

    }

}