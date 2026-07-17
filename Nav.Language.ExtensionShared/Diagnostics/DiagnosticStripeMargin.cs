#region Using Directives

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Diagnostics; 

/// <summary>
/// Editor-Randleiste im vertikalen Scrollbalken (Overview), die die Diagnosen des Dokuments als kleine
/// farbige Marken über die gesamte Dokumenthöhe darstellt (Fehler rot, Warnung orange, Vorschlag blau). Ein
/// Klick auf eine Marke springt zur betreffenden Diagnose. Bezieht ihre Daten aus dem gemeinsamen
/// <see cref="DiagnosticService"/> der Ansicht und zeichnet die Marken selbst (erbt von
/// <see cref="Border"/>). Erzeugt vom <see cref="DiagnosticStripeMarginProvider"/>.
/// </summary>
sealed class DiagnosticStripeMargin : Border, IWpfTextViewMargin {

    readonly IWpfTextView _textView;
    bool                  _isDisposed;

    /// <summary>Eindeutiger Name dieser Randleiste, unter dem VS sie identifiziert.</summary>
    public const string MarginName = nameof(DiagnosticStripeMargin);

    readonly DiagnosticService _diagnosticService;

    readonly IVerticalScrollBar _scrollBar;
    readonly IEditorFormatMap   _editorFormatMap;

    Dictionary<DiagnosticSeverity, GeometryGroup> _markerGeometryGroups;

    /// <summary>
    /// Initialisiert die Streifen-Randleiste für den <paramref name="textView"/>, holt den geteilten
    /// <see cref="DiagnosticService"/> und abonniert Diagnose-, Scroll-, Layout- und Format-Ereignisse, um
    /// die Marken aktuell zu halten. Der <paramref name="scrollBar"/> liefert die Y-Koordinaten der
    /// Dokumentpositionen, der <paramref name="editorFormatMapService"/> die themengerechten Markenfarben.
    /// </summary>
    public DiagnosticStripeMargin(IWpfTextView textView, IVerticalScrollBar scrollBar,
                                  IEditorFormatMapService editorFormatMapService) {

        _textView          = textView;
        _isDisposed        = false;
        _scrollBar         = scrollBar;
        _editorFormatMap   = editorFormatMapService.GetEditorFormatMap(textView);
        _diagnosticService = DiagnosticService.GetOrCreate(textView);

        ClipToBounds      = true;
        // Transparent (nicht null), damit der Streifen Maus-Klicks empfängt und nicht durchreicht —
        // sonst ließen sich die Diagnose-Marken nicht anklicken.
        Background        = Brushes.Transparent;
        VerticalAlignment = VerticalAlignment.Stretch;
        Focusable         = false;
        Width             = 10;

        RenderOptions.SetEdgeMode(this, System.Windows.Media.EdgeMode.Aliased);

        _diagnosticService.DiagnosticsChanging += OnDiagnosticsChanging;
        _diagnosticService.DiagnosticsChanged  += OnDiagnosticsChanged;
        _editorFormatMap.FormatMappingChanged  += OnFormatMappingChanged;
        _scrollBar.TrackSpanChanged            += OnTrackSpanChanged;
        _textView.LayoutChanged                += OnTextViewLayoutChanged;
        _textView.Closed                       += OnTextViewClosed;
        MouseLeftButtonUp                      += OnMouseLeftButtonUp;
    }

    // Maximaler vertikaler Abstand (in Pixeln) zwischen Klick und Marke, bis zu dem noch zur
    // betreffenden Diagnose gesprungen wird. Hält ein Klick ins Leere folgenlos.
    const double ClickToleranceInPixel = 6.0;

    /// <summary>
    /// Springt bei einem Klick zur Diagnose, deren Marke dem Klick vertikal am nächsten liegt — sofern der
    /// Abstand die <see cref="ClickToleranceInPixel"/> nicht überschreitet.
    /// </summary>
    void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {

        var clickY = e.GetPosition(this).Y;

        ITagSpan<DiagnosticErrorTag> nearest         = null;
        double                       nearestDistance = double.MaxValue;

        var severities = new[] {
            DiagnosticSeverity.Error,
            DiagnosticSeverity.Warning,
            DiagnosticSeverity.Suggestion};

        foreach (var severity in severities) {
            foreach (var mappingTagSpan in _diagnosticService.GetDiagnosticsWithSeverity(severity)) {

                var tagSpan = _textView.MapToSingleSnapshotSpan(mappingTagSpan);
                if (tagSpan == null) {
                    continue;
                }

                var distance = Math.Abs(_scrollBar.GetYCoordinateOfBufferPosition(tagSpan.Span.Start) - clickY);
                if (distance < nearestDistance) {
                    nearestDistance = distance;
                    nearest         = tagSpan;
                }
            }
        }

        if (nearest != null && nearestDistance <= ClickToleranceInPixel) {
            if (_textView.TryMoveCaretToAndEnsureVisible(nearest.Span.Start)) {
                e.Handled = true;
            }
        }
    }

    void OnTextViewClosed(object sender, EventArgs e) {
        Dispose();
    }

    void OnFormatMappingChanged(object sender, FormatItemsEventArgs e) {
            
    }

    void OnDiagnosticsChanging(object sender, EventArgs e) {
    }

    void OnDiagnosticsChanged(object sender, EventArgs e) {
        InvalidateGeometry();
        InvalidateVisual();
    }
       
    void OnTextViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {
        InvalidateGeometry();
        InvalidateVisual();
    }

    void OnTrackSpanChanged(object sender, EventArgs e) {
        InvalidateGeometry();
        InvalidateVisual();
    }

    /// <summary>Verwirft die zwischengespeicherten Marken-Geometrien, sodass sie beim nächsten Rendern neu gebildet werden.</summary>
    void InvalidateGeometry() {
        _markerGeometryGroups = null;
    }

    /// <summary>
    /// Baut die Marken-Geometrien je Schweregrad aus den aktuellen Diagnosen auf (sofern noch nicht
    /// vorhanden), indem jede Diagnoseposition über den <see cref="IVerticalScrollBar"/> auf eine
    /// Y-Koordinate abgebildet wird.
    /// </summary>
    void EnsureGeometry() {

        if(_markerGeometryGroups != null) {
            return;
        }

        _markerGeometryGroups = new Dictionary<DiagnosticSeverity, GeometryGroup>();

        var severities = new[] {
            DiagnosticSeverity.Error,
            DiagnosticSeverity.Warning,
            DiagnosticSeverity.Suggestion};
            
        foreach (var severity in severities) {
               
            var group = new GeometryGroup();
                
            foreach (var mappingTagSpan in _diagnosticService.GetDiagnosticsWithSeverity(severity)) {

                var tagSpan = _textView.MapToSingleSnapshotSpan(mappingTagSpan);
                if(tagSpan == null) {
                    continue;
                }
                group.Children.Add(GetMarkerGeometry(tagSpan.Span.Start));
            }

            _markerGeometryGroups.Add(severity, group);
        }                       
    }
        
    /// <summary>Bildet die Rechteck-Geometrie einer einzelnen Marke an der Y-Position der übergebenen Dokumentposition.</summary>
    Geometry GetMarkerGeometry(SnapshotPoint snapshotPoint) {

        double markerHeight = 2.0;

        var y = _scrollBar.GetYCoordinateOfBufferPosition(snapshotPoint);

        var rectangleGeometry = new RectangleGeometry(new Rect(0, y - markerHeight / 2, Width, markerHeight));

        return rectangleGeometry;
    }

    /// <summary>Verwirft die Marken-Geometrien bei Größenänderung, da sich die Y-Koordinaten verschieben.</summary>
    protected override Size ArrangeOverride(Size finalSize) {
        InvalidateGeometry();
        return base.ArrangeOverride(finalSize);
    }

    /// <summary>Zeichnet die Marken je Schweregrad mit der zugehörigen Farbe.</summary>
    protected override void OnRender(DrawingContext dc) {
        base.OnRender(dc);
        EnsureGeometry();

        foreach(var geoGroup in _markerGeometryGroups) {

            var brush = GetMarkerBrush(geoGroup.Key);
            dc.DrawGeometry(brush, null, geoGroup.Value);
        }            
    }

    /// <summary>Liefert die Markenfarbe für den angegebenen <paramref name="severity"/> (aus der Format-Map, mit Fallback).</summary>
    Brush GetMarkerBrush(DiagnosticSeverity severity) {
        // TODO Farben
        switch (severity) {                
            case DiagnosticSeverity.Suggestion:
                return GetForeGroundColor(DiagnosticErrorTypeNames.Suggestion , Brushes.Blue);
            case DiagnosticSeverity.Warning:
                return Brushes.Orange; //GetForeGroundColor(DiagnosticErrorTypeNames.Warning, Brushes.Orange);
            case DiagnosticSeverity.Error:
                return GetForeGroundColor(DiagnosticErrorTypeNames.Error, Brushes.Red);
            default:
                throw new ArgumentOutOfRangeException(nameof(severity), severity, null);
        }
    }

    /// <summary>Liest die Vordergrundfarbe des VS-Fehlertyps <paramref name="type"/> aus der Editor-Format-Map, sonst <paramref name="fallbackBrush"/>.</summary>
    Brush GetForeGroundColor(string type, Brush fallbackBrush) {
        ResourceDictionary resourceDictionary = _editorFormatMap.GetProperties(type);

        if (resourceDictionary.Contains(EditorFormatDefinition.ForegroundBrushId)) {
            var color = (Brush)resourceDictionary[EditorFormatDefinition.ForegroundBrushId];
            return color;
        } else {
            return fallbackBrush;
        }
    }

    /// <summary>Das dargestellte WPF-Element — diese Instanz selbst (erbt von <see cref="Border"/>).</summary>
    public FrameworkElement VisualElement {
        get {
            ThrowIfDisposed();
            return this;
        }
    }

    /// <summary>Breite der Randleiste (Streifenbreite).</summary>
    public double MarginSize {
        get {
            ThrowIfDisposed();

            return ActualWidth;
        }
    }

    /// <summary>Aktiv, solange der vertikale Scrollbalken der Ansicht angezeigt wird.</summary>
    bool ITextViewMargin.Enabled {
        get { return _textView.Options.GetOptionValue(DefaultTextViewHostOptions.VerticalScrollBarId); }
    }
        
    /// <summary>Liefert diese Instanz, wenn <paramref name="marginName"/> dem <see cref="MarginName"/> entspricht, sonst <see langword="null"/>.</summary>
    public ITextViewMargin GetTextViewMargin(string marginName) {
        return string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
    }

    /// <summary>Meldet die Randleiste von allen Ereignissen ab (idempotent).</summary>
    public void Dispose() {
        if(!_isDisposed) {
            _isDisposed = true;

            _diagnosticService.DiagnosticsChanging -= OnDiagnosticsChanging;
            _diagnosticService.DiagnosticsChanged  -= OnDiagnosticsChanged;
            _scrollBar.TrackSpanChanged            -= OnTrackSpanChanged;
            _textView.LayoutChanged                -= OnTextViewLayoutChanged;
            _editorFormatMap.FormatMappingChanged  -= OnFormatMappingChanged;
            _textView.Closed                       -= OnTextViewClosed;
            MouseLeftButtonUp                      -= OnMouseLeftButtonUp;
        }
    }

    void ThrowIfDisposed() {
        if(_isDisposed) {
            throw new ObjectDisposedException(MarginName);
        }
    }
}