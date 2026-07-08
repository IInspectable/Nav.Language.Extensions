#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

static class ExtensionOrderer {
        
    public static IEnumerable<Lazy<TExtension, TMetadata>> Order<TExtension, TMetadata>(IEnumerable<Lazy<TExtension, TMetadata>> extensions) where TMetadata : IOrderableMetadata {
        var graph = new Graph<TExtension, TMetadata>(extensions);
        return graph.TopologicalSort();
    }

    public static void CheckForCycles<TExtension, TMetadata>(IEnumerable<Lazy<TExtension, TMetadata>> extensions) where TMetadata : IOrderableMetadata {
        var graph = new Graph<TExtension, TMetadata>(extensions);
        graph.CheckForCycles();
    }

    sealed class Node<TExtension, TMetadata> where TMetadata : IOrderableMetadata {
        public Node(Lazy<TExtension, TMetadata> extension) {
            Extension   = extension;
            NodesBefore = new HashSet<Node<TExtension, TMetadata>>();
        }

        public string Name {
            get { return Extension.Metadata.Name; }
        }

        public HashSet<Node<TExtension, TMetadata>> NodesBefore { get; }

        public Lazy<TExtension, TMetadata> Extension { get; }

        public void CheckForCycles() {
            CheckForCycles(new HashSet<Node<TExtension, TMetadata>>());
        }

        public void Visit(List<Lazy<TExtension, TMetadata>> result, HashSet<Node<TExtension, TMetadata>> seenNodes) {

            if (!seenNodes.Add(this)) {
                return;
            }

            foreach (var before in NodesBefore) {
                before.Visit(result, seenNodes);
            }

            result.Add(Extension);
        }

        private void CheckForCycles(HashSet<Node<TExtension, TMetadata>> seenNodes) {

            if (!seenNodes.Add(this)) {
                throw new ArgumentException($"Cycle detected in extensions. Extension Name: '{Name}'");
            }

            foreach (var before in NodesBefore) {
                before.CheckForCycles(seenNodes);
            }

            seenNodes.Remove(this);
        }
    }

    sealed class Graph<TExtension, TMetadata> where TMetadata : IOrderableMetadata {

        public Graph(IEnumerable<Lazy<TExtension, TMetadata>> extensions) {

            Nodes = new Dictionary<string, Node<TExtension, TMetadata>>();

            foreach (var extension in extensions) {
                var node = new Node<TExtension, TMetadata>(extension);
                Nodes.Add(node.Name, node);
            }

            foreach (var node in Nodes.Values) {

                foreach (var before in node.Extension.Metadata.Before) {
                    var nodeAfter = Nodes[before];
                    nodeAfter.NodesBefore.Add(node);
                }

                foreach (var after in node.Extension.Metadata.After) {
                    var nodeBefore = Nodes[after];
                    node.NodesBefore.Add(nodeBefore);
                }
            }
        }

        Dictionary<string, Node<TExtension, TMetadata>> Nodes { get; }

        public IList<Lazy<TExtension, TMetadata>> TopologicalSort() {

            CheckForCycles();

            var result    = new List<Lazy<TExtension, TMetadata>>();
            var seenNodes = new HashSet<Node<TExtension, TMetadata>>();

            foreach (var node in Nodes.Values) {
                node.Visit(result, seenNodes);
            }

            return result;
        }

        public void CheckForCycles() {
            foreach (var node in Nodes.Values) {
                node.CheckForCycles();
            }
        }
    }
}