#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using JetBrains.Annotations;

using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.QuickInfo; 

partial class QuickinfoBuilderService {

    /// <summary>
    /// Symbol-Besucher, der je Nav-Symbolart das passende QuickInfo-<see cref="UIElement"/> aufbaut. Der
    /// Standardpfad zeigt Icon + Signatur; Choices ergänzen den Fan-out der erreichbaren Ziele (Kanten/Calls)
    /// im <see cref="EdgeQuickInfoControl"/>, und für das <c>init</c>-Keyword mit Alias wird bewusst kein
    /// Tooltip erzeugt.
    /// </summary>
    sealed class SymbolQuickInfoVisitor: SymbolVisitor<UIElement> {

        #region Infrastructure

        SymbolQuickInfoVisitor(ISymbol originatingSymbol, QuickinfoBuilderService quickinfoBuilderService) {
            OriginatingSymbol       = originatingSymbol;
            QuickinfoBuilderService = quickinfoBuilderService;
        }

        ISymbol                 OriginatingSymbol       { get; }
        QuickinfoBuilderService QuickinfoBuilderService { get; }

        /// <summary>
        /// Baut den vollständigen QuickInfo-Inhalt zu <paramref name="source"/>: die symbolspezifische
        /// Darstellung plus — falls über der Deklaration ein Kommentar steht — dessen Doku-Zeile
        /// (<see cref="AppendDocumentation"/>).
        /// </summary>
        [CanBeNull]
        public static UIElement Build(ISymbol source, QuickinfoBuilderService quickinfoBuilderService) {
            var builder = new SymbolQuickInfoVisitor(source, quickinfoBuilderService);
            var content = builder.Visit(source);
            // Der Kommentar über der Deklaration als zusätzliche Zeile unter der Signatur (Roslyn-Doku-Stil).
            return quickinfoBuilderService.AppendDocumentation(content, source);
        }

        #endregion

        /// <summary>Standardpfad: der symbolneutrale QuickInfo-Kopf (Icon + Signatur) über <see cref="CreateDefaultSymbolQuickInfoControl"/>.</summary>
        protected override UIElement DefaultVisit(ISymbol symbol) {
            return QuickinfoBuilderService.CreateDefaultSymbolQuickInfoControl(symbol);
        }

        /// <summary>
        /// QuickInfo für einen Init-Knoten. Für das <c>init</c>-Keyword mit Alias wird kein Tooltip erzeugt
        /// (<c>null</c>); sonst der Standardpfad.
        /// </summary>
        public override UIElement VisitInitNodeSymbol(IInitNodeSymbol initNodeSymbol) {
            // Wir zeigen keinen Tooltip für das init Keyword an, wenn es einen Alias gibt
            if (OriginatingSymbol == initNodeSymbol && initNodeSymbol.Alias != null) {
                return null;
            }

            return DefaultVisit(initNodeSymbol);

        }

        /// <summary>
        /// QuickInfo für einen Choice-Knoten: unter den Standardkopf kommt der Fan-out der erreichbaren
        /// Ziele — je Call der Kantenmodus-Icon plus das aufbereitete Zielknoten-Element, gesammelt im
        /// <see cref="EdgeQuickInfoControl"/>.
        /// </summary>
        public override UIElement VisitChoiceNodeSymbol(IChoiceNodeSymbol choiceNodeSymbol) {

            var node = base.VisitChoiceNodeSymbol(choiceNodeSymbol);

            var edgeViewModel = new EdgeViewModel(
                moniker: ImageMonikers.Edge,
                calls  : choiceNodeSymbol.ExpandCalls()
                                         .OrderBy(call => call.Node.Name)
                                         .Select(call => new CallViewModel(
                                                     edgeModeMoniker: ImageMonikers.FromSymbol(call.EdgeMode),
                                                     node           : Visit(call.Node)
                                                 )));

            var calls = new EdgeQuickInfoControl {
                DataContext = edgeViewModel
            };

            var panel = new StackPanel {
                Orientation = Orientation.Vertical
            };

            panel.Children.Add(node);
            panel.Children.Add(calls);
                
            return panel;
        }

        /// <summary>
        /// QuickInfo für eine Choice-Referenz: zeigt — sofern auflösbar — die QuickInfo der referenzierten
        /// Choice-Deklaration (inkl. Fan-out); sonst der Default-Pfad.
        /// </summary>
        public override UIElement VisitChoiceNodeReferenceSymbol(IChoiceNodeReferenceSymbol choiceNodeReferenceSymbol) {

            if (choiceNodeReferenceSymbol.Declaration is { } choiceNode) {
                return VisitChoiceNodeSymbol(choiceNode);
            }

            return base.VisitChoiceNodeReferenceSymbol(choiceNodeReferenceSymbol);
        }

        // Für eine Kante (Pfeil) zeigt die QuickInfo — wie der LSP-Hover — ihre Art und Bedeutung
        // (Signatur via DisplayParts + Beschreibung via AppendDocumentation), nicht mehr die erreichbaren
        // Ziele: die stehen bereits sichtbar neben dem Pfeil. Der Fan-out bleibt der Choice vorbehalten
        // (siehe VisitChoiceNodeSymbol). Daher genügt hier der Default-Pfad — kein eigener Override nötig.

    }

    /// <summary>
    /// View-Model eines einzelnen Calls im Choice-Fan-out: das Kantenmodus-Icon plus das aufbereitete
    /// Zielknoten-Element. Wird im <see cref="EdgeQuickInfoControl"/> per Datenbindung dargestellt.
    /// </summary>
    class CallViewModel {

        public CallViewModel(ImageMoniker edgeModeMoniker, object node) {
            EdgeModeMoniker = edgeModeMoniker;

            Node = node;
        }

        /// <summary>Icon für den Kantenmodus dieses Calls.</summary>
        [UsedImplicitly]
        public ImageMoniker EdgeModeMoniker { get; }

        /// <summary>Das darzustellende Zielknoten-Element (bereits aufbereitetes <see cref="UIElement"/>).</summary>
        [UsedImplicitly]
        public object Node { get; }

    }

    /// <summary>
    /// View-Model des Choice-Fan-outs: das Kanten-Icon plus die Liste der erreichbaren Ziele
    /// (<see cref="CallViewModel"/>). Datenkontext des <see cref="EdgeQuickInfoControl"/>.
    /// </summary>
    class EdgeViewModel {

        public EdgeViewModel(ImageMoniker moniker, IEnumerable<CallViewModel> calls) {
            Moniker = moniker;
            Calls   = new List<CallViewModel>(calls);
        }

        /// <summary>Icon der Kante (Pfeil).</summary>
        [UsedImplicitly]
        public ImageMoniker Moniker { get; }

        /// <summary>Die erreichbaren Ziele des Choice-Knotens.</summary>
        [UsedImplicitly]
        public IReadOnlyList<CallViewModel> Calls { get; }

    }

}