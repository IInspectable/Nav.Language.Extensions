#region Using Directives

using System;
using System.Windows;
using System.Windows.Media;

using Microsoft.VisualStudio.Text.Editor;

using Pharmatechnik.Nav.Language.Extension;
using Pharmatechnik.Nav.Language.Extension.CodeFixes;

#endregion

namespace Pharmatechnik.Language.Nav.Extension.Margin;

/// <summary>
/// Editor-Randleiste (Margin) am unteren Rand einer <c>.nav</c>-Ansicht, die den WPF-Aktionsstreifen
/// <see cref="NavMarginControl"/> (Code-Vorschau, C#-Generierung, Dokument formatieren) hostet. Erzeugt
/// vom <see cref="NavMarginProvider"/>. Implementiert den VS-Vertrag <see cref="IWpfTextViewMargin"/>.
/// </summary>
sealed class NavMargin: IWpfTextViewMargin {

    /// <summary>Eindeutiger Name dieser Randleiste, unter dem VS sie identifiziert.</summary>
    public const string MarginName = NavLanguageContentDefinitions.ContentType + nameof(NavMargin);

    private  bool         _isDisposed;
    readonly IWpfTextView _textView;

    readonly NavMarginControl _marginControl;

    /// <summary>
    /// Initialisiert die Randleiste für den <paramref name="textView"/> und erzeugt das enthaltene
    /// <see cref="NavMarginControl"/>; der <paramref name="textChangeService"/> wird an den Format-Befehl
    /// des Steuerelements weitergereicht.
    /// </summary>
    public NavMargin(IWpfTextView textView, ITextChangeService textChangeService) {
        _textView = textView;

        _marginControl = new NavMarginControl(textView, textChangeService);

        _textView.Closed += OnTextViewClosed;

        RenderOptions.SetEdgeMode(_marginControl, EdgeMode.Aliased);

    }

    /// <summary>Meldet die Randleiste vom <see cref="ITextView.Closed"/>-Ereignis ab (idempotent).</summary>
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

    /// <summary>Liefert diese Instanz, wenn <paramref name="marginName"/> dem <see cref="MarginName"/> entspricht, sonst <see langword="null"/>.</summary>
    public ITextViewMargin GetTextViewMargin(string marginName) {
        return string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
    }

    /// <summary>Breite der Randleiste, entspricht der tatsächlichen Breite des <see cref="NavMarginControl"/>.</summary>
    public double MarginSize => _marginControl.ActualWidth;

    /// <summary>Immer <see langword="true"/> — die Randleiste ist stets aktiv.</summary>
    public bool Enabled => true;

    /// <summary>Das gehostete WPF-Steuerelement (<see cref="NavMarginControl"/>).</summary>
    public FrameworkElement VisualElement => _marginControl;

}