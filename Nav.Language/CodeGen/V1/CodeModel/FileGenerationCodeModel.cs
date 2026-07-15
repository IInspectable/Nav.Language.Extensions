#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Basis aller CodeModels, die für <b>genau eine zu erzeugende Datei</b> stehen — jede Ableitung
/// trägt den Ausschnitt des generierten C#-Codes einer der V1-Ausgabedateien. Konkrete Ableitungen:
/// <see cref="IWfsCodeModel"/> (<c>I{Task}WFS</c>), <see cref="IBeginWfsCodeModel"/>
/// (<c>IBegin{Task}WFS</c>), <see cref="WfsBaseCodeModel"/> (<c>{Task}WFSBase</c> + <c>{Task}WFS</c>),
/// <see cref="WfsCodeModel"/> (die einmalig angelegte Benutzer-Datei) und <c>TOCodeModel</c> (die
/// <c>{View}TO</c>-Stubs). Der <see cref="CodeGeneratorV1"/> bündelt sie je Task im
/// <see cref="CodeModelResult"/>.
/// </summary>
abstract class FileGenerationCodeModel : CodeModel {

    protected FileGenerationCodeModel(TaskCodeInfo taskCodeInfo, string? relativeSyntaxFileName, string? filePath) {
        RelativeSyntaxFileName = relativeSyntaxFileName ?? String.Empty;
        Task                   = taskCodeInfo           ?? throw new ArgumentNullException(nameof(taskCodeInfo));
        FilePath               = filePath               ?? String.Empty;
    }

    /// <summary>Die versionierbare Namens-/Pfadschicht des tragenden Tasks (Namespaces, Typnamen); siehe <see cref="TaskCodeInfo"/>.</summary>
    public TaskCodeInfo Task                   { get; }
    /// <summary>Der relative Pfad von der Ausgabedatei zurück zur <c>.nav</c>-Quelldatei — Inhalt der <c>&lt;NavFile&gt;</c>-Annotation im Kopf der erzeugten Datei.</summary>
    public string       RelativeSyntaxFileName { get; }
    /// <summary>Der Zielpfad, unter dem die erzeugte Datei geschrieben wird (vom <c>IPathProvider</c> bestimmt); landet als <c>FilePath</c> in der <c>CodeGenerationSpec</c>.</summary>
    public string       FilePath               { get; }               
}