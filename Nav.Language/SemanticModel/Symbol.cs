using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

abstract partial class Symbol: ISymbol {

    protected Symbol(string name, Location location) {
        Name     = name;
        Location = location ?? throw new ArgumentNullException(nameof(location));
    }

    public abstract SyntaxTree? SyntaxTree { get; }

    public virtual string Name { get; }

    public Location Location { get; }

    int IExtent.Start => Location.Start;
    int IExtent.End   => Location.End;

}
