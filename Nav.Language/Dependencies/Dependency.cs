#nullable enable

namespace Pharmatechnik.Nav.Language.Dependencies;

public sealed class Dependency {
        
    public Dependency(DependencyItem usingItem, DependencyItem usedItem) {
        UsingItem = usingItem;
        UsedItem  = usedItem;
    }

    public DependencyItem UsingItem { get; }
    public DependencyItem UsedItem  { get; }
}