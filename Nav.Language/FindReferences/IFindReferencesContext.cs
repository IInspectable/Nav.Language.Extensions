#region Using Directives

using System.Threading;
using System.Threading.Tasks;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

public interface IFindReferencesContext {

    CancellationToken CancellationToken { get; }

    Task SetSearchTitleAsync(string title);
    Task ReportMessageAsync(string message);

    Task OnDefinitionFoundAsync(DefinitionItem definitionItem);
    Task OnReferenceFoundAsync(ReferenceItem referenceItem);


}