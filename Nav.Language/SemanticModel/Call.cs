#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

public sealed class Call {

    public Call(INodeSymbol node, IEdgeModeSymbol edgeMode) {
        Node     = node     ?? throw new ArgumentNullException(nameof(node));
        EdgeMode = edgeMode ?? throw new ArgumentNullException(nameof(edgeMode));
    }

    public INodeSymbol     Node     { get; }
    public IEdgeModeSymbol EdgeMode { get; }

}

public class CallComparer: IEqualityComparer<Call> {

    protected CallComparer() {
    }

    public static readonly IEqualityComparer<Call> Default   = new CallComparer();
    public static readonly IEqualityComparer<Call> FoldExits = new FoldExitsCallComparer();

    public virtual bool Equals(Call? x, Call? y) {

        if (x == null && y == null) {
            return true;
        }

        if (x == null || y == null) {
            return false;
        }

        return x.Node.Name     == y.Node.Name &&
               x.EdgeMode.Name == y.EdgeMode.Name;
    }

    public virtual int GetHashCode(Call call) {
        unchecked {
            return (call.Node.Name.GetHashCode() * 397) ^ call.EdgeMode.Name.GetHashCode();
        }
    }

}

/// <summary>
/// In der Codegenerierung werden Exits nicht unterschieden
/// </summary>
class FoldExitsCallComparer: CallComparer {

    public override bool Equals(Call? x, Call? y) {
        if (base.Equals(x, y)) {
            return true;
        }

        return x?.Node is IExitNodeSymbol && y?.Node is IExitNodeSymbol;
    }

    public override int GetHashCode(Call call) {
        if (call.Node is IExitNodeSymbol) {
            return typeof(IExitNodeSymbol).GetHashCode();
        }

        return base.GetHashCode(call);

    }

}
