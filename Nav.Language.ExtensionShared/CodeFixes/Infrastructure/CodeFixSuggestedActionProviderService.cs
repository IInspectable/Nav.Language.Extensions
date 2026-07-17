#region Using Directives

using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// Die Aggregations-Fassade über alle <see cref="ICodeFixSuggestedActionProvider"/>. Sie bündelt die per
/// MEF importierten Provider und liefert für einen <see cref="CodeFixSuggestedActionParameter"/> die
/// vereinigte Menge ihrer Lightbulb-Aktionen — der Einstiegspunkt, den die
/// <see cref="CodeFixSuggestedActionsSource"/> aufruft.
/// </summary>
interface ICodeFixSuggestedActionProviderService {

    /// <summary>Liefert die über alle Provider vereinigten Lightbulb-Aktionen für den Parameter.</summary>
    /// <param name="parameter">Der aufruf-spezifische Eingabe-Zustand (Bereich, Snapshot, TextView).</param>
    /// <param name="cancellationToken">Token zum Abbrechen der Berechnung.</param>
    /// <returns>Die vereinigte Menge der angebotenen Aktionen (ggf. leer).</returns>
    IEnumerable<CodeFixSuggestedAction> GetCodeFixSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken);
}

/// <summary>
/// Die über MEF exportierte Standard-Implementierung von <see cref="ICodeFixSuggestedActionProviderService"/>.
/// Sie sammelt alle exportierten <see cref="ICodeFixSuggestedActionProvider"/> ein und fragt sie bei jeder
/// Anfrage der Reihe nach ab.
/// </summary>
[Export(typeof(ICodeFixSuggestedActionProviderService))]
class CodeFixSuggestedActionProviderService: ICodeFixSuggestedActionProviderService {

    readonly ImmutableList<ICodeFixSuggestedActionProvider> _codeFixActionProviders;

    /// <summary>Importiert alle exportierten Fix-Provider über MEF.</summary>
    /// <param name="codeFixActionProviders">Die per <c>[ImportMany]</c> eingesammelten Provider.</param>
    [ImportingConstructor]
    public CodeFixSuggestedActionProviderService([ImportMany] IEnumerable<ICodeFixSuggestedActionProvider> codeFixActionProviders) {
        _codeFixActionProviders = codeFixActionProviders?.ToImmutableList() ??ImmutableList<ICodeFixSuggestedActionProvider>.Empty;
    }
        
    /// <inheritdoc/>
    public IEnumerable<CodeFixSuggestedAction> GetCodeFixSuggestedActions(CodeFixSuggestedActionParameter parameter, CancellationToken cancellationToken) {
        return _codeFixActionProviders.SelectMany(p=> p.GetSuggestedActions(parameter, cancellationToken));
    }
}