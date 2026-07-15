#region Using Directives

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider; 

/// <summary>
/// Provider für bereits bekannte, fertig aufgelöste <see cref="LocationInfo"/>. Er führt keine Suche aus,
/// sondern reicht die im Konstruktor übergebenen Ziele direkt durch — nützlich für Sprungziele, deren
/// Location schon feststeht (etwa die aktuelle Nav-Deklaration selbst).
/// </summary>
class SimpleLocationInfoProvider: LocationInfoProvider {

    /// <summary>Erzeugt einen leeren Provider; Ziele lassen sich über <see cref="Locations"/> ergänzen.</summary>
    public SimpleLocationInfoProvider() {
        Locations = new List<LocationInfo>();
    }

    /// <summary>Erzeugt einen Provider mit einem einzelnen Sprungziel.</summary>
    public SimpleLocationInfoProvider(LocationInfo locationInfo) {
        Locations = new List<LocationInfo> { locationInfo };
    }

    /// <summary>Erzeugt einen Provider mit den übergebenen Sprungzielen.</summary>
    public SimpleLocationInfoProvider(IEnumerable<LocationInfo> locationInfos) {
        Locations = new List<LocationInfo>(locationInfos);
    }
        
    /// <summary>Die durchzureichenden Sprungziele.</summary>
    public List<LocationInfo> Locations { get; }

    /// <inheritdoc/>
    public override Task<IEnumerable<LocationInfo>> GetLocationsAsync(CancellationToken cancellationToken = new()) {
        return Task.FromResult(Locations.AsEnumerable());
    }
}