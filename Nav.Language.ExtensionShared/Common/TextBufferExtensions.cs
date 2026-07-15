#region Using Directives

using System.Windows.Threading;

using JetBrains.Annotations;

using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text;

using Pharmatechnik.Nav.Utilities.Logging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Erweiterungsmethoden über <see cref="ITextBuffer"/>, die vom Puffer zum zugehörigen
/// <see cref="ITextDocument"/> bzw. zum enthaltenden Roslyn-<see cref="Project"/> navigieren.
/// </summary>
static class TextBufferExtensions {

    static readonly Logger Logger = Logger.Create(typeof(TextBufferExtensions));

    /// <summary>
    /// Liefert das <see cref="ITextDocument"/> zu <paramref name="textBuffer"/> aus dessen
    /// Property-Bag, oder <see langword="null"/> (mit Warn-Log), wenn keines hinterlegt ist.
    /// </summary>
    /// <param name="textBuffer">Der Text-Puffer.</param>
    /// <returns>Das zugehörige <see cref="ITextDocument"/> oder <see langword="null"/>.</returns>
    [CanBeNull]
    public static ITextDocument GetTextDocument(this ITextBuffer textBuffer) {

        textBuffer.Properties.TryGetProperty(typeof(ITextDocument), out ITextDocument textDoc);

        if (textDoc == null) {
            Logger.Warn($"{nameof(GetTextDocument)}: There's no ITextDocument for the {nameof(ITextBuffer)}");
            return null;
        }

        return textDoc;
    }

    /// <summary>
    /// Ermittelt das Roslyn-<see cref="Project"/>, das die Datei hinter <paramref name="textBuffer"/>
    /// enthält (über <see cref="ITextDocument.FilePath"/> und
    /// <see cref="NavLanguagePackage.GetContainingProject"/>). Erwartet den UI-Thread.
    /// </summary>
    /// <param name="textBuffer">Der Text-Puffer.</param>
    /// <returns>Das enthaltende <see cref="Project"/> oder <see langword="null"/>.</returns>
    [CanBeNull]
    public static Project GetContainingProject(this ITextBuffer textBuffer) {

        Dispatcher.CurrentDispatcher.VerifyAccess();

        var filePath = textBuffer.GetTextDocument()?.FilePath;

        return NavLanguagePackage.GetContainingProject(filePath);
    }

}