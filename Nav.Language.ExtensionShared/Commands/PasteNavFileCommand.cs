#region Using Directives

using System.Collections.Specialized;
using System.IO;
using System.Windows;

using JetBrains.Annotations;

using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

using Pharmatechnik.Nav.Language.Extension.DropHandler;
using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// Fügt einen Verweis auf eine <c>.nav</c>-Datei als taskref-Statement in den Editor ein. Zeigt der
/// eingefügte bzw. abgelegte Inhalt (Zwischenablage oder Drag-and-Drop) auf eine <c>.nav</c>-Datei, wird deren
/// Pfad relativ zur aktuell bearbeiteten Datei berechnet und an der Cursor-Position ein
/// <c>taskref "…";</c>-Statement (<see cref="SyntaxFacts.TaskrefKeyword"/>) eingefügt. Wird vom
/// <see cref="PasteCommandHandler"/> und den Drop-Handlern genutzt.
/// </summary>
class PasteNavFileCommand {

    readonly IEditorOperationsFactoryService _editorOperationsFactoryService;

    public PasteNavFileCommand(ITextView textView, IEditorOperationsFactoryService editorOperationsFactoryService) {
        TextView                        = textView;
        _editorOperationsFactoryService = editorOperationsFactoryService;

    }

    /// <summary>Die Ziel-<see cref="ITextView"/>, in die eingefügt wird.</summary>
    public ITextView TextView { get; }

    /// <summary>
    /// Fügt für einen auf eine <c>.nav</c>-Datei verweisenden <paramref name="dataObject"/> ein
    /// taskref-Statement an der Cursor-Position ein. Bricht ab (<see langword="false"/>), wenn keine
    /// <c>.nav</c>-Datei bzw. kein Zielverzeichnis ermittelbar ist oder der Cursor innerhalb eines
    /// String-Literals steht.
    /// </summary>
    public bool Execute(IDataObject dataObject) {

        ThreadHelper.ThrowIfNotOnUIThread();

        var navFileToReference = TryGetNavFile(dataObject);
        var navDirectory       = TryGetDirectory();

        if (navFileToReference == null || navDirectory == null) {
            return false;
        }

        var    directoryName    = navDirectory.FullName + Path.DirectorySeparatorChar;
        var    relativeFileName = PathHelper.GetRelativePath(fromPath: directoryName, toPath: navFileToReference.FullName);
        string taskrefStatement = $"{SyntaxFacts.TaskrefKeyword} \"{relativeFileName}\"{SyntaxFacts.Semicolon}";

        var editorOperations = _editorOperationsFactoryService.GetEditorOperations(TextView);

        var selStart     = TextView.Selection.Start.Position;
        var position     = selStart.Position;
        var line         = selStart.GetContainingLine();
        var lineText     = line.GetText();
        var linePosition = position - line.Start;

        if (lineText.IsInQuotation(linePosition)) {
            return false;
        }

        editorOperations.InsertText(taskrefStatement);

        return true;
    }

    /// <summary>Prüft, ob <paramref name="dataObject"/> auf eine <c>.nav</c>-Datei verweist und der Befehl damit ausführbar wäre.</summary>
    public bool CanExecute(IDataObject dataObject) {

        bool canExecute = TryGetNavFile(dataObject) != null;

        return canExecute;
    }

    /// <summary>Liefert die referenzierte Datei nur dann, wenn sie die <c>.nav</c>-Endung trägt, sonst <see langword="null"/>.</summary>
    [CanBeNull]
    static FileInfo TryGetNavFile(IDataObject dataObject) {

        var fileInfo = TryGetFile(dataObject);

        return fileInfo?.Extension == NavLanguageContentDefinitions.FileExtension ? fileInfo : null;
    }

    /// <summary>
    /// Extrahiert einen einzelnen Dateipfad aus dem <paramref name="dataObject"/> — je nach Herkunft aus einem
    /// Datei-Drop (Dateisystem), aus VS-Projektelementen (Projektmappen-Explorer) oder einem einfachen
    /// String — und gibt ihn als <see cref="FileInfo"/> zurück (oder <see langword="null"/>).
    /// </summary>
    [CanBeNull]
    static FileInfo TryGetFile(IDataObject dataObject) {

        var    data     = new DataObject(dataObject);
        string fileName = null;

        if (data.GetDataPresent(ClipBoardFormats.FileDrop)) {
            // The drag and drop operation came from the file system
            StringCollection files = data.GetFileDropList();

            if (files.Count == 1) {
                fileName = files[0];
            }
        } else if (data.GetDataPresent(ClipBoardFormats.VsProjectItems)) {
            // The drag and drop operation came from the VS solution explorer
            fileName = data.GetText();
        } else if (data.GetDataPresent(typeof(string))) {
            fileName = data.GetText();
        }

        return TryGetFileInfo(fileName);
    }

    /// <summary>Liefert die aktuelle <see cref="CodeGenerationUnit"/> des Puffers über den <see cref="SemanticModelService"/>.</summary>
    CodeGenerationUnit GetCodeGenerationUnit() {

        ThreadHelper.ThrowIfNotOnUIThread();

        var semanticModelService = SemanticModelService.GetOrCreateSingelton(TextView.TextBuffer);

        var generationUnitAndSnapshot = semanticModelService.UpdateSynchronously();
        var codeGenerationUnit        = generationUnitAndSnapshot.CodeGenerationUnit;

        return codeGenerationUnit;
    }

    /// <summary>Ermittelt das Verzeichnis der aktuell bearbeiteten <c>.nav</c>-Datei — Basis für den relativen taskref-Pfad.</summary>
    [CanBeNull]
    DirectoryInfo TryGetDirectory() {
        ThreadHelper.ThrowIfNotOnUIThread();
        return GetCodeGenerationUnit()?.Syntax.SyntaxTree.SourceText.FileInfo?.Directory;
    }

    /// <summary>Wandelt einen Pfad-Kandidaten in eine <see cref="FileInfo"/> um, sofern er ein gültiger Pfad ist.</summary>
    [CanBeNull]
    static FileInfo TryGetFileInfo(string candidate) {

        PathHelper.TryGetFileInfo(candidate, out var fileInfo);
        return fileInfo;

    }

}