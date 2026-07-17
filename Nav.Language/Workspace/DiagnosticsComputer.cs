#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Liefert die relevanten Nav-Diagnostics eines Dokuments aus einem bereits gebauten semantischen Modell:
/// Syntaxfehler (Lexer/Parser) plus die semantischen Analyzer-Diagnostics, gefiltert auf das betreffende
/// Dokument und stabil sortiert. VS-/LSP-frei — gemeinsam genutzt von LSP- und MCP-Server-Host.
/// </summary>
public static class DiagnosticsComputer {

    /// <summary>
    /// Sammelt die Diagnostics für <paramref name="filePath"/> aus <paramref name="unit"/>: Syntaxfehler des
    /// Syntaxbaums plus die semantischen Analyzer-Diagnostics, jeweils via <c>ExpandLocations</c> auf ihre
    /// Einzel-Orte aufgefächert, auf das betreffende Dokument gefiltert (siehe <see cref="BelongsToDocument"/>)
    /// und nach Start-Position stabil sortiert.
    /// </summary>
    public static IReadOnlyList<Diagnostic> FromUnit(CodeGenerationUnit unit, string filePath) {

        var diagnostics = unit.Syntax.SyntaxTree.Diagnostics.Concat(unit.Diagnostics);

        var normalizedPath = PathHelper.NormalizePath(filePath);

        return diagnostics
              .SelectMany(diagnostic => diagnostic.ExpandLocations())
              .Where(diagnostic => BelongsToDocument(diagnostic, normalizedPath))
              .OrderBy(diagnostic => diagnostic.Location.Start)
              .ToList();
    }

    /// <summary>
    /// <c>true</c>, wenn eine Diagnose zum Dokument <paramref name="normalizedPath"/> gehört. Diagnostics ohne
    /// eigenen Pfad stammen aus dem aktuell geparsten Dokument und zählen immer dazu; sonst wird der
    /// normalisierte Pfad case-insensitiv verglichen.
    /// </summary>
    static bool BelongsToDocument(Diagnostic diagnostic, string? normalizedPath) {

        var locationPath = diagnostic.Location.NormalizedFilePath;

        // Diagnostics ohne Pfad stammen aus dem aktuell geparsten Dokument.
        if (string.IsNullOrEmpty(locationPath)) {
            return true;
        }

        return string.Equals(locationPath, normalizedPath, StringComparison.OrdinalIgnoreCase);
    }
}
