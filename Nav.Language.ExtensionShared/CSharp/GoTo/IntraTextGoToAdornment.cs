#region Using Directives

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;

using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

using Pharmatechnik.Nav.Language.Extension.GoToLocation;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CSharp.GoTo; 

sealed class IntraTextGoToAdornment: ButtonBase {

    readonly         IWpfTextView        _textView;
    private readonly SnapshotSpan        _span;
    readonly         GoToLocationService _goToLocationService;
    readonly         CrispImage          _crispImage;

    internal IntraTextGoToAdornment(IntraTextGoToTag goToTag, IWpfTextView textView, SnapshotSpan span, GoToLocationService goToLocationService) {

        _textView            = textView;
        _span                = span;
        _goToLocationService = goToLocationService;
        _crispImage          = new CrispImage();

        RenderOptions.SetBitmapScalingMode(_crispImage, BitmapScalingMode.NearestNeighbor);

        Width       = 20;
        Height      = 20;
        Background  = Brushes.Transparent;
        BorderBrush = Brushes.Transparent;
        Cursor      = Cursors.Hand;
        Margin      = new Thickness(0, 0, 0, 0);
        Content     = _crispImage;

        Click += OnClick;

        Update(goToTag);
    }

    public IntraTextGoToTag GoToTag { get; private set; }

    protected override void OnVisualParentChanged(DependencyObject oldParent) {
        base.OnVisualParentChanged(oldParent);
        UpdateColor();
    }

    void OnClick(object sender, RoutedEventArgs e) {
        // Vorher den Cursor setzen, damit das rückwärts Navigieren schöner geht

        // Wichtig: Die Snapshots können mittlerweile auseinandergelkaufen sein
        var trackingSpan =_span.Snapshot.CreateTrackingSpan(_span, SpanTrackingMode.EdgeInclusive);
        var targetSpan   = trackingSpan.GetSpan(_textView.TextBuffer.CurrentSnapshot);
            
        _textView.Caret.MoveTo(targetSpan.End, PositionAffinity.Predecessor);
        _textView.VisualElement.Focus();

        NavLanguagePackage.Jtf.RunAsync(async () => {

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var transform          = TransformToAncestor(_textView.VisualElement);
            var placementRectangle = transform.TransformBounds(new Rect(0, 0, ActualWidth, ActualHeight));

            await _goToLocationService.GoToLocationInPreviewTabAsync(
                _textView,
                placementRectangle,
                GoToTag.Provider);
        });
    }

    internal void Update(IntraTextGoToTag goToTag) {
        GoToTag             = goToTag;
        ToolTip             = GoToTag.ToolTip;
        _crispImage.Moniker = GoToTag.ImageMoniker;

        UpdateColor();
    }

    void UpdateColor() {
        if (_textView.Background is SolidColorBrush backgroundBrush) {
            ImageThemingUtilities.SetImageBackgroundColor(_crispImage, backgroundBrush.Color);
        }
    }

}