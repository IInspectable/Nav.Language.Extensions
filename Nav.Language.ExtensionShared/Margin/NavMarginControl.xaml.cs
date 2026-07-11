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

public partial class NavMarginControl {

    private readonly IWpfTextView       _textView;
    private readonly ITextChangeService _textChangeService;

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

    void UpdateTooltips() {
        ThreadHelper.ThrowIfNotOnUIThread();

        NavPreviewButton.ToolTip  = GetTooltipText(KnownCommandIds.ViewCode,           "View Code");
        GenerateNavButton.ToolTip = GetTooltipText(KnownCommandIds.NavGenerateCommand, "C# Code aus .nav-Dateien generieren");
        FormatButton.ToolTip      = GetTooltipText(KnownCommandIds.FormatDocument,     "Nav-Dokument formatieren");
    }

    private void OnGenerateNavButtonClick(object sender, RoutedEventArgs e) {
        ThreadHelper.ThrowIfNotOnUIThread();

        _textView.VisualElement.Focus();
        NavLanguagePackage.InvokeCommand(KnownCommandIds.NavGenerateCommand);
    }

    private void OnNavPreviewClick(object sender, RoutedEventArgs e) {
        ThreadHelper.ThrowIfNotOnUIThread();

        _textView.VisualElement.Focus();
        NavLanguagePackage.InvokeCommand(KnownCommandIds.ViewCode);
    }

    private void OnFormatButtonClick(object sender, RoutedEventArgs e) {
        ThreadHelper.ThrowIfNotOnUIThread();

        _textView.VisualElement.Focus();
        NavFormatCommand.FormatDocument(_textView, _textChangeService);
    }

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