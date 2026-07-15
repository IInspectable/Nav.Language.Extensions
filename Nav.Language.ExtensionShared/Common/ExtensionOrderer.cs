#region Using Directives

using System;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Sortiert MEF-Erweiterungen topologisch anhand ihrer <see cref="IOrderableMetadata"/>-Vor- und
/// -Nach-Beziehungen (<c>Before</c>/<c>After</c>), sodass jede Erweiterung nach ihren Vorgängern
/// erscheint.
/// </summary>
static class ExtensionOrderer {
        
    /// <summary>
    /// Ordnet die Erweiterungen so, dass die über <c>Before</c>/<c>After</c> ausgedrückten
    /// Reihenfolge-Bedingungen eingehalten werden.
    /// </summary>
    /// <param name="extensions">Die zu ordnenden, verzögert erzeugten Erweiterungen.</param>
    /// <returns>Die Erweiterungen in gültiger topologischer Reihenfolge.</returns>
    public static IEnumerable<Lazy<TExtension, TMetadata>> Order<TExtension, TMetadata>(IEnumerable<Lazy<TExtension, TMetadata>> extensions) where TMetadata : IOrderableMetadata {
        var graph = new Graph<TExtension, TMetadata>(extensions);
        return graph.TopologicalSort();
    }

    /// <summary>
    /// Prüft die Reihenfolge-Beziehungen der Erweiterungen auf Zyklen und wirft bei einem Zyklus
    /// eine <see cref="ArgumentException"/>.
    /// </summary>
    /// <param name="extensions">Die zu prüfenden Erweiterungen.</param>
    public static void CheckForCycles<TExtension, TMetadata>(IEnumerable<Lazy<TExtension, TMetadata>> extensions) where TMetadata : IOrderableMetadata {
        var graph = new Graph<TExtension, TMetadata>(extensions);
        graph.CheckForCycles();
    }

    /// <summary>
    /// Knoten des Ordnungs-Graphen: kapselt eine Erweiterung samt der Menge der Knoten, die vor
    /// ihr liegen müssen.
    /// </summary>
    sealed class Node<TExtension, TMetadata> where TMetadata : IOrderableMetadata {
        public Node(Lazy<TExtension, TMetadata> extension) {
            Extension   = extension;
            NodesBefore = new HashSet<Node<TExtension, TMetadata>>();
        }

        public string Name {
            get { return Extension.Metadata.Name; }
        }

        /// <summary>Die Knoten, die in der Reihenfolge vor diesem Knoten liegen müssen.</summary>
        public HashSet<Node<TExtension, TMetadata>> NodesBefore { get; }

        /// <summary>Die von diesem Knoten gekapselte Erweiterung.</summary>
        public Lazy<TExtension, TMetadata> Extension { get; }

        /// <summary>
        /// Prüft ausgehend von diesem Knoten rekursiv auf Zyklen in den Vorgänger-Beziehungen.
        /// </summary>
        public void CheckForCycles() {
            CheckForCycles(new HashSet<Node<TExtension, TMetadata>>());
        }

        /// <summary>
        /// Fügt diesen Knoten in Tiefensuche — Vorgänger zuerst — an das Ergebnis an; bereits
        /// besuchte Knoten werden übersprungen.
        /// </summary>
        /// <param name="result">Die im Ergebnis gesammelte, geordnete Erweiterungsliste.</param>
        /// <param name="seenNodes">Bereits besuchte Knoten.</param>
        public void Visit(List<Lazy<TExtension, TMetadata>> result, HashSet<Node<TExtension, TMetadata>> seenNodes) {

            if (!seenNodes.Add(this)) {
                return;
            }

            foreach (var before in NodesBefore) {
                before.Visit(result, seenNodes);
            }

            result.Add(Extension);
        }

        /// <summary>
        /// Rekursive Zyklusprüfung, die den aktuellen Pfad in <paramref name="seenNodes"/> mitführt
        /// und bei einem erneut betretenen Knoten eine <see cref="ArgumentException"/> wirft.
        /// </summary>
        /// <param name="seenNodes">Die Knoten des aktuell verfolgten Pfads.</param>
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

    /// <summary>
    /// Gerichteter Graph aller Erweiterungen, dessen Kanten aus den <c>Before</c>/<c>After</c>-
    /// Beziehungen der Metadaten gebildet werden und der die topologische Sortierung liefert.
    /// </summary>
    sealed class Graph<TExtension, TMetadata> where TMetadata : IOrderableMetadata {

        /// <summary>
        /// Baut den Graphen aus den Erweiterungen auf und übersetzt deren <c>Before</c>/<c>After</c>-
        /// Angaben in Vorgänger-Kanten.
        /// </summary>
        /// <param name="extensions">Die in den Graphen aufzunehmenden Erweiterungen.</param>
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

        /// <summary>Die Knoten des Graphen, indiziert über <see cref="IOrderableMetadata.Name"/>.</summary>
        Dictionary<string, Node<TExtension, TMetadata>> Nodes { get; }

        /// <summary>
        /// Prüft den Graphen auf Zyklen und liefert die Erweiterungen in gültiger topologischer
        /// Reihenfolge.
        /// </summary>
        /// <returns>Die geordnete Erweiterungsliste.</returns>
        public IList<Lazy<TExtension, TMetadata>> TopologicalSort() {

            CheckForCycles();

            var result    = new List<Lazy<TExtension, TMetadata>>();
            var seenNodes = new HashSet<Node<TExtension, TMetadata>>();

            foreach (var node in Nodes.Values) {
                node.Visit(result, seenNodes);
            }

            return result;
        }

        /// <summary>Prüft alle Knoten des Graphen auf Zyklen.</summary>
        public void CheckForCycles() {
            foreach (var node in Nodes.Values) {
                node.CheckForCycles();
            }
        }
    }
}