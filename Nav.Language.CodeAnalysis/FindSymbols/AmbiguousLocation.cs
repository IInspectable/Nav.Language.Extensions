#region Using Directives

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols; 

/// <summary>
/// Eine benannte <see cref="Location"/> für einen mehrdeutigen Treffer: die Stelle plus der Name des
/// adressierten Elements. Genutzt von <see cref="LocationFinder"/> für die Exit-Auflösung, wo ein Exit über
/// mehrere Exit-Transitionen zu mehreren gleichrangigen Verbindungspunkten führen kann — jeder Treffer
/// trägt hier seinen Verbindungspunkt-Namen als Unterscheidung.
/// </summary>
public class AmbiguousLocation : Location {
        
    /// <summary>
    /// Erzeugt eine benannte Location aus der zugrunde liegenden <see cref="Location"/> und dem Namen des
    /// Treffers. Ein <c>null</c>-<paramref name="name"/> wird zu <see cref="string.Empty"/> normalisiert.
    /// </summary>
    /// <param name="location">Die eigentliche Location im <c>.nav</c>.</param>
    /// <param name="name">Der Name des adressierten Elements (z.B. des Exit-Verbindungspunkts).</param>
    public AmbiguousLocation(Location location, string name) : base(location) {
        Name = name ??string.Empty;
    }

    /// <summary>Der Name des adressierten Elements; nie <c>null</c>.</summary>
    [NotNull]
    public string Name { get; }
}