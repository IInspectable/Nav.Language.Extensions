#region Using Directives

using System.Linq;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

public static class EdgeExtensions {

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
            yield return new Call(targetNode, edge.EdgeMode);
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
