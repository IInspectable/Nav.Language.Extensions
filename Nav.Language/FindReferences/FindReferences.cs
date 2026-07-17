#region Using Directives

using System;
using System.Threading.Tasks;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

/// <summary>
/// Einstiegspunkt der Referenzsuche — der VS-freie Feature-Kern, den die Hosts (VS-Extension, LSP,
/// MCP) gemeinsam nutzen. Startet die Suche für die in <see cref="FindReferencesArgs"/> beschriebene
/// Anfrage; die Ergebnisse laufen über deren <see cref="FindReferencesArgs.Context"/> zurück.
/// </summary>
public class ReferenceFinder {

    /// <summary>
    /// Führt die Referenzsuche zur Anfrage <paramref name="args"/> asynchron aus.
    /// </summary>
    /// <param name="args">Die Suchanfrage.</param>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> ist <c>null</c>.</exception>
    public static Task FindReferencesAsync(FindReferencesArgs args) {

        if (args == null) {
            throw new ArgumentNullException(nameof(args));
        }

        return FindReferencesVisitor.Invoke(args);

    }

}