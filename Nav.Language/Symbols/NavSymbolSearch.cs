#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

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
    /// <param name="unit">Die zu durchsuchende <see cref="CodeGenerationUnit"/>.</param>
    /// <param name="name">
    /// Der exakt (ordinal) zu treffende Symbolname. Ist er <c>null</c> oder leer, wird eine leere Liste
    /// geliefert.
    /// </param>
    /// <param name="taskScope">
    /// Optionaler Task-Name. Ist er gesetzt, wird ausschließlich innerhalb der Task-Definition(en) dieses
    /// Namens gesucht (deren Knoten + die Task selbst) — zur Disambiguierung mehrdeutiger Knotennamen.
    /// </param>
    /// <returns>
    /// Die passenden Symbole in Suchreihenfolge, dedupliziert über ihre Location; eine leere Liste, wenn
    /// <paramref name="name"/> leer ist oder kein Symbol passt.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="unit"/> ist <c>null</c>.</exception>
    public static IReadOnlyList<ISymbol> FindByName(CodeGenerationUnit unit, string? name, string? taskScope = null) {

        if (unit == null) {
            throw new ArgumentNullException(nameof(unit));
        }

        if (string.IsNullOrEmpty(name)) {
            return Array.Empty<ISymbol>();
        }

        var seen    = new HashSet<(string?, int)>();
        var results = new List<ISymbol>();

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

        void TryAdd(ISymbol? symbol) {
            if (symbol?.Location == null || !string.Equals(symbol.Name, name, StringComparison.Ordinal)) {
                return;
            }

            if (seen.Add((symbol.Location.FilePath, symbol.Location.Start))) {
                results.Add(symbol);
            }
        }
    }

    /// <summary>
    /// Liefert alle <b>Definitions</b>-Symbole in <paramref name="unit"/>, deren Name mit
    /// <paramref name="prefix"/> beginnt (groß-/kleinschreibungsignorierend; leerer Präfix matcht alle).
    /// „Definition" meint die lokalen Task-Definitionen und deren Knoten — NICHT die (auch inkludierten)
    /// <c>taskref</c>-Deklarationen. Grundlage der solution-weiten Symbolsuche (MCP <c>nav_find_symbol</c>):
    /// findet, WO ein Name DEFINIERT ist, ohne die Datei vorher zu kennen. Mehrere Treffer gleichen Namens
    /// (z.B. ein Knotenname in mehreren Tasks) werden alle zurückgegeben; Duplikate an derselben Stelle
    /// (Datei + Startoffset) werden entfernt.
    /// </summary>
    /// <param name="unit">Die zu durchsuchende <see cref="CodeGenerationUnit"/>.</param>
    /// <param name="prefix">
    /// Der groß-/kleinschreibungsignorierend zu treffende Namenspräfix. <c>null</c> wird wie ein leerer
    /// Präfix behandelt und matcht alle Definitionen.
    /// </param>
    /// <returns>
    /// Die passenden Definitions-Symbole (Task-Definitionen und deren Knoten), dedupliziert über ihre
    /// Location; eine leere Liste, wenn kein Symbol passt.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="unit"/> ist <c>null</c>.</exception>
    public static IReadOnlyList<ISymbol> FindDefinitionsByPrefix(CodeGenerationUnit unit, string? prefix) {

        if (unit == null) {
            throw new ArgumentNullException(nameof(unit));
        }

        prefix ??= string.Empty;

        var seen    = new HashSet<(string?, int)>();
        var results = new List<ISymbol>();

        // Nur Definitionen: lokale Task-Definitionen und deren Knoten. Die taskref-Deklarationen
        // (unit.TaskDeclarations) bleiben außen vor — Verwendungsstellen liefert nav_references.
        foreach (var task in unit.TaskDefinitions) {
            TryAdd(task);
            foreach (var node in task.NodeDeclarations) {
                TryAdd(node);
            }
        }

        return results;

        void TryAdd(ISymbol? symbol) {
            if (symbol?.Location == null ||
                !symbol.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            if (seen.Add((symbol.Location.FilePath, symbol.Location.Start))) {
                results.Add(symbol);
            }
        }
    }

}