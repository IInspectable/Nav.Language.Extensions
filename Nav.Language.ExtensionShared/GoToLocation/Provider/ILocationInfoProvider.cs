#region Using Directives

using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider; 

/// <summary>
/// Auflöser für ein oder mehrere Sprungziele. Jede konkrete Ausprägung kennt eine bestimmte Nav↔C#-Richtung
/// (etwa: von einer Nav-Annotation im generierten Code zur Nav-Quelle, oder von einem Nav-Symbol zur
/// generierten C#-Deklaration). Der <see cref="GoToLocationService"/> fragt die Provider eines
/// <see cref="GoToTag"/> ab und führt ihre Ergebnisse zusammen.
/// </summary>
public interface ILocationInfoProvider {

    /// <summary>Löst die Sprungziele dieses Providers auf. Liefert nie <c>null</c> (ggf. eine leere Menge).</summary>
    [NotNull]
    Task<IEnumerable<LocationInfo>> GetLocationsAsync(CancellationToken cancellationToken = default);        
}