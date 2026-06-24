#region Using Directives

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

        var location = diagnostic.Location;

        return new Lsp.Diagnostic {
            Range = new Lsp.Range {
                Start = new Lsp.Position { Line = location.StartLine, Character = location.StartCharacter },
                End   = new Lsp.Position { Line = location.EndLine,   Character = location.EndCharacter }
            },
            Severity = ToLsp(diagnostic.Severity),
            Code     = diagnostic.Descriptor.Id,
            Source   = "nav",
            Message  = diagnostic.Message
        };
    }

    static Lsp.DiagnosticSeverity ToLsp(NavSeverity severity) => severity switch {
        NavSeverity.Error      => Lsp.DiagnosticSeverity.Error,
        NavSeverity.Warning    => Lsp.DiagnosticSeverity.Warning,
        NavSeverity.Suggestion => Lsp.DiagnosticSeverity.Information,
        _                      => Lsp.DiagnosticSeverity.Information
    };
}
