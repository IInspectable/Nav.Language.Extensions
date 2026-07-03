#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Hält die Include-Abhängigkeiten zwischen Nav-Dateien als Vorwärtskanten
/// (Inkludierer → inkludierte Dateien, alle Schlüssel via <see cref="PathHelper.NormalizePath"/> normalisiert).
/// Dient der dependency-aware Re-Diagnose: Ändert sich eine Datei, müssen auch die (transitiv) inkludierenden
/// Dateien neu diagnostiziert werden, weil sich deren Cross-File-Diagnostics ändern können.
/// Threadsicher (<see cref="ConcurrentDictionary{TKey,TValue}"/>), damit der Graph auch beim parallelen
/// Solution-Scan befüllt werden kann.
/// </summary>
public sealed class IncludeDependencyGraph {

    // Normalisierter Inkludierer-Pfad -> Menge der (normalisiert) inkludierten Dateien.
    readonly ConcurrentDictionary<string, ImmutableHashSet<string>> _includes = new();

    /// <summary>
    /// Setzt die Vorwärtskanten einer Datei (ersetzt deren bisherige Include-Menge vollständig). Pfade werden
    /// intern normalisiert; nicht auflösbare Pfade werden übersprungen.
    /// </summary>
    public void SetIncludes(string filePath, IEnumerable<string>? includedFiles) {

        var key = PathHelper.NormalizePath(filePath);
        if (key == null) {
            return;
        }

        var builder = ImmutableHashSet.CreateBuilder(StringComparer.OrdinalIgnoreCase);
        if (includedFiles != null) {
            foreach (var included in includedFiles) {

                var includedKey = PathHelper.NormalizePath(included);
                // Selbst-Include ignorieren (kann durch fehlerhafte taskref-Pfade entstehen) — würde sonst nur
                // den Reverse-BFS unnötig belasten.
                if (includedKey != null && !string.Equals(includedKey, key, StringComparison.OrdinalIgnoreCase)) {
                    builder.Add(includedKey);
                }
            }
        }

        _includes[key] = builder.ToImmutable();
    }

    /// <summary>
    /// Entfernt die Vorwärtskanten einer Datei (z.B. wenn die Datei gelöscht wurde). Kanten ANDERER Dateien,
    /// die auf <paramref name="filePath"/> zeigen, bleiben erhalten — sie werden aufgefrischt, sobald die
    /// jeweils inkludierende Datei neu berechnet wird.
    /// </summary>
    public void Remove(string filePath) {

        var key = PathHelper.NormalizePath(filePath);
        if (key != null) {
            _includes.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Liefert alle Dateien, die <paramref name="filePath"/> transitiv inkludieren (ohne die Datei selbst).
    /// Reverse-BFS über eine Momentaufnahme der Vorwärtskanten; Zyklen sind über das visited-Set abgefangen.
    /// </summary>
    public IReadOnlyCollection<string> GetDependentsClosure(string filePath) {

        var start = PathHelper.NormalizePath(filePath);
        if (start == null) {
            return Array.Empty<string>();
        }

        // Reverse-Adjazenz aus dem aktuellen Snapshot aufbauen: inkludierte Datei -> Menge ihrer Inkludierer.
        var reverse = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _includes) {

            var includer = entry.Key;
            foreach (var included in entry.Value) {
                if (!reverse.TryGetValue(included, out var includers)) {
                    includers          = new List<string>();
                    reverse[included] = includers;
                }

                includers.Add(includer);
            }
        }

        var dependents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue      = new Queue<string>();
        queue.Enqueue(start);

        while (queue.Count > 0) {

            var current = queue.Dequeue();
            if (!reverse.TryGetValue(current, out var includers)) {
                continue;
            }

            foreach (var includer in includers) {
                // start nie aufnehmen; jeden Inkludierer nur einmal verfolgen (Zyklenschutz).
                if (!string.Equals(includer, start, StringComparison.OrdinalIgnoreCase) && dependents.Add(includer)) {
                    queue.Enqueue(includer);
                }
            }
        }

        return dependents;
    }
}
