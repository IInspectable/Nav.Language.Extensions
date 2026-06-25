#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language.Symbols;

/// <summary>
/// VS-freie Auflösung eines Symbols über seinen <b>Namen</b> innerhalb einer <see cref="CodeGenerationUnit"/>.
/// Brücke für name-basierte Hosts (z.B. der MCP-Server, dessen KI-Agent keinen Cursor hat): liefert zu einem
/// Namen die passenden Symbole, deren <c>Location.Start</c> dann in die bestehenden positions-basierten
/// Engine-Services (GoTo, References, Rename, CodeActions über <c>SymbolPosition.SymbolsAt</c>) eingespeist
/// werden kann — als läge der Caret auf dem Namen. Die Services selbst bleiben unverändert.
/// </summary>
public static class NavSymbolSearch {

    /// <summary>
    /// Liefert alle Symbole in <paramref name="unit"/>, deren Name exakt <paramref name="name"/> ist
    /// (ordinaler Vergleich). Durchsucht — in dieser Reihenfolge — die lokalen Task-Definitionen, die
    /// (auch inkludierten) Task-Deklarationen und die Knoten aller Task-Definitionen. Mehrere Treffer
    /// gleichen Namens (z.B. ein Knotenname in zwei Tasks) werden alle zurückgegeben, damit der Aufrufer
    /// disambiguieren kann; Duplikate an derselben Stelle (Datei + Startoffset) werden entfernt.
    /// </summary>
    /// <param name="taskScope">
    /// Optionaler Task-Name. Ist er gesetzt, wird ausschließlich innerhalb der Task-Definition(en) dieses
    /// Namens gesucht (deren Knoten + die Task selbst) — zur Disambiguierung mehrdeutiger Knotennamen.
    /// </param>
    [NotNull]
    public static IReadOnlyList<ISymbol> FindByName([NotNull] CodeGenerationUnit unit, string name,
                                                    string taskScope = null) {

        if (unit == null) {
            throw new ArgumentNullException(nameof(unit));
        }

        if (string.IsNullOrEmpty(name)) {
            return Array.Empty<ISymbol>();
        }

        var seen    = new HashSet<(string, int)>();
        var results = new List<ISymbol>();

        void TryAdd(ISymbol symbol) {
            if (symbol?.Location == null || !string.Equals(symbol.Name, name, StringComparison.Ordinal)) {
                return;
            }

            if (seen.Add((symbol.Location.FilePath, symbol.Location.Start))) {
                results.Add(symbol);
            }
        }

        if (taskScope != null) {
            foreach (var task in unit.TaskDefinitions.Where(t => string.Equals(t.Name, taskScope, StringComparison.Ordinal))) {
                TryAdd(task);
                foreach (var node in task.NodeDeclarations) {
                    TryAdd(node);
                }
            }

            return results;
        }

        // Tasks (Definitionen lokal, Deklarationen inkl. inkludierter) zuerst — eine lokal definierte Task
        // steht in beiden Sammlungen, die Definition wird dank Dedup über die Location bevorzugt.
        foreach (var task in unit.TaskDefinitions) {
            TryAdd(task);
        }

        foreach (var declaration in unit.TaskDeclarations) {
            TryAdd(declaration);
        }

        // Knoten aller Task-Definitionen.
        foreach (var task in unit.TaskDefinitions) {
            foreach (var node in task.NodeDeclarations) {
                TryAdd(node);
            }
        }

        return results;
    }

}