﻿#region Using Directives

using System.Linq;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language {
    public static class EdgeExtensions {
        
        public static IEnumerable<Call> GetReachableCalls(this IEdge edge, HashSet<IEdge> seenEdges = null) {

            seenEdges = seenEdges ?? new HashSet<IEdge>();

            if (seenEdges.Contains(edge)) {
                yield break;
            }
            seenEdges.Add(edge);

            var targetNode = edge?.Target?.Declaration;
            if(targetNode == null) {
                yield break;
            }
            
            var choiceNode = targetNode as IChoiceNodeSymbol;
            if(choiceNode != null) {
                foreach(var call in choiceNode.Outgoings.SelectMany(e => GetReachableCalls(e, seenEdges))) {
                    yield return call;
                }
            } else {
                yield return new Call(targetNode, edge.EdgeMode);
            }            
        }
    }
}