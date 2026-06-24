#region Using Directives

using System;

using Pharmatechnik.Nav.Language.Text;

using NavDiagnostic = Pharmatechnik.Nav.Language.Diagnostic;
using NavSeverity = Pharmatechnik.Nav.Language.DiagnosticSeverity;

using Lsp = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Server;

/// <summary>
/// Bildet Engine-Diagnostics auf die LSP-Protokoll-DTOs ab. Die Nav-<see cref="NavDiagnostic.Location"/>
/// ist bereits 0-basiert (Zeile/Spalte) und damit deckungsgleich zur LSP-Position.
/// </summary>
static class LspMapper {

    public static Lsp.Diagnostic ToLsp(NavDiagnostic diagnostic) {

        return new Lsp.Diagnostic {
            Range    = ToRange(diagnostic.Location),
            Severity = ToLsp(diagnostic.Severity),
            Code     = diagnostic.Descriptor.Id,
            Source   = "nav",
            Message  = diagnostic.Message
        };
    }

    public static Lsp.Range ToRange(Location location) => new() {
        Start = new Lsp.Position { Line = location.StartLine, Character = location.StartCharacter },
        End   = new Lsp.Position { Line = location.EndLine,   Character = location.EndCharacter }
    };

    /// <summary>
    /// Bildet eine Engine-<see cref="Location"/> auf eine LSP-<see cref="Lsp.Location"/> ab. Der
    /// Ziel-Pfad (auch cross-file) wird zu einer <c>file://</c>-URI; null, wenn die Location keinen
    /// Dateipfad trägt.
    /// </summary>
    public static Lsp.Location? ToLocation(Location location) {

        if (string.IsNullOrEmpty(location?.FilePath)) {
            return null;
        }

        return new Lsp.Location {
            Uri   = new Uri(location.FilePath),
            Range = ToRange(location)
        };
    }

    /// <summary>
    /// Rechnet eine LSP-<see cref="Lsp.Position"/> (Zeile/Zeichen, 0-basiert) in einen 0-basierten
    /// Zeichen-Offset innerhalb des <paramref name="sourceText"/> um. Zeile und Zeichen werden auf
    /// gültige Grenzen geklemmt, damit Anfragen am Dokumentrand nicht werfen.
    /// </summary>
    public static int ToOffset(SourceText sourceText, Lsp.Position position) {

        var lines = sourceText.TextLines;
        if (lines.Count == 0) {
            return 0;
        }

        var lineIndex = Math.Min(Math.Max(position.Line, 0), lines.Count - 1);
        var line      = lines[lineIndex];

        var offset = line.Start + Math.Max(position.Character, 0);

        return Math.Min(offset, line.End);
    }

    static Lsp.DiagnosticSeverity ToLsp(NavSeverity severity) => severity switch {
        NavSeverity.Error      => Lsp.DiagnosticSeverity.Error,
        NavSeverity.Warning    => Lsp.DiagnosticSeverity.Warning,
        NavSeverity.Suggestion => Lsp.DiagnosticSeverity.Information,
        _                      => Lsp.DiagnosticSeverity.Information
    };
}
