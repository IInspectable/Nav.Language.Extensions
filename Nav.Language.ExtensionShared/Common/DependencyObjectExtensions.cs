﻿#region Using Directives

using System.Windows;
using System.Windows.Documents;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

static class DependencyObjectExtensions {

    public static void SetTextProperties(this DependencyObject dependencyObject, TextFormattingRunProperties textProperties) {
        dependencyObject.SetValue(TextElement.FontFamilyProperty, textProperties.Typeface.FontFamily);
        dependencyObject.SetValue(TextElement.FontSizeProperty  , textProperties.FontRenderingEmSize);
        dependencyObject.SetValue(TextElement.FontStyleProperty , textProperties.Italic ? FontStyles.Italic : FontStyles.Normal);
        dependencyObject.SetValue(TextElement.FontWeightProperty, textProperties.Bold   ? FontWeights.Bold : FontWeights.Normal);
        dependencyObject.SetValue(TextElement.BackgroundProperty, textProperties.BackgroundBrush);
        dependencyObject.SetValue(TextElement.ForegroundProperty, textProperties.ForegroundBrush);
    }

    public static void SetDefaultTextProperties(this DependencyObject dependencyObject, IClassificationFormatMap formatMap) {
        dependencyObject.SetTextProperties(formatMap.DefaultTextProperties);
    }
}