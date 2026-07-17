#region Using Directives

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Pharmatechnik.Nav.Language.FindReferences;

#endregion

namespace Pharmatechnik.Nav.Language.Lsp;

/// <summary>
/// Sammelnder <see cref="IFindReferencesContext"/> für den LSP-Server: nimmt die von der Engine
/// gefundenen Definitionen/Referenzen entgegen und legt deren <see cref="Location"/> ab. Titel- und
/// Status-Meldungen (für das VS-„Find All References"-Fenster gedacht) sind hier No-Ops. Duplikate
/// (Datei + Startposition) werden entfernt; die Reihenfolge bleibt stabil.
/// </summary>
sealed class ReferenceCollector: IFindReferencesContext {

    readonly bool                  _includeDeclaration;
    readonly HashSet<(string?, int)> _seen      = new();
    readonly List<Location>         _locations = new();

    public ReferenceCollector(bool includeDeclaration, CancellationToken cancellationToken) {
        _includeDeclaration = includeDeclaration;
        CancellationToken   = cancellationToken;
    }

    public CancellationToken CancellationToken { get; }

    public IReadOnlyList<Location> Locations => _locations;

    public Task SetSearchTitleAsync(string title)   => Task.CompletedTask;
    public Task ReportMessageAsync(string message)  => Task.CompletedTask;

    public Task OnDefinitionFoundAsync(DefinitionItem definitionItem) {
        if (_includeDeclaration) {
            Add(definitionItem.Location);
        }

        return Task.CompletedTask;
    }

    public Task OnReferenceFoundAsync(ReferenceItem referenceItem) {
        Add(referenceItem.Location);
        return Task.CompletedTask;
    }

    void Add(Location? location) {
        if (location != null && _seen.Add((location.FilePath, location.Start))) {
            _locations.Add(location);
        }
    }

}
