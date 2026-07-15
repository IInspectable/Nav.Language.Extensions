#region Using Directives

using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider; 

/// <summary>
/// Gemeinsame Basis der <see cref="ILocationInfoProvider"/>-Implementierungen. Sie legt die
/// abzuleitende Auflösungsmethode fest und stellt eine kleine Hilfsroutine bereit, um einen Einzelwert als
/// Sequenz zurückzugeben.
/// </summary>
abstract class LocationInfoProvider : ILocationInfoProvider {

    /// <inheritdoc/>
    public abstract Task<IEnumerable<LocationInfo>> GetLocationsAsync(CancellationToken cancellationToken = new());

    /// <summary>Verpackt einen einzelnen <paramref name="value"/> in eine einelementige Sequenz.</summary>
    protected static IEnumerable<T> ToEnumerable<T>(T value) {
        return new[] { value };
    }
}