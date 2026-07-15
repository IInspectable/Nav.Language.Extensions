#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

using Microsoft;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.FindAllReferences;
using Microsoft.VisualStudio.Text.Classification;

using Pharmatechnik.Nav.Language.Extension.Classification;
using Pharmatechnik.Nav.Language.Extension.Common;
using Pharmatechnik.Nav.Language.Extension.HighlightReferences;
using Pharmatechnik.Nav.Language.Extension.Utilities;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.FindReferences; 

/// <summary>
/// Der Presenter der Nav-„Find All References"-Integration: Er öffnet über den VS-Dienst
/// <see cref="IFindAllReferencesService"/> das Ergebnisfenster und erzeugt je Suche einen
/// <see cref="FindReferencesContext"/>, in den die Engine (<see cref="Pharmatechnik.Nav.Language.FindReferences"/>)
/// Definitionen und Fundstellen einspeist. Zusätzlich stellt er die WPF-Darstellungshilfen bereit, die
/// die klassifizierten Textteile der Engine in eingefärbte <see cref="TextBlock"/>s/Tooltips für die
/// Ergebnis- und Vorschauspalten umsetzen.
/// </summary>
[Export(typeof(FindReferencesPresenter))]
class FindReferencesPresenter {

    private readonly ProjectService                  _projectService;
    readonly         IEditorFormatMapService         _editorFormatMapService;
    readonly         IClassificationFormatMapService _classificationFormatMapService;
    readonly         IFindAllReferencesService       _vsFindAllReferencesService;

    readonly ImmutableDictionary<TextClassification, IClassificationType> _classificationMap;

    [ImportingConstructor]
    public FindReferencesPresenter(SVsServiceProvider serviceProvider,
                                   ProjectService projectService,
                                   IEditorFormatMapService editorFormatMapService,
                                   IClassificationFormatMapService classificationFormatMapService,
                                   IClassificationTypeRegistryService classificationTypeRegistryService) {

        _projectService                 = projectService;
        _editorFormatMapService         = editorFormatMapService;
        _classificationFormatMapService = classificationFormatMapService;
        _classificationMap              = ClassificationTypeDefinitions.GetSyntaxTokenClassificationMap(classificationTypeRegistryService);
        _vsFindAllReferencesService     = (IFindAllReferencesService) serviceProvider.GetService(typeof(SVsFindAllReferences));
        Assumes.Present(_vsFindAllReferencesService);
    }

    IClassificationFormatMap FormatMap    => _classificationFormatMapService.GetClassificationFormatMap("tooltip");
    IEditorFormatMap         EditorFormat => _editorFormatMapService.GetEditorFormatMap("text");

    Brush HighlightBackgroundBrush {
        get {
            var properties     = EditorFormat.GetProperties(MarkerFormatDefinitionNames.ReferenceHighlight);
            var highlightBrush = properties["Background"] as Brush;

            return highlightBrush;
        }
    }

    Brush ToolWindowBackgroundBrush => (Brush) Application.Current.Resources[EnvironmentColors.ToolWindowBackgroundBrushKey];

    /// <summary>
    /// Öffnet das „Find All References"-Fenster und liefert den <see cref="FindReferencesContext"/>, an den
    /// die Engine ihre Treffer meldet. Nur auf dem UI-Thread aufrufbar.
    /// </summary>
    public FindReferencesContext StartSearch() {

        ThreadHelper.ThrowIfNotOnUIThread();

        var window        = _vsFindAllReferencesService.StartSearch("Find References");
        var projectMapper = _projectService.GetProjectMapper();
        var context       = new FindReferencesContext(this, window, projectMapper);

        return context;
    }

    /// <summary>Hinterlegt den Text-<see cref="Run"/> mit der Referenz-Highlight-Farbe (Trefferhervorhebung).</summary>
    public void HighlightBackground(Run run) {

        var highlightBrush = HighlightBackgroundBrush;

        if (highlightBrush == null) {
            return;
        }

        run.SetValue(
            TextElement.BackgroundProperty,
            HighlightBackgroundBrush);

    }

    /// <summary>Setzt den Text-<see cref="Run"/> auf Fettschrift (z.B. für den Definitionsnamen).</summary>
    public void SetBold(Run run) {
        run.SetValue(TextElement.FontWeightProperty, FontWeights.Bold);

    }

    /// <summary>Erzeugt einen Tooltip mit Toolfenster-Hintergrund für den gegebenen Inhalt.</summary>
    public ToolTip CreateToolTip(object content) {

        return new ToolTip {
            Background = ToolWindowBackgroundBrush,
            Content    = content
        };
    }

    /// <summary>
    /// Baut aus den klassifizierten Textteilen der Engine einen einzeiligen, eingefärbten
    /// <see cref="TextBlock"/> (mit Ellipsis). <paramref name="runAction"/> erlaubt pro Teil eine
    /// Nachbearbeitung (etwa Trefferhervorhebung).
    /// </summary>
    public TextBlock ToTextBlock(IEnumerable<ClassifiedText> parts, Action<Run, ClassifiedText, int> runAction = null, bool consolidateWhitespace = true) {

        var textBlock = new TextBlock {
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        textBlock.SetDefaultTextProperties(FormatMap);

        var inlines = ToInlines(parts, runAction, consolidateWhitespace);
        textBlock.Inlines.AddRange(inlines);

        return textBlock;
    }

    /// <summary>
    /// Wandelt die klassifizierten Textteile in eingefärbte WPF-<see cref="Inline"/>s; die Position (in
    /// Zeichen ab Zeilenanfang) wird an <paramref name="runAction"/> durchgereicht.
    /// </summary>
    public IEnumerable<Inline> ToInlines(IEnumerable<ClassifiedText> parts, Action<Run, ClassifiedText, int> runAction = null, bool consolidateWhitespace = true) {

        var position = 0;
        foreach (var part in parts) {

            var inline = ToInline(part, consolidateWhitespace, isLineStart: position == 0);

            runAction?.Invoke(inline, part, position);

            position += part.Text.Length;

            yield return inline;
        }

    }

    Run ToInline(ClassifiedText classifiedText, bool consolidateWhitespace, bool isLineStart) {
        return ToInline(classifiedText.Text, classifiedText.Classification, consolidateWhitespace, isLineStart);
    }

    Run ToInline(string text, TextClassification classification, bool consolidateWhitespace, bool isLineStart) {

        // Es nervt in der Vorschau, wenn Tabluatoren den Text unnötig in die Länge ziehen. Deswegen dampfen wir
        // Whitepaces auf ein Leerzeichen respektive NL ein.
        if (consolidateWhitespace &&
            classification == TextClassification.Whitespace) {

            var ws = " ";
            if (isLineStart) {
                ws = String.Empty;
            }
            // NL dürfen wir nicht einfach wegwerfen.
            text = text.GetNewLineCharCount() == 0 ? ws : Environment.NewLine;
        }

        var run = new Run(text);

        _classificationMap.TryGetValue(classification, out var ct);

        if (ct != null) {
            var props = FormatMap.GetTextProperties(ct);
            run.SetTextProperties(props);

        }

        return run;
    }

}