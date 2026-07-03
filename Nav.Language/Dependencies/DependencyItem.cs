using System;

namespace Pharmatechnik.Nav.Language.Dependencies;

public abstract class DependencyItem: IEquatable<DependencyItem> {

    public static DependencyItem FromSymbol(ISymbol symbol) {
        return new SymbolDependencyItem(symbol);
    }

    public abstract string   Name     { get; }
    public abstract Location Location { get; }

    #region Equality members

    public virtual bool Equals(DependencyItem? other) {

        if (ReferenceEquals(null, other)) {
            return false;
        }

        if (ReferenceEquals(this, other)) {
            return true;
        }

        return string.Equals(Name, other.Name) && Equals(Location, other.Location);
    }

    public override bool Equals(object? obj) {

        if (ReferenceEquals(null, obj)) {
            return false;
        }

        if (ReferenceEquals(this, obj)) {
            return true;
        }

        return obj is DependencyItem item && Equals(item);
    }

    public override int GetHashCode() {
        unchecked {
            return (Name.GetHashCode() * 397) ^ Location.GetHashCode();
        }
    }

    public static bool operator ==(DependencyItem? left, DependencyItem? right) {
        return Equals(left, right);
    }

    public static bool operator !=(DependencyItem? left, DependencyItem? right) {
        return !Equals(left, right);
    }

    #endregion

}

class SymbolDependencyItem: DependencyItem {

    public SymbolDependencyItem(ISymbol symbol) {
        Name     = symbol.Name;
        Location = symbol.Location;
    }

    public override string   Name     { get; }
    public override Location Location { get; }

}