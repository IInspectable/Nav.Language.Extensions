#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;

using NavDiagnostic = Pharmatechnik.Nav.Language.Diagnostic;
using NavSeverity = Pharmatechnik.Nav.Language.DiagnosticSeverity;

using Protocol = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Lsp;

/// <summary>
/// Bildet Engine-Diagnostics auf die LSP-Protokoll-DTOs ab. Die Nav-<see cref="NavDiagnostic.Location"/>
/// ist bereits 0-basiert (Zeile/Spalte) und damit deckungsgleich zur LSP-Position.
/// </summary>
static class LspMapper {

    public static Protocol.Diagnostic ToLsp(NavDiagnostic diagnostic) {

        return new Protocol.Diagnostic {
            Range    = ToRange(diagnostic.Location),
            Severity = ToLsp(diagnostic.Severity),
            Code     = diagnostic.Descriptor.Id,
            Source   = "nav",
            Message  = diagnostic.Message
        };
    }

    public static Protocol.Range ToRange(Location location) => new() {
        Start = new Protocol.Position { Line = location.StartLine, Character = location.StartCharacter },
        End   = new Protocol.Position { Line = location.EndLine,   Character = location.EndCharacter }
    };

    /// <summary>
    /// Bildet eine Engine-<see cref="Location"/> auf eine LSP-<see cref="Protocol.Location"/> ab. Der
    /// Ziel-Pfad (auch cross-file) wird zu einer <c>file://</c>-URI; null, wenn die Location keinen
    /// Dateipfad trägt.
    /// </summary>
    public static Protocol.Location? ToLocation(Location location) {

        if (string.IsNullOrEmpty(location.FilePath)) {
            return null;
        }

        return new Protocol.Location {
            Uri   = new Uri(location.FilePath),
            Range = ToRange(location)
        };
    }

    /// <summary>
    /// Rechnet eine LSP-<see cref="Protocol.Position"/> (Zeile/Zeichen, 0-basiert) in einen 0-basierten
    /// Zeichen-Offset innerhalb des <paramref name="sourceText"/> um. Zeile und Zeichen werden auf
    /// gültige Grenzen geklemmt, damit Anfragen am Dokumentrand nicht werfen.
    /// </summary>
    public static int ToOffset(SourceText sourceText, Protocol.Position position) {

        var lines = sourceText.TextLines;
        if (lines.Count == 0) {
            return 0;
        }

        var lineIndex = Math.Min(Math.Max(position.Line, 0), lines.Count - 1);
        var line      = lines[lineIndex];

        var offset = line.Start + Math.Max(position.Character, 0);

        return Math.Min(offset, line.End);
    }

    static Protocol.DiagnosticSeverity ToLsp(NavSeverity severity) => severity switch {
        NavSeverity.Error      => Protocol.DiagnosticSeverity.Error,
        NavSeverity.Warning    => Protocol.DiagnosticSeverity.Warning,
        NavSeverity.Suggestion => Protocol.DiagnosticSeverity.Information,
        _                      => Protocol.DiagnosticSeverity.Information
    };
}
