#region Using Directives

using System.Windows;
using System.Windows.Documents;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Erweiterungsmethoden, die die Text-Formatierung des VS-Editors
/// (<see cref="TextFormattingRunProperties"/>) auf WPF-<see cref="DependencyObject"/>e übertragen.
/// </summary>
static class DependencyObjectExtensions {

    /// <summary>
    /// Überträgt Schriftart, -größe, -stil und -stärke sowie Vorder- und Hintergrund der
    /// Editor-Textformatierung auf die WPF-Text-Eigenschaften des Objekts.
    /// </summary>
    /// <param name="dependencyObject">Das WPF-Objekt, dessen Text-Eigenschaften gesetzt werden.</param>
    /// <param name="textProperties">Die zu übernehmende Editor-Textformatierung.</param>
    public static void SetTextProperties(this DependencyObject dependencyObject, TextFormattingRunProperties textProperties) {
        dependencyObject.SetValue(TextElement.FontFamilyProperty, textProperties.Typeface.FontFamily);
        dependencyObject.SetValue(TextElement.FontSizeProperty  , textProperties.FontRenderingEmSize);
        dependencyObject.SetValue(TextElement.FontStyleProperty , textProperties.Italic ? FontStyles.Italic : FontStyles.Normal);
        dependencyObject.SetValue(TextElement.FontWeightProperty, textProperties.Bold   ? FontWeights.Bold : FontWeights.Normal);
        dependencyObject.SetValue(TextElement.BackgroundProperty, textProperties.BackgroundBrush);
        dependencyObject.SetValue(TextElement.ForegroundProperty, textProperties.ForegroundBrush);
    }

    /// <summary>
    /// Überträgt die Standard-Textformatierung der übergebenen Classification-Format-Map auf die
    /// WPF-Text-Eigenschaften des Objekts.
    /// </summary>
    /// <param name="dependencyObject">Das WPF-Objekt, dessen Text-Eigenschaften gesetzt werden.</param>
    /// <param name="formatMap">Die Format-Map, deren <see cref="IClassificationFormatMap.DefaultTextProperties"/> übernommen werden.</param>
    public static void SetDefaultTextProperties(this DependencyObject dependencyObject, IClassificationFormatMap formatMap) {
        dependencyObject.SetTextProperties(formatMap.DefaultTextProperties);
    }
}