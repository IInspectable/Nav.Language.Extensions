#region Using Directives

using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.IO;

using JetBrains.Annotations;

using Microsoft.VisualStudio.Text.Classification;

using Pharmatechnik.Nav.Language.Extension.Classification;
using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.Images;
using Pharmatechnik.Nav.Language.QuickInfo;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.QuickInfo; 

/// <summary>
/// Der geteilte Dienst, der die QuickInfo-Inhalte (WPF-<see cref="UIElement"/>) für Hover und
/// Completion-Tooltips aufbaut — für Symbole, Schlüsselwörter, Datei- und Verzeichnis-Infos. Als MEF-Dienst
/// (<c>[Export]</c>) exportiert und von <see cref="SymbolQuickInfoSource"/> und den Completion-Quellen
/// genutzt. Die Textformatierung folgt der Editor-Klassifikation (<c>tooltip</c>-Format-Map); die
/// symbolspezifische Aufbereitung übernimmt der <see cref="SymbolQuickInfoVisitor"/> (Partial-Datei).
/// </summary>
[Export]
sealed partial class QuickinfoBuilderService {

    readonly IClassificationFormatMapService                              _classificationFormatMapService;
    readonly ImmutableDictionary<TextClassification, IClassificationType> _classificationMap;

    [ImportingConstructor]
    public QuickinfoBuilderService(IClassificationFormatMapService classificationFormatMapService,
                                   IClassificationTypeRegistryService classificationTypeRegistryService) {

        _classificationFormatMapService = classificationFormatMapService;
        _classificationMap              = ClassificationTypeDefinitions.GetSyntaxTokenClassificationMap(classificationTypeRegistryService);

    }

    /// <summary>Die für QuickInfo-Tooltips maßgebliche Klassifikations-Format-Map (Schriftbild je Klassifikation).</summary>
    public IClassificationFormatMap ClassificationFormatMap => _classificationFormatMapService.GetClassificationFormatMap("tooltip");

    /// <summary>
    /// Baut den QuickInfo-Inhalt zu einem Nav-<paramref name="source"/>-Symbol (Signatur, ggf. Fan-out der
    /// erreichbaren Ziele bei Choices, plus Doku-Zeile). Delegiert an den <see cref="SymbolQuickInfoVisitor"/>.
    /// </summary>
    [CanBeNull]
    public UIElement BuildSymbolQuickInfoContent(ISymbol source) {
        return SymbolQuickInfoVisitor.Build(source, this);
    }

    /// <summary>
    /// Baut den QuickInfo-Inhalt zu einem Schlüsselwort: Kopf mit Keyword-Icon plus — falls vorhanden — die
    /// (bereits kontextabhängig aufgelöste) <paramref name="description"/> als Doku-Zeile.
    /// </summary>
    public UIElement BuildKeywordQuickInfoContent(string keyword, string description) {
        var control = new SymbolQuickInfoControl {
            CrispImage  = {Moniker = ImageMonikers.Keyword},
            TextContent = {Content = $"keyword {keyword}"}
        };

        // Die (bereits kontextabhängig aufgelöste) Bedeutung des Keywords als Doku-Zeile unter den Kopf —
        // analog zu AppendDocumentation für Symbole. Die Auflösung liegt beim Aufrufer, der den Kontext kennt
        // (Editor-Hover: das Token samt Wirt; Completion-Tooltip: die Beschreibung des Engine-Items). Ohne
        // Beschreibung bleibt es beim Kopf.
        if (string.IsNullOrEmpty(description)) {
            return control;
        }

        var panel = new StackPanel {Orientation = Orientation.Vertical};
        panel.Children.Add(control);
        panel.Children.Add(CreateDocumentationTextBlock(description));

        return panel;
    }

    /// <summary>Baut den QuickInfo-Inhalt zu einer <c>.nav</c>-Datei: Nav-Datei-Icon plus vollständiger Pfad.</summary>
    public UIElement BuildNavFileInfoQuickInfoContent(FileInfo fileInfo) {
        var control = new SymbolQuickInfoControl {
            CrispImage  = {Moniker = ImageMonikers.NavFile},
            TextContent = {Content = $"{fileInfo.FullName}"}
        };
        return control;
    }

    /// <summary>Baut den QuickInfo-Inhalt zu einem Verzeichnis: Ordner-Icon plus vollständiger Pfad.</summary>
    public UIElement BuildDirectoryInfoQuickInfoContent(DirectoryInfo dirInfo) {
        var control = new SymbolQuickInfoControl {
            CrispImage  = {Moniker = ImageMonikers.FolderClosed},
            TextContent = {Content = $"{dirInfo.FullName}"}
        };
        return control;
    }

    /// <summary>
    /// Hängt — falls über der Deklaration des Symbols ein Kommentar steht — diesen als zusätzliche
    /// Zeile unter den eigentlichen QuickInfo-Inhalt (Roslyn-Doku-Stil). Ohne Kommentar bzw. ohne
    /// Inhalt bleibt das ursprüngliche Element unverändert.
    /// </summary>
    [CanBeNull]
    public UIElement AppendDocumentation([CanBeNull] UIElement content, ISymbol symbol) {

        if (content == null) {
            return null;
        }

        var documentation = NavSymbolDocumentation.GetDocumentation(symbol);
        if (string.IsNullOrEmpty(documentation)) {
            return content;
        }

        var docBlock = CreateDocumentationTextBlock(documentation);

        var panel = new StackPanel {Orientation = Orientation.Vertical};
        panel.Children.Add(content);
        panel.Children.Add(docBlock);

        return panel;
    }

    /// <summary>
    /// Erzeugt den Doku-<see cref="TextBlock"/> aus einem (ggf. mehrzeiligen) Kommentar — mit echten
    /// Zeilenumbrüchen (WPF würde <c>\n</c> sonst zu Leerraum normalisieren) und dem Tooltip-Schriftbild.
    /// </summary>
    TextBlock CreateDocumentationTextBlock(string documentation) {

        var textBlock = new TextBlock {TextWrapping = TextWrapping.Wrap};

        textBlock.SetDefaultTextProperties(ClassificationFormatMap);

        // Mehrzeilige Kommentare zeilenweise mit echten Umbrüchen (WPF normalisiert sonst '\n' zu Leerraum).
        var lines = documentation.Split('\n');
        for (var i = 0; i < lines.Length; i++) {
            if (i > 0) {
                textBlock.Inlines.Add(new LineBreak());
            }

            textBlock.Inlines.Add(ToInline(lines[i], TextClassification.Text, ClassificationFormatMap));
        }

        return textBlock;
    }

    /// <summary>
    /// Baut den Standard-QuickInfo-Kopf eines Symbols: Icon aus der Symbolart plus die klassifizierte
    /// Signatur (<see cref="ISymbol"/>-DisplayParts). Liefert <c>null</c>, wenn keine Signatur vorliegt.
    /// </summary>
    [CanBeNull]
    SymbolQuickInfoControl CreateDefaultSymbolQuickInfoControl(ISymbol symbol) {

        var imageMoniker = ImageMonikers.FromSymbol(symbol);
        var content      = ToTextBlock(symbol.ToDisplayParts());

        if (content == null) {
            return null;
        }

        var control = new SymbolQuickInfoControl {
            CrispImage  = {Moniker = imageMoniker},
            TextContent = {Content = content}
        };

        return control;

    }

    //[CanBeNull]
    //TextBlock ToTextBlock(string text, TextClassification classification) {
    //    return ToTextBlock(new ClassifiedText(text, classification));
    //}

    //[CanBeNull]
    //TextBlock ToTextBlock(params ClassifiedText[] parts) {
    //    return ToTextBlock(parts.ToImmutableArray());
    //}

    /// <summary>
    /// Setzt eine Folge klassifizierter Textteile (<see cref="ClassifiedText"/>) zu einem <see cref="TextBlock"/>
    /// zusammen — jeder Teil mit seinem Schriftbild. Liefert <c>null</c> bei leerer Eingabe.
    /// </summary>
    [CanBeNull]
    TextBlock ToTextBlock(IReadOnlyCollection<ClassifiedText> parts) {

        if (parts.Count == 0) {
            return null;
        }

        var textBlock = new TextBlock {TextWrapping = TextWrapping.Wrap};

        textBlock.SetDefaultTextProperties(ClassificationFormatMap);

        foreach (var part in parts) {
            var inline = ToInline(part.Text, part.Classification, ClassificationFormatMap);
            textBlock.Inlines.Add(inline);
        }

        return textBlock;
    }

    /// <summary>
    /// Erzeugt ein <see cref="Run"/>-Inline aus <paramref name="text"/> und wendet das zur
    /// <paramref name="classification"/> gehörende Schriftbild der <paramref name="formatMap"/> an.
    /// </summary>
    Run ToInline(string text, TextClassification classification, IClassificationFormatMap formatMap) {

        var inline = new Run(text);

        _classificationMap.TryGetValue(classification, out var ct);
        if (ct != null) {
            var props = formatMap.GetTextProperties(ct);
            inline.SetTextProperties(props);
        }

        return inline;
    }

}