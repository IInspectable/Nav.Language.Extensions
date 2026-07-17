namespace Pharmatechnik.Nav.Language.Extension.DropHandler; 

/// <summary>
/// Namen der Zwischenablage-/Drag-and-Drop-Datenformate, die der <see cref="FileDropHandler"/> verarbeitet.
/// </summary>
static class ClipBoardFormats {

    /// <summary>Format für aus dem Projektmappen-Explorer gezogene Projektelemente.</summary>
    public const string VsProjectItems = "CF_VSSTGPROJECTITEMS";
    /// <summary>Format für aus dem Dateisystem (z.B. Windows-Explorer) gezogene Dateien.</summary>
    public const string FileDrop       = "FileDrop";

}