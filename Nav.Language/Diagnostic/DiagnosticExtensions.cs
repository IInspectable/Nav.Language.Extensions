#region Using Directives

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Erweiterungsmethoden zum Filtern und Prüfen von <see cref="Diagnostic"/>-Folgen nach
/// <see cref="DiagnosticSeverity"/>.
/// </summary>
public static class DiagnosticExtensions {

    /// <summary>
    /// Gibt an, ob <paramref name="source"/> mindestens eine Diagnose mit Schweregrad
    /// <see cref="DiagnosticSeverity.Error"/> enthält.
    /// </summary>
    /// <param name="source">Die zu prüfende Diagnose-Folge.</param>
    public static bool HasErrors(this IEnumerable<Diagnostic> source) {
        return source.Errors().Any();
    }

    /// <summary>
    /// Liefert die Diagnosen aus <paramref name="source"/> mit Schweregrad
    /// <see cref="DiagnosticSeverity.Warning"/>.
    /// </summary>
    /// <param name="source">Die zu filternde Diagnose-Folge.</param>
    public static IEnumerable<Diagnostic> Warnings(this IEnumerable<Diagnostic> source) {
        return source.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Warning);
    }

    /// <summary>
    /// Liefert die Diagnosen aus <paramref name="source"/> mit Schweregrad
    /// <see cref="DiagnosticSeverity.Error"/>.
    /// </summary>
    /// <param name="source">Die zu filternde Diagnose-Folge.</param>
    public static IEnumerable<Diagnostic> Errors(this IEnumerable<Diagnostic> source) {
        return source.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>
    /// Liefert die Diagnosen aus <paramref name="source"/> mit Schweregrad
    /// <see cref="DiagnosticSeverity.Suggestion"/>.
    /// </summary>
    /// <param name="source">Die zu filternde Diagnose-Folge.</param>
    public static IEnumerable<Diagnostic> Suggestions(this IEnumerable<Diagnostic> source) {
        return source.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Suggestion);
    }

}