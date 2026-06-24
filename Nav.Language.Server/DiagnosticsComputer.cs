#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Server;

/// <summary>
/// Berechnet die Nav-Diagnostics für ein einzelnes Dokument: Syntaxfehler (Lexer/Parser) plus die
/// semantischen Analyzer-Diagnostics aus dem <see cref="CodeGenerationUnit"/>. Cross-File-Includes
/// werden über den disk-basierten <see cref="SyntaxProvider"/> aufgelöst (Overlay-/Workspace-Modell
/// folgt in einem späteren Milestone).
/// </summary>
static class DiagnosticsComputer {

    /// <summary>
    /// Diagnostics aus dem in-memory-Text eines Dokuments (z.B. ein offenes Dokument).
    /// </summary>
    public static IReadOnlyList<Diagnostic> Compute(string? filePath, string text, CancellationToken cancellationToken) {

        var syntaxTree = SyntaxTree.ParseText(text, filePath, cancellationToken);

        IEnumerable<Diagnostic> diagnostics = syntaxTree.Diagnostics;

        if (syntaxTree.Root is CodeGenerationUnitSyntax codeGenerationUnitSyntax) {
            var unit = SemanticModelProvider.Default.GetSemanticModel(codeGenerationUnitSyntax, cancellationToken);
            if (unit != null) {
                diagnostics = diagnostics.Concat(unit.Diagnostics);
            }
        }

        return Filter(diagnostics, filePath);
    }

    /// <summary>
    /// Diagnostics aus einem bereits gebauten semantischen Modell (z.B. beim Workspace-Scan von Platte).
    /// </summary>
    public static IReadOnlyList<Diagnostic> FromUnit(CodeGenerationUnit unit, string? filePath) {

        var diagnostics = unit.Syntax.SyntaxTree.Diagnostics.Concat(unit.Diagnostics);

        return Filter(diagnostics, filePath);
    }

    static IReadOnlyList<Diagnostic> Filter(IEnumerable<Diagnostic> diagnostics, string? filePath) {

        var normalizedPath = PathHelper.NormalizePath(filePath);

        return diagnostics
              .SelectMany(diagnostic => diagnostic.ExpandLocations())
              .Where(diagnostic => BelongsToDocument(diagnostic, normalizedPath))
              .OrderBy(diagnostic => diagnostic.Location.Start)
              .ToList();
    }

    static bool BelongsToDocument(Diagnostic diagnostic, string? normalizedPath) {

        var locationPath = diagnostic.Location.NormalizedFilePath;

        // Diagnostics ohne Pfad stammen aus dem aktuell geparsten Dokument.
        if (string.IsNullOrEmpty(locationPath)) {
            return true;
        }

        return string.Equals(locationPath, normalizedPath, StringComparison.OrdinalIgnoreCase);
    }
}
