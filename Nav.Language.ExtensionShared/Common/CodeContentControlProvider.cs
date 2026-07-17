#region Using Directives

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudio.Text.Projection;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// MEF-Dienst, der WPF-Steuerelemente mit einer eingebetteten Nav-Code-Vorschau erzeugt — etwa
/// für Outlining-Tooltips und QuickInfo. Die Vorschau nutzt einen echten <see cref="IWpfTextView"/>
/// über Projektions- bzw. Elisions-Puffern, sodass die Nav-Syntaxhervorhebung erhalten bleibt.
/// </summary>
[Export(typeof(CodeContentControlProvider))]
sealed class CodeContentControlProvider {

    #region Fields/Ctor

    readonly ITextEditorFactoryService       _textEditorFactory;
    readonly IProjectionBufferFactoryService _projectionFactory;
    readonly TextViewConnectionListener      _textViewConnectionListener;
    readonly ITextBufferFactoryService       _textBufferFactoryService;
    readonly ITextEditorFactoryService       _textEditorFactoryService;
    /// <summary>
    /// Wird von MEF aufgerufen und bezieht die zur Erzeugung der Vorschau-Ansichten benötigten
    /// Editor- und Puffer-Factory-Dienste.
    /// </summary>
    [ImportingConstructor]
    public CodeContentControlProvider(ITextEditorFactoryService textEditorFactory, 
                                      IProjectionBufferFactoryService projectionFactory, 
                                      TextViewConnectionListener textViewConnectionListener, 
                                      ITextBufferFactoryService textBufferFactoryService, 
                                      ITextEditorFactoryService textEditorFactoryService) {

        _textEditorFactory          = textEditorFactory;
        _projectionFactory          = projectionFactory;
        _textViewConnectionListener = textViewConnectionListener;
        _textBufferFactoryService   = textBufferFactoryService;
        _textEditorFactoryService   = textEditorFactoryService;
    }

    #endregion

    /// <summary>
    /// Text-View-Rolle, mit der die Vorschau-Ansichten dieses Providers markiert werden, damit sie
    /// sich von regulären Editor-Ansichten unterscheiden lassen.
    /// </summary>
    public const string CodeContentTextViewRole = nameof(CodeContentTextViewRole);

    /// <summary>
    /// Erzeugt ein Vorschau-Steuerelement für das Outlining, das den angegebenen Bereich des
    /// Original-Puffers gekürzt darstellt.
    /// </summary>
    /// <param name="span">Der im Original-Puffer zu zeigende Bereich.</param>
    /// <returns>Ein <see cref="ContentControl"/> mit der eingebetteten Code-Vorschau.</returns>
    public ContentControl CreateContentControlForOutlining(SnapshotSpan span) {
        return new CodePreviewControl(() => CreateTextView(span));
    }

    /// <summary>
    /// Erzeugt ein Vorschau-Steuerelement für die QuickInfo, das den übergebenen Nav-Code in einem
    /// eigenen Puffer mit passendem Parse-Modus darstellt.
    /// </summary>
    /// <param name="textBuffer">Puffer, dessen <see cref="ITextBuffer.ContentType"/> übernommen wird.</param>
    /// <param name="navCode">Der anzuzeigende Nav-Quelltext.</param>
    /// <param name="parseMethod">Der auf den Vorschau-Puffer anzuwendende Parse-Modus.</param>
    /// <returns>Ein <see cref="ContentControl"/> mit der eingebetteten Code-Vorschau.</returns>
    public ContentControl CreateContentControlForQuickInfo(ITextBuffer textBuffer, string navCode, ParseMethod parseMethod) {
        return new CodePreviewControl(() => CreateTextView(textBuffer, navCode, parseMethod));
    }

    /// <summary>
    /// Erzeugt eine Vorschau-Ansicht über einem neuen Puffer, der mit dem übergebenen Nav-Code und
    /// Parse-Modus befüllt wird.
    /// </summary>
    IWpfTextView CreateTextView(ITextBuffer textBuffer, string navCode, ParseMethod parseMethod) {

        var buffer = _textBufferFactoryService.CreateTextBuffer(navCode, textBuffer.ContentType);

        ParserService.SetParseMethod(buffer, parseMethod);

        var roles = _textEditorFactoryService.CreateTextViewRoleSet(CodeContentTextViewRole);
        var view  = _textEditorFactory.CreateTextView(buffer, roles);

        view.Background =  Brushes.Transparent;
        view.ZoomLevel  *= 0.75;

        view.PrepareSizeToFit();

        return view;
    }

    /// <summary>
    /// Erzeugt eine Vorschau-Ansicht über einem gekürzten Elisions-/Projektions-Puffer für den
    /// angegebenen Bereich des Original-Puffers.
    /// </summary>
    IWpfTextView CreateTextView(SnapshotSpan span) {

        var buffer = CreatePreviewBuffer(span);
        var roles  = _textEditorFactoryService.CreateTextViewRoleSet(CodeContentTextViewRole);
        var view   = _textEditorFactory.CreateTextView(buffer, roles);

        view.Background =  Brushes.Transparent;
        view.ZoomLevel  *= 0.75;

        view.PrepareSizeToFit();

        return view;
    }

    /// <summary>
    /// Baut den Puffer für die Bereichsvorschau: ein Elisions-Puffer über den relevanten Zeilen,
    /// bei Kürzung um ein „…" ergänzt.
    /// </summary>
    ITextBuffer CreatePreviewBuffer(SnapshotSpan span) {

        var exposedSpans = GetPreviewSpans(span, 23, out bool needsEllipses);

        ITextBuffer buffer = _projectionFactory.CreateElisionBuffer(null, exposedSpans, ElisionBufferOptions.None);
        if(needsEllipses) {
            buffer = CreateProjectionBufferWithEllipses(buffer);
        }
        return buffer;
    }

    /// <summary>
    /// Umschließt den bereits gekürzten Elisions-Puffer mit einem Projektions-Puffer, der ein
    /// abschließendes „…" anhängt.
    /// </summary>
    ITextBuffer CreateProjectionBufferWithEllipses(ITextBuffer elisionBuffer) {
        // The elision buffer is too long.  We've already trimmed it, but now we want to add
        // a "..." to it.  We do that by creating a projection of both the elision buffer and
        // a new text buffer wrapping the ellipsis.
        var elisionSpan = elisionBuffer.CurrentSnapshot.GetFullSpan();

        var sourceSpans = new List<object>()
        {
            elisionSpan.Snapshot.CreateTrackingSpan(elisionSpan, SpanTrackingMode.EdgeExclusive),
            "..."
        };

        var projectionBuffer = _projectionFactory.CreateProjectionBuffer(
            projectionEditResolver: null,
            sourceSpans: sourceSpans,
            options: ProjectionBufferOptions.None);

        return projectionBuffer;
    }

    #region Dokumenation
    /// <summary>
    /// Liefert die Bereiche für eine Vorschau im Tooltip.
    /// </summary>
    /// <example>
    /// 
    /// o : Leehrzeichen
    /// ->: Tabulator (weite 4)
    /// T : beliebiger Text
    /// » : Begin der Region
    /// « : Ende der Region
    /// 
    /// ToTTTT->»TTTTTTT
    /// oo->--->TTTTTTTT
    /// ooooooooooTTTTTT
    /// ooooooooTTTTTTTT«
    /// TTTTTTTTTTTTTTTT
    /// 
    /// ToTTTT->|TTTTTTTT
    /// oo->--->|TTTTTTTT
    /// oooooooo|ooTTTTTT
    /// oooooooo|TTTTTTTT
    /// --------^ (signifikante Spalte)
    /// 
    /// Der Text, wie er in der Vorschau angezeigt wird:
    /// TTTTTTTT
    /// TTTTTTTT
    /// ooTTTTTT
    /// TTTTTTTT
    /// </example>
    #endregion
    NormalizedSnapshotSpanCollection GetPreviewSpans(SnapshotSpan span, int maxLines, out bool shortened) {

        var parentView = _textViewConnectionListener.GetTextViewForBuffer(span.Snapshot.TextBuffer);

        var startLineIndex = span.Start.GetContainingLine().LineNumber;
        var endLineIndex   = span.End.GetContainingLine().LineNumber;
        var lineCount      = endLineIndex - startLineIndex + 1;

        shortened = false;
        maxLines  = Math.Min(maxLines, lineCount);

        if (lineCount > maxLines) {
            shortened    = true;
            lineCount    = maxLines;
            endLineIndex = startLineIndex + lineCount - 1;

            var newEnd = span.Snapshot.GetLineFromLineNumber(endLineIndex).EndIncludingLineBreak;
            span = new SnapshotSpan(span.Start, newEnd);
        }

        var tabSize           = parentView.Options.GetTabSize();
        var significantColumn = Int32.MaxValue;
        var lines             = new List<ITextSnapshotLine>();
        for (int lineIndex = startLineIndex; lineIndex <= endLineIndex; lineIndex++) {

            var line        = span.Snapshot.GetLineFromLineNumber(lineIndex);
            var isFirstLine = lineIndex == startLineIndex;
            if (isFirstLine) {
                significantColumn = line.GetColumnForOffset(tabSize, span.Start - line.Start);
            } else {
                significantColumn = Math.Min(significantColumn, line.GetSignificantColumn(tabSize));
            }
            lines.Add(line);
        }

        var result = new List<SnapshotSpan>();
        for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++) {

            var line        = lines[lineIndex];
            var isFirstLine = lineIndex == 0;
            var isLastLine  = lineIndex == lines.Count - 1;
            int offset      = line.GetOffsetForColumn(significantColumn, tabSize);

            if (isFirstLine) {
                offset = span.Start - line.Start;
            }

            var end = isLastLine ? span.End : line.EndIncludingLineBreak;
            result.Add(new SnapshotSpan(line.Start + offset, end));
        }

        return new NormalizedSnapshotSpanCollection(result);
    }

    #region CodePreviewControl

    /// <summary>
    /// WPF-Steuerelement, das seine Text-View-Vorschau verzögert (erst bei Sichtbarkeit) erzeugt
    /// und beim Ausblenden wieder schließt, um Ressourcen zu sparen.
    /// </summary>
    sealed class CodePreviewControl : ContentControl {

        readonly Func<IWpfTextView> _createView;

        /// <summary>
        /// Die eingebettete Text-View; wird beim ersten Zugriff über die übergebene Factory erzeugt.
        /// </summary>
        ITextView TextView {
            get {
                var wpfTextView = (IWpfTextView)Content;
                if (wpfTextView == null) {
                    wpfTextView = _createView();
                    Content     = wpfTextView.VisualElement;
                }
                return wpfTextView;
            }
        }

        /// <summary>
        /// Initialisiert das Steuerelement mit der Factory, die die Vorschau-Ansicht bei Bedarf
        /// erzeugt.
        /// </summary>
        /// <param name="createView">Erzeugt die einzubettende <see cref="IWpfTextView"/>.</param>
        public CodePreviewControl(Func<IWpfTextView> createView) {
            _createView         =  createView;
            IsVisibleChanged    += OnIsVisibleChanged;
            Background          =  Brushes.Transparent;
            HorizontalAlignment =  HorizontalAlignment.Left;
        }

        /// <summary>
        /// Erzeugt die Vorschau beim Einblenden und schließt die Text-View beim Ausblenden.
        /// </summary>
        void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if ((bool)e.NewValue) {
                Content ??= _createView().VisualElement;
            } else {
                TextView.Close();
                Content = null;
            }
        }
    }

    #endregion
}