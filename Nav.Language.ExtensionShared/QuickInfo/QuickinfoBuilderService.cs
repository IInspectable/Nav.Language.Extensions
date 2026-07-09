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

    public IClassificationFormatMap ClassificationFormatMap => _classificationFormatMapService.GetClassificationFormatMap("tooltip");

    [CanBeNull]
    public UIElement BuildSymbolQuickInfoContent(ISymbol source) {
        return SymbolQuickInfoVisitor.Build(source, this);
    }

    public UIElement BuildKeywordQuickInfoContent(string keyword) {
        var control = new SymbolQuickInfoControl {
            CrispImage  = {Moniker = ImageMonikers.Keyword},
            TextContent = {Content = $"keyword {keyword}"}
        };
        return control;
    }

    public UIElement BuildNavFileInfoQuickInfoContent(FileInfo fileInfo) {
        var control = new SymbolQuickInfoControl {
            CrispImage  = {Moniker = ImageMonikers.NavFile},
            TextContent = {Content = $"{fileInfo.FullName}"}
        };
        return control;
    }

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