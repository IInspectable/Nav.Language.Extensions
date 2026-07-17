#region Using Directives

using System;
using System.Windows;
using System.ComponentModel.Design;
using System.Linq;
using System.Windows.Controls;

using Microsoft.Internal.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;

using Pharmatechnik.Nav.Language.Extension.Commands;
using Pharmatechnik.Nav.Language.Extension.CodeFixes;
using Pharmatechnik.Nav.Language.Extension;

#endregion

namespace Pharmatechnik.Language.Nav.Extension.Margin;

/// <summary>
/// WPF-Aktionsstreifen der <see cref="Pharmatechnik.Language.Nav.Extension.Margin.NavMargin"/>-Randleiste.
/// Bietet Schaltflächen für die Code-Vorschau (<c>View Code</c>), das Generieren des C#-Codes aus den
/// <c>.nav</c>-Dateien und das Formatieren des Dokuments; die Befehle werden über
/// <see cref="NavLanguagePackage"/> bzw. <see cref="NavFormatCommand"/> ausgelöst.
/// </summary>
public partial class NavMarginControl {

    private readonly IWpfTextView       _textView;
    private readonly ITextChangeService _textChangeService;

    /// <summary>
    /// Initialisiert das Steuerelement für den <paramref name="textView"/>, hinterlegt den
    /// <paramref name="textChangeService"/> für den Format-Befehl, setzt die Tooltips und übernimmt — falls
    /// vorhanden — den VS-Stil der Datei-Health-Indikator-Schaltflächen. Muss auf dem UI-Thread laufen.
    /// </summary>
    internal NavMarginControl(IWpfTextView textView, ITextChangeService textChangeService) {
        ThreadHelper.ThrowIfNotOnUIThread();

        _textView          = textView;
        _textChangeService = textChangeService;
        InitializeComponent();

        UpdateTooltips();

        var buttonStyle = (Style)textView.VisualElement.TryFindResource("FileHealthIndicatorButtonStyle");
        if (buttonStyle != null) {
            foreach (var button in LayoutPanel.Children.OfType<Button>()) {
                button.Style = buttonStyle;
            }
        }

    }

    /// <summary>
    /// Setzt die Tooltips der drei Schaltflächen, jeweils samt zugeordnetem Tastenkürzel (sofern eines
    /// gebunden ist).
    /// </summary>
    void UpdateTooltips() {
        ThreadHelper.ThrowIfNotOnUIThread();

        NavPreviewButton.ToolTip  = GetTooltipText(KnownCommandIds.ViewCode,           "View Code");
        GenerateNavButton.ToolTip = GetTooltipText(KnownCommandIds.NavGenerateCommand, "C# Code aus .nav-Dateien generieren");
        FormatButton.ToolTip      = GetTooltipText(KnownCommandIds.FormatDocument,     "Nav-Dokument formatieren");
    }

    /// <summary>Löst die C#-Codegenerierung aus den <c>.nav</c>-Dateien aus (<see cref="KnownCommandIds.NavGenerateCommand"/>).</summary>
    private void OnGenerateNavButtonClick(object sender, RoutedEventArgs e) {
        ThreadHelper.ThrowIfNotOnUIThread();

        _textView.VisualElement.Focus();
        NavLanguagePackage.InvokeCommand(KnownCommandIds.NavGenerateCommand);
    }

    /// <summary>Öffnet die Code-Vorschau des generierten C#-Codes (<see cref="KnownCommandIds.ViewCode"/>).</summary>
    private void OnNavPreviewClick(object sender, RoutedEventArgs e) {
        ThreadHelper.ThrowIfNotOnUIThread();

        _textView.VisualElement.Focus();
        NavLanguagePackage.InvokeCommand(KnownCommandIds.ViewCode);
    }

    /// <summary>Formatiert das aktuelle <c>.nav</c>-Dokument über <see cref="NavFormatCommand.FormatDocument"/>.</summary>
    private void OnFormatButtonClick(object sender, RoutedEventArgs e) {
        ThreadHelper.ThrowIfNotOnUIThread();

        _textView.VisualElement.Focus();
        NavFormatCommand.FormatDocument(_textView, _textChangeService);
    }

    /// <summary>
    /// Bildet den Tooltip-Text aus dem Anzeigenamen <paramref name="commandName"/> und — falls vorhanden —
    /// dem für <paramref name="commandId"/> gebundenen Tastenkürzel (angehängt in Klammern).
    /// </summary>
    static string GetTooltipText(CommandID commandId, string commandName) {
        ThreadHelper.ThrowIfNotOnUIThread();

        var tooltipText = commandName;

        var keyBinding = NavKeyBindingHelper.GetKeyBinding(commandId.Guid, commandId.ID);
        if (!String.IsNullOrEmpty(keyBinding)) {
            tooltipText += $" ({keyBinding})";
        }

        return tooltipText;
    }

}