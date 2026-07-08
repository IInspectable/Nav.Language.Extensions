#region Using Directives

using System.Linq;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

public static class EdgeExtensions {

    /// <summary>
    /// Liefert zu allen von <paramref name="source"/> erreichbaren <see cref="Call"/>s deren
    /// <see cref="Call.ContinuationCall"/> (der „obendrauf" liegende Folge-Task-Aufruf einer Continuation,
    /// <c>… o-^ Task</c> / <c>… --^ Task</c>); Calls ohne Continuation werden übersprungen.
    /// </summary>
    public static IEnumerable<Call> GetReachableContinuationCalls(this IEdge source) {
        return source.GetReachableCalls()
                     .Select(call => call.ContinuationCall)
                     .WhereNotNull();
    }

    /// <summary>
    /// Liefert die von <paramref name="source"/> erreichbaren Continuations (<c>… o-^ Task</c> /
    /// <c>… --^ Task</c>) — die <see cref="IContinuationTransition"/>-Anhänge der durchlaufenen Kanten,
    /// Choice-Ketten rekursiv aufgelöst. Anders als <see cref="GetReachableContinuationCalls"/> (das den
    /// Folge-Task-Call liefert) trägt die Continuation selbst ihren tragenden GUI-Knoten
    /// (<see cref="IEdge.SourceReference"/>), den z.B. Nav0122 braucht.
    /// </summary>
    public static IEnumerable<IContinuationTransition> GetReachableContinuations(this IEdge source) {
        return GetReachableContinuationsImpl(source, new HashSet<IEdge>());
    }

    static IEnumerable<IContinuationTransition> GetReachableContinuationsImpl(IEdge edge, ISet<IEdge> seenEdges) {

        if (!seenEdges.Add(edge)) {
            yield break;
        }

        // Trägt die aktuell betrachtete Kante selbst eine Continuation, so ist sie erreichbar.
        if (edge is IContinuableEdge {ContinuationTransition: {} continuation}) {
            yield return continuation;
        }

        // Choices auflösen: hinter einer Choice liegende Continuations sind ebenfalls erreichbar.
        if (edge.TargetReference?.Declaration is IChoiceNodeSymbol choiceNode) {
            foreach (var reachable in choiceNode.Outgoings.SelectMany(e => GetReachableContinuationsImpl(e, seenEdges))) {
                yield return reachable;
            }
        }
    }

    /// <summary>
    /// Liefert die <b>direkten</b> Aufrufe der <paramref name="edges"/> — die <see cref="Call"/>s ihrer
    /// unmittelbaren Ziele, <b>ohne</b> Choices plattzufalten. Zeigt eine Kante auf eine Choice, entsteht
    /// ein <see cref="Call"/> auf den <b>Choice-Knoten selbst</b> (dessen <see cref="Call.Node"/> ein
    /// <see cref="IChoiceNodeSymbol"/> ist), statt — wie <see cref="GetReachableCalls(IEnumerable{IEdge})"/>
    /// — rekursiv in deren Ausgänge abzusteigen. Der V2-Codegen bildet einen solchen Choice-Call auf einen
    /// <c>{Choice}(…)</c>-Forward ab (§3.5), statt die Choice-Logik an jeder Quelle einzufalten. Für
    /// choice-freie Quellen ist das Ergebnis deckungsgleich mit <see cref="GetReachableCalls(IEnumerable{IEdge})"/>.
    /// </summary>
    public static IEnumerable<Call> GetDirectCalls(this IEnumerable<IEdge> edges) {

        var calls = new List<Call>();

        foreach (var edge in edges) {

            var targetNode = edge.TargetReference?.Declaration;

            // Nur aufgelöste Ziele mit definiertem Kantenmodus ergeben einen Call (wie GetReachableCallsImpl);
            // ein Choice-Ziel wird dabei NICHT aufgelöst, sondern selbst zum Call.
            if (targetNode != null && edge.EdgeMode != null) {
                calls.Add(new Call(targetNode, edge));
            }
        }

        return calls.Distinct(CallComparer.Default);
    }

    public static IEnumerable<Call> GetReachableCalls(this IEdge source) {
        return GetReachableCallsImpl(source, new HashSet<IEdge>()).Distinct(CallComparer.Default);
    }

    public static IEnumerable<Call> GetReachableCalls(this IEnumerable<IEdge> edges) {

        return GetReachableCallsCore(edges).Distinct(CallComparer.Default);

        static IEnumerable<Call> GetReachableCallsCore(IEnumerable<IEdge> edges) {

            var seenEdges = new HashSet<IEdge>();

            foreach (var edge in edges) {
                foreach (var call in GetReachableCallsImpl(edge, seenEdges)) {
                    yield return call;
                }
            }
        }
    }

    static IEnumerable<Call> GetReachableCallsImpl(IEdge edge, ISet<IEdge> seenEdges) {

        if (seenEdges.Contains(edge)) {
            yield break;
        }

        seenEdges.Add(edge);

        var targetNode = edge.TargetReference?.Declaration;
        if (targetNode == null) {
            yield break;
        }

        // Choices auflösen
        if (targetNode is IChoiceNodeSymbol choiceNode) {
            foreach (var call in choiceNode.Outgoings.SelectMany(e => GetReachableCallsImpl(e, seenEdges))) {
                yield return call;
            }
        } else if (edge.EdgeMode != null) {
            // Nur Edges mit einem definiertem Edge Mode ergeben einen Call
            yield return new Call(targetNode, edge);
        }
    }

    public static bool IsReachable(this IEdge source) {

        return IsReachableImpl(source, new HashSet<IEdge>());

        static bool IsReachableImpl(IEdge edge, HashSet<IEdge> seenEdges) {

            if (seenEdges.Contains(edge)) {
                return false;
            }

            seenEdges.Add(edge);

            var sourceNode = edge.SourceReference?.Declaration;
            return sourceNode switch {
                null                         => false,
                ITargetNodeSymbol targetNode => targetNode.Incomings.Any(e => IsReachableImpl(e, seenEdges)),
                // Ein Source Node, der kein Target Node ist, ist immer der Anfang und damit per Definition erreichbar
                ISourceNodeSymbol => true,
                _                 => false,
            };
        }
    }

}
