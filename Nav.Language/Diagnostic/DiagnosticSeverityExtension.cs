using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Erweiterungsmethoden, um den schwersten aus mehreren <see cref="DiagnosticSeverity"/>-Werten zu
/// bestimmen. „Schwerer" bedeutet dabei der höhere zugrunde liegende Zahlenwert der Aufzählung
/// (<see cref="DiagnosticSeverity.Suggestion"/> &lt; <see cref="DiagnosticSeverity.Warning"/> &lt;
/// <see cref="DiagnosticSeverity.Error"/>).
/// </summary>
public static class DiagnosticSeverityExtension {

    /// <summary>
    /// Liefert den schwereren der beiden Schweregrade.
    /// </summary>
    /// <param name="value1">Der erste Schweregrad.</param>
    /// <param name="value2">Der zweite Schweregrad.</param>
    public static DiagnosticSeverity? GetWorst(this DiagnosticSeverity value1, DiagnosticSeverity value2) {
        return (int) value2 > (int) value1 ? value2 : value1;
    }

    /// <summary>
    /// Liefert den schwereren zweier optionaler Schweregrade; ist einer <c>null</c>, wird der andere
    /// zurückgegeben (beide <c>null</c> ergibt <c>null</c>).
    /// </summary>
    /// <param name="value1">Der erste Schweregrad (optional).</param>
    /// <param name="value2">Der zweite Schweregrad (optional).</param>
    public static DiagnosticSeverity? GetWorst(this DiagnosticSeverity? value1, DiagnosticSeverity? value2) {
        if (value1 == null) {
            return value2;
        }

        if (value2 == null) {
            return value1;
        }

        return GetWorst(value1.Value, value2.Value);
    }

    /// <summary>
    /// Liefert den schwersten Schweregrad aus <paramref name="values"/> oder <c>null</c>, wenn die
    /// Folge leer ist. Bricht ab, sobald <see cref="DiagnosticSeverity.Error"/> auftritt, da es keinen
    /// höheren Wert gibt.
    /// </summary>
    /// <param name="values">Die auszuwertenden Schweregrade.</param>
    public static DiagnosticSeverity? GetWorst(this IEnumerable<DiagnosticSeverity> values) {
        DiagnosticSeverity? worst = null;
        foreach (var v in values) {
            // Shortcut: Error
            if (v == DiagnosticSeverity.Error) {
                return DiagnosticSeverity.Error;
            }

            worst = worst.GetWorst(v);
        }

        return worst;
    }

}