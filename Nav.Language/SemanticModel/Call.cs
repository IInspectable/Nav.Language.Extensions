#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

public sealed class Call {

    /// <summary>
    /// Erzeugt einen Aufruf des <paramref name="node"/> über die Kante <paramref name="edge"/>. Trägt die
    /// Kante eine Continuation (<c>… o-^ Task</c> / <c>… --^ Task</c>), wird deren Folge-Task-Aufruf als
    /// <see cref="ContinuationCall"/> mitgeführt.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="node"/> ist null oder <paramref name="edge"/> hat keinen Kantenmodus.
    /// </exception>
    public Call(INodeSymbol node, IEdge edge) {
        Node             = node          ?? throw new ArgumentNullException(nameof(node));
        EdgeMode         = edge.EdgeMode ?? throw new ArgumentNullException(nameof(edge));
        ContinuationCall = TryGetContinuationCall(edge);

        static Call? TryGetContinuationCall(IEdge edge) {

            // Nur eine Continuation mit aufgelöstem Ziel UND Kantenmodus ergibt einen Folge-Call — sonst
            // würde der geschachtelte Call-Ctor am fehlenden EdgeMode werfen (malformter Baum darf den
            // Modell-Aufbau nicht crashen).
            if (edge is IContinuableEdge {ContinuationTransition: {EdgeMode: not null, TargetReference.Declaration: {} continuationTarget} continuationEdge}) {
                return new Call(continuationTarget, continuationEdge);
            }

            return null;
        }
    }

    public INodeSymbol     Node     { get; }
    public IEdgeModeSymbol EdgeMode { get; }

    /// <summary>
    /// Bei einer Continuation (<c>… o-^ Task</c> / <c>… --^ Task</c>, ab Sprachversion 2) der Aufruf des
    /// Folge-Tasks, der auf diesem GUI-Knoten-Call „obendrauf" liegt; sonst null.
    /// </summary>
    public Call? ContinuationCall { get; }

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

        return x.Node.Name     == y.Node.Name     &&
               x.EdgeMode.Name == y.EdgeMode.Name &&
               Equals(x.ContinuationCall, y.ContinuationCall);
    }

    public virtual int GetHashCode(Call call) {
        unchecked {
            var hashCode = (call.Node.Name.GetHashCode() * 397) ^ call.EdgeMode.Name.GetHashCode();
            hashCode = (hashCode * 397) ^ (call.ContinuationCall == null ? 0 : GetHashCode(call.ContinuationCall));
            return hashCode;
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
