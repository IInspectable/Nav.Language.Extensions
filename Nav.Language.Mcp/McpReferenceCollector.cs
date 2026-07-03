#region Using Directives

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Pharmatechnik.Nav.Language.FindReferences;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp;

/// <summary>
/// Sammelnder <see cref="IFindReferencesContext"/> für den MCP-Server: nimmt die von der Engine gefundenen
/// Definitionen/Referenzen entgegen und legt deren <see cref="Location"/> ab. Titel-/Status-Meldungen (für
/// das VS-„Find All References"-Fenster gedacht) sind hier No-Ops. Duplikate (Datei + Startposition) werden
/// entfernt; die Reihenfolge bleibt stabil. Faithful Port der LSP-<c>ReferenceCollector</c>.
/// </summary>
sealed class McpReferenceCollector: IFindReferencesContext {

    readonly bool                                          _includeDeclaration;
    readonly HashSet<(string?, int)>                       _seen    = new();
    readonly List<(Location Location, bool IsDeclaration)> _results = new();

    public McpReferenceCollector(bool includeDeclaration, CancellationToken cancellationToken) {
        _includeDeclaration = includeDeclaration;
        CancellationToken   = cancellationToken;
    }

    public CancellationToken CancellationToken { get; }

    /// <summary>Die gesammelten Treffer; Deklaration(en) tragen <c>IsDeclaration = true</c>.</summary>
    public IReadOnlyList<(Location Location, bool IsDeclaration)> Results => _results;

    public Task SetSearchTitleAsync(string title)  => Task.CompletedTask;
    public Task ReportMessageAsync(string message) => Task.CompletedTask;

    public Task OnDefinitionFoundAsync(DefinitionItem definitionItem) {
        if (_includeDeclaration) {
            Add(definitionItem?.Location, isDeclaration: true);
        }

        return Task.CompletedTask;
    }

    public Task OnReferenceFoundAsync(ReferenceItem referenceItem) {
        Add(referenceItem?.Location, isDeclaration: false);
        return Task.CompletedTask;
    }

    void Add(Location? location, bool isDeclaration) {
        if (location != null && _seen.Add((location.FilePath, location.Start))) {
            _results.Add((location, isDeclaration));
        }
    }

}
