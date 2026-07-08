#region Using Directives

using JetBrains.Annotations;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols; 

public class AmbiguousLocation : Location {
        
    public AmbiguousLocation(Location location, string name) : base(location) {
        Name = name ??string.Empty;
    }

    [NotNull]
    public string Name { get; }
}