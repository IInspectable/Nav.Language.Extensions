using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Shared;

/// <summary>
/// Schlanker, fluenter Emitter für generierten C#-Quelltext mit Einrückungsverwaltung. Gemeinsame
/// Basis der Quellgeneratoren.
/// </summary>
public sealed class SourceBuilder {

    readonly List<StringBuilder> _lines = new() { new StringBuilder() };
    readonly string              _indentUnit;
    readonly bool                _sameLineBrace;
    bool                         _isStartOfLine = true;

    /// <summary>
    /// Initialisiert eine neue Instanz von <see cref="SourceBuilder"/>.
    /// </summary>
    /// <param name="useTabs">Gibt an, ob Tabs für Einrückungen verwendet werden sollen.</param>
    /// <param name="spacesPerIndent">Die Anzahl der Leerzeichen pro Einrückungsebene (falls <paramref name="useTabs"/> false ist).</param>
    /// <param name="sameLineBrace">Gibt an, ob öffnende Klammern in derselben Zeile wie der Header stehen sollen (K&amp;R Style).</param>
    public SourceBuilder(bool useTabs = false, int spacesPerIndent = 4, bool sameLineBrace = true) {
        if (!useTabs && spacesPerIndent <= 0) {
            throw new ArgumentOutOfRangeException(nameof(spacesPerIndent));
        }

        _indentUnit = useTabs
            ? "\t"
            : new string(' ', spacesPerIndent);

        _sameLineBrace = sameLineBrace;
    }

    public int IndentLevel { get; private set; }

    public IDisposable Indent() {
        IndentLevel++;
        return new IndentScope(this);
    }

    public void PushIndent() {
        IndentLevel++;
    }

    public void PopIndent() {
        if (IndentLevel == 0) {
            throw new InvalidOperationException("Indent level cannot be negative.");
        }

        IndentLevel--;
    }

    public SourceBuilder Append(string text) {
        if (string.IsNullOrEmpty(text)) {
            return this;
        }

        var parts = SplitPreservingNewLines(text);

        foreach (var part in parts) {
            if (part == "\r" || part == "\n" || part == "\r\n") {
                AppendLine();
                continue;
            }

            if (_isStartOfLine) {
                WriteIndent();
            }

            _lines[_lines.Count - 1].Append(part);
        }

        return this;
    }

    public SourceBuilder AppendLine() {
        _lines.Add(new StringBuilder());
        _isStartOfLine = true;
        return this;
    }

    public SourceBuilder AppendLine(string text) {
        Append(text);
        AppendLine();
        return this;
    }

    public SourceBuilder AppendRaw(string text) {
        if (string.IsNullOrEmpty(text)) {
            return this;
        }

        var parts = SplitPreservingNewLines(text);
        foreach (var part in parts) {
            if (part == "\r" || part == "\n" || part == "\r\n") {
                _lines.Add(new StringBuilder());
                _isStartOfLine = true;
                continue;
            }

            _lines[_lines.Count - 1].Append(part);
            _isStartOfLine = false;
        }

        return this;
    }

    public SourceBuilder AppendLines(IEnumerable<string> lines) {
        foreach (var line in lines) {
            AppendLine(line);
        }

        return this;
    }

    /// <summary>
    /// Startet einen Code-Block (z. B. Klasse oder Methode).
    /// </summary>
    /// <param name="header">Der Text vor dem Block (z. B. "public class Foo").</param>
    /// <param name="content">Die Aktion zum Befüllen des Blockinhalts.</param>
    /// <param name="suffix">Optionaler Suffix nach der Klammer (z. B. ein Semikolon).</param>
    public SourceBuilder Block(string header, Action<SourceBuilder> content, string suffix = "") {
        if (_sameLineBrace) {
            Append(header);
            OpenBlock();
        } else {
            AppendLine(header);
            OpenBlock();
        }

        content(this);
        CloseBlock(suffix);
        return this;
    }

    /// <summary>
    /// Öffnet eine geschweifte Klammer. Berücksichtigt <c>sameLineBrace</c>.
    /// </summary>
    public SourceBuilder OpenBlock() {
        if (_sameLineBrace) {
            var currentLine = _lines[_lines.Count - 1];

            // Wenn die aktuelle Zeile leer/nur Indent ist (isStartOfLine),
            // schauen wir, ob die vorherige Zeile existiert.
            if (_isStartOfLine && _lines.Count > 1) {
                var previousLine = _lines[_lines.Count - 2];
                // Wir hängen es an die vorherige Zeile an, wenn diese nicht leer ist.
                if (previousLine.Length > 0) {
                    if (previousLine[previousLine.Length - 1] != ' ') {
                        previousLine.Append(' ');
                    }

                    previousLine.Append('{');
                    PushIndent();
                    return this;
                }
            }

            // Fallback: Falls wir doch am Anfang einer neuen Zeile sind oder
            // die aktuelle Zeile Text hat.
            if (!_isStartOfLine && currentLine.Length > 0 && currentLine[currentLine.Length - 1] != ' ') {
                currentLine.Append(' ');
            }

            currentLine.Append('{');
            AppendLine();
        } else {
            AppendLine("{");
        }

        PushIndent();
        return this;
    }

    /// <summary>
    /// Schließt eine geschweifte Klammer und verringert die Einrückung.
    /// </summary>
    /// <param name="suffix">Optionaler Suffix nach der Klammer (z. B. ein Semikolon).</param>
    public SourceBuilder CloseBlock(string suffix = "") {
        PopIndent();
        AppendLine("}" + suffix);
        return this;
    }

    public override string ToString() {
        var sb = new StringBuilder();
        for (var i = 0; i < _lines.Count; i++) {
            var line = _lines[i].ToString();

            if (i == _lines.Count - 1) {
                if (line.Length > 0) {
                    sb.Append(line);
                }

                break;
            }

            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Fügt einen Datei-Header für generierten Code hinzu.
    /// </summary>
    public SourceBuilder AppendHeader() {
        AppendLine("// <auto-generated />");
        AppendLine("#nullable enable");
        AppendLine();
        return this;
    }

    /// <summary>
    /// Fügt eine Namespace-Deklaration (datei-bezogen) hinzu.
    /// </summary>
    public SourceBuilder Namespace(string namespaceName, Action<SourceBuilder> content) {
        AppendLine("namespace " + namespaceName + ";");
        AppendLine();
        content(this);
        return this;
    }

    void WriteIndent() {
        var currentLine = _lines[_lines.Count - 1];
        for (var i = 0; i < IndentLevel; i++) {
            currentLine.Append(_indentUnit);
        }

        _isStartOfLine = false;
    }

    static List<string> SplitPreservingNewLines(string text) {
        var result = new List<string>();
        var start  = 0;

        for (var i = 0; i < text.Length; i++) {
            if (text[i] == '\r') {
                if (i > start) {
                    result.Add(text.Substring(start, i - start));
                }

                if (i + 1 < text.Length && text[i + 1] == '\n') {
                    result.Add("\r\n");
                    i++;
                } else {
                    result.Add("\r");
                }

                start = i + 1;
            } else if (text[i] == '\n') {
                if (i > start) {
                    result.Add(text.Substring(start, i - start));
                }

                result.Add("\n");
                start = i + 1;
            }
        }

        if (start < text.Length) {
            result.Add(text.Substring(start));
        }

        return result;
    }

    sealed class IndentScope: IDisposable {

        readonly SourceBuilder _owner;
        bool                   _disposed;

        public IndentScope(SourceBuilder owner) {
            _owner = owner;
        }

        public void Dispose() {
            if (_disposed) {
                return;
            }

            _owner.PopIndent();
            _disposed = true;
        }

    }

}
