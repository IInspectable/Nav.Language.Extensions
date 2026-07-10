#region Using Directives

using System;
using System.Windows;
using System.Windows.Media;

using Microsoft.VisualStudio.Text.Editor;

using Pharmatechnik.Nav.Language.Extension;
using Pharmatechnik.Nav.Language.Extension.CodeFixes;

#endregion

namespace Pharmatechnik.Language.Nav.Extension.Margin;

sealed class NavMargin: IWpfTextViewMargin {

    public const string MarginName = NavLanguageContentDefinitions.ContentType + nameof(NavMargin);

    private  bool         _isDisposed;
    readonly IWpfTextView _textView;

    readonly NavMarginControl _marginControl;

    public NavMargin(IWpfTextView textView, ITextChangeService textChangeService) {
        _textView = textView;

        _marginControl = new NavMarginControl(textView, textChangeService);

        _textView.Closed += OnTextViewClosed;

        RenderOptions.SetEdgeMode(_marginControl, EdgeMode.Aliased);

    }

    public void Dispose() {
        if (_isDisposed) {
            return;
        }

        _isDisposed = true;

        _textView.Closed -= OnTextViewClosed;
    }

    private void OnTextViewClosed(object sender, EventArgs e) {
        Dispose();
    }

    public ITextViewMargin GetTextViewMargin(string marginName) {
        return string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
    }

    public double MarginSize => _marginControl.ActualWidth;

    public bool Enabled => true;

    public FrameworkElement VisualElement => _marginControl;

}