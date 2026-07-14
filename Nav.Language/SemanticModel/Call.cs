#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Ein aufgelöster Aufruf im Übergangsgraphen: welcher Knoten (<see cref="Node"/>) mit welcher
/// Aufruf-Art (<see cref="EdgeMode"/>) erreicht wird. Anders als eine <see cref="IEdge"/> ist ein
/// Call bereits das <b>Ergebnis</b> der Graph-Auflösung (siehe
/// <see cref="EdgeExtensions.GetReachableCalls(IEdge)"/> bzw.
/// <see cref="EdgeExtensions.GetDirectCalls"/>) — die Grundlage der Erreichbarkeits-Analyzer und
/// der Aufruf-Listen in der Codegenerierung.
/// </summary>
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

    /// <summary>Der aufgerufene Knoten — das (ggf. über Choices hinweg aufgelöste) Ziel der Kante.</summary>
    public INodeSymbol     Node     { get; }
    /// <summary>Der Kantenmodus, mit dem der Knoten aufgerufen wird — nie <c>null</c> (der Konstruktor erzwingt ihn).</summary>
    public IEdgeModeSymbol EdgeMode { get; }

    /// <summary>
    /// Bei einer Continuation (<c>… o-^ Task</c> / <c>… --^ Task</c>, ab Sprachversion 2) der Aufruf des
    /// Folge-Tasks, der auf diesem GUI-Knoten-Call „obendrauf" liegt; sonst null.
    /// </summary>
    public Call? ContinuationCall { get; }

}

/// <summary>
/// Wertbasierte Gleichheit für <see cref="Call"/>s: gleich sind zwei Calls, wenn Knoten-Name,
/// Kantenmodus-Name und — rekursiv — der <see cref="Call.ContinuationCall"/> übereinstimmen.
/// Damit fallen mehrere Kanten auf dasselbe Ziel mit derselben Aufruf-Art zu <b>einem</b> Call
/// zusammen (Deduplizierung in <see cref="EdgeExtensions.GetReachableCalls(IEdge)"/> u.a.).
/// </summary>
public class CallComparer: IEqualityComparer<Call> {

    /// <summary>Nur für abgeleitete Comparer — Verwendung über <see cref="Default"/> bzw. <see cref="FoldExits"/>.</summary>
    protected CallComparer() {
    }

    /// <summary>Die wertbasierte Standard-Gleichheit (Knoten-Name, Kantenmodus-Name, Continuation).</summary>
    public static readonly IEqualityComparer<Call> Default   = new CallComparer();
    /// <summary>
    /// Wie <see cref="Default"/>, aber alle Aufrufe von Exit-Knoten (<see cref="IExitNodeSymbol"/>)
    /// gelten zusätzlich untereinander als gleich — in der Codegenerierung werden Exits nicht
    /// unterschieden.
    /// </summary>
    public static readonly IEqualityComparer<Call> FoldExits = new FoldExitsCallComparer();

    /// <summary>
    /// Ob <paramref name="x"/> und <paramref name="y"/> denselben Aufruf bezeichnen — Vergleich
    /// über Knoten-Name, Kantenmodus-Name und (rekursiv) <see cref="Call.ContinuationCall"/>.
    /// </summary>
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

    /// <summary>Hashcode passend zu <see cref="Equals(Call, Call)"/> (Knoten-Name, Kantenmodus-Name, Continuation).</summary>
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
