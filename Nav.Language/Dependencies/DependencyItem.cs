using System;

namespace Pharmatechnik.Nav.Language.Dependencies;

/// <summary>
/// Ein Endpunkt einer <see cref="Dependency"/>-Kante — die abstrakte Identität eines an einer
/// Abhängigkeit beteiligten Elements, adressiert über <see cref="Name"/> und <see cref="Location"/>.
/// Die Wertgleichheit (<see cref="IEquatable{T}"/>) über diese beiden Merkmale macht das Element zum
/// Dictionary-Schlüssel bei der Gruppierung in <see cref="DependencyExtensions"/>. Konkrete Ausprägung
/// ist der symbolbasierte Endpunkt aus <see cref="FromSymbol"/>.
/// </summary>
public abstract class DependencyItem: IEquatable<DependencyItem> {

    /// <summary>
    /// Erzeugt einen Abhängigkeits-Endpunkt aus einem Semantikmodell-Symbol; übernimmt dessen
    /// <see cref="ISymbol.Name"/> und <see cref="ISymbol.Location"/> als Identität.
    /// </summary>
    public static DependencyItem FromSymbol(ISymbol symbol) {
        return new SymbolDependencyItem(symbol);
    }

    /// <summary>Der Name des Elements — zusammen mit <see cref="Location"/> seine Identität.</summary>
    public abstract string   Name     { get; }
    /// <summary>Die Quelltextposition des Elements — zusammen mit <see cref="Name"/> seine Identität.</summary>
    public abstract Location Location { get; }

    #region Equality members

    /// <summary>
    /// Vergleicht zwei Endpunkte wertbasiert über <see cref="Name"/> und <see cref="Location"/>.
    /// </summary>
    public virtual bool Equals(DependencyItem? other) {

        if (ReferenceEquals(null, other)) {
            return false;
        }

        if (ReferenceEquals(this, other)) {
            return true;
        }

        return string.Equals(Name, other.Name) && Equals(Location, other.Location);
    }

    /// <inheritdoc cref="Equals(DependencyItem?)"/>
    public override bool Equals(object? obj) {

        if (ReferenceEquals(null, obj)) {
            return false;
        }

        if (ReferenceEquals(this, obj)) {
            return true;
        }

        return obj is DependencyItem item && Equals(item);
    }

    /// <summary>Streut <see cref="Name"/> und <see cref="Location"/> passend zu <see cref="Equals(DependencyItem?)"/>.</summary>
    public override int GetHashCode() {
        unchecked {
            return (Name.GetHashCode() * 397) ^ Location.GetHashCode();
        }
    }

    /// <summary>Wertgleichheit — siehe <see cref="Equals(DependencyItem?)"/>.</summary>
    public static bool operator ==(DependencyItem? left, DependencyItem? right) {
        return Equals(left, right);
    }

    /// <summary>Wertungleichheit — Negation von <see cref="operator =="/>.</summary>
    public static bool operator !=(DependencyItem? left, DependencyItem? right) {
        return !Equals(left, right);
    }

    #endregion

}

/// <summary>
/// Symbolbasierter <see cref="DependencyItem"/>-Endpunkt: übernimmt Name und Position aus einem
/// <see cref="ISymbol"/>. Erzeugt über <see cref="DependencyItem.FromSymbol"/>.
/// </summary>
class SymbolDependencyItem: DependencyItem {

    public SymbolDependencyItem(ISymbol symbol) {
        Name     = symbol.Name;
        Location = symbol.Location;
    }

    /// <inheritdoc/>
    public override string   Name     { get; }
    /// <inheritdoc/>
    public override Location Location { get; }

}