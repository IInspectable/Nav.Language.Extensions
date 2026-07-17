#region Using Directives

using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Text.Adornments;

using Pharmatechnik.Nav.Language.Extension.Images;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion; 

/// <summary>
/// Die <see cref="ImageElement"/>-Icons der IntelliSense-Completion-Items, aus den Nav-<see cref="ImageMonikers"/>
/// gewonnen. <see cref="FromSymbol"/> wählt das Icon passend zum Nav-Symbol.
/// </summary>
static class CompletionImages {

    /// <summary>Icon für Schlüsselwort-Vorschläge.</summary>
    public static ImageElement Keyword      = new(ImageMonikers.Keyword.ToImageId());
    /// <summary>Icon für Verzeichnis-Vorschläge.</summary>
    public static ImageElement Folder       = new(ImageMonikers.FolderClosed.ToImageId());
    /// <summary>Icon für <c>.nav</c>-Datei-Vorschläge (Include-Pfade).</summary>
    public static ImageElement NavFile      = new(ImageMonikers.Include.ToImageId());
    /// <summary>Icon für allgemeine Datei-Vorschläge.</summary>
    public static ImageElement File         = new(ImageMonikers.File.ToImageId());
    /// <summary>Icon für den Sprung ins übergeordnete Verzeichnis.</summary>
    public static ImageElement ParentFolder = new(ImageMonikers.ParentFolder.ToImageId());

    /// <summary>Icon für Choice-Knoten.</summary>
    public static ImageElement Choice          = new(ImageMonikers.ChoiceNode.ToImageId());
    /// <summary>Icon für Task-Knoten.</summary>
    public static ImageElement Task            = new(ImageMonikers.TaskNode.ToImageId());
    /// <summary>Icon für View-/Dialog-Knoten.</summary>
    public static ImageElement GuiNode         = new(ImageMonikers.ViewNode.ToImageId());
    /// <summary>Icon für Verbindungspunkte (ConnectionPoints).</summary>
    public static ImageElement ConnectionPoint = new(ImageMonikers.ExitConnectionPoint.ToImageId());

    /// <summary>Liefert das zum Nav-<paramref name="symbol"/> passende Completion-Icon.</summary>
    public static ImageElement FromSymbol(ISymbol symbol) => new(ImageMonikers.FromSymbol(symbol).ToImageId());
}