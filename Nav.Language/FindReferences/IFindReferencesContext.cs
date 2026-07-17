#region Using Directives

using System.Threading;
using System.Threading.Tasks;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

/// <summary>
/// Die Rückmeldungs-Senke einer Referenzsuche — der Host implementiert sie, um Ergebnisse und
/// Fortschritt entgegenzunehmen, während die Suche läuft. Roslyn-Analogon
/// <c>Microsoft.CodeAnalysis.FindUsages.IFindUsagesContext</c>.
/// </summary>
public interface IFindReferencesContext {

    /// <summary>Das Abbruch-Token, mit dem der Host die laufende Suche abbrechen kann.</summary>
    CancellationToken CancellationToken { get; }

    /// <summary>Setzt den Titel der Suche (z.B. für das Ergebnisfenster).</summary>
    /// <param name="title">Der anzuzeigende Titel.</param>
    Task SetSearchTitleAsync(string title);
    /// <summary>Meldet eine allgemeine Statusmeldung.</summary>
    /// <param name="message">Der Meldungstext.</param>
    Task ReportMessageAsync(string message);

    /// <summary>Meldet eine gefundene Definition, unter der Fundstellen gruppiert werden.</summary>
    /// <param name="definitionItem">Die gefundene Definition.</param>
    Task OnDefinitionFoundAsync(DefinitionItem definitionItem);
    /// <summary>Meldet eine gefundene Fundstelle.</summary>
    /// <param name="referenceItem">Die gefundene Fundstelle.</param>
    Task OnReferenceFoundAsync(ReferenceItem referenceItem);


}