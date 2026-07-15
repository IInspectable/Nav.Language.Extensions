#region Using Directives

using JetBrains.Annotations;

using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Completion;

/// <summary>
/// Die <see cref="CompletionFilter"/>-Kategorien der IntelliSense-Toolbar (Symbol + Tastenkürzel + Icon),
/// mit denen der Nutzer die Completion-Liste einschränken kann. Die Zuordnung eines Nav-Symbols zu seinem
/// Filter erfolgt über <see cref="TryGetFromSymbol"/>.
/// </summary>
static class CompletionFilters {

    /// <summary>Filter für Schlüsselwörter.</summary>
    public static CompletionFilter Keywords         = new("Keywords"         , "K", CompletionImages.Keyword);
    /// <summary>Filter für Verzeichnisse (Pfad-Vervollständigung).</summary>
    public static CompletionFilter Folders          = new("Folders"          , "D", CompletionImages.Folder);
    /// <summary>Filter für Dateien (Pfad-Vervollständigung).</summary>
    public static CompletionFilter Files            = new("Files"            , "F", CompletionImages.File);
    /// <summary>Filter für Choice-Knoten.</summary>
    public static CompletionFilter Choices          = new("Choices"          , "C", CompletionImages.Choice);
    /// <summary>Filter für View- und Dialog-Knoten.</summary>
    public static CompletionFilter GuiNodes         = new("Views and Dialogs", "V", CompletionImages.GuiNode);
    /// <summary>Filter für Verbindungspunkte (ConnectionPoints: Init-, Exit-, End-Knoten).</summary>
    public static CompletionFilter ConnectionPoints = new("Connection Points", "P", CompletionImages.ConnectionPoint);
    /// <summary>Filter für Task-Knoten.</summary>
    public static CompletionFilter Tasks            = new("Tasks"            , "T", CompletionImages.Task);

    /// <summary>
    /// Ordnet einem Nav-<see cref="ISymbol"/> die passende Filter-Kategorie zu (Verbindungspunkt, Choice,
    /// GUI-Knoten, Task). Liefert <c>null</c>, wenn das Symbol keiner Kategorie entspricht.
    /// </summary>
    [CanBeNull]
    public static CompletionFilter TryGetFromSymbol(ISymbol symbol) {
        switch (symbol) {
            case IInitNodeSymbol:
            case IExitNodeSymbol:
            case IEndNodeSymbol:    return ConnectionPoints;
            case IChoiceNodeSymbol: return Choices;
            case IGuiNodeSymbol:    return GuiNodes;
            case ITaskNodeSymbol:   return Tasks;
        }

        return null;
    }

}