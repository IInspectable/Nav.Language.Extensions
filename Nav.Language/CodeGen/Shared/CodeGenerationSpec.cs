#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Der Konvergenzpunkt der Codegenerierung: eine einzelne zu schreibende Datei aus Inhalt,
/// Zielpfad und Schreib-Policy. Ab hier ist die Pipeline versionsfrei — jede Generation liefert
/// ihre Artefakte als Liste solcher Specs, ohne dass der nachgelagerte <see cref="IFileGenerator"/>
/// die Art des Artefakts kennen muss.
/// </summary>
public sealed record CodeGenerationSpec {

    /// <summary>
    /// Erzeugt einen Spec. <paramref name="content"/> und <paramref name="filePath"/> werden
    /// null-sicher auf <see cref="String.Empty"/> normalisiert.
    /// </summary>
    /// <param name="content">Der zu schreibende Dateiinhalt.</param>
    /// <param name="filePath">Der Zielpfad der Datei.</param>
    /// <param name="overwritePolicy">Ob eine bereits vorhandene Datei überschrieben werden darf.</param>
    public CodeGenerationSpec(string? content, string? filePath, OverwritePolicy overwritePolicy) {
        Content         = content  ?? String.Empty;
        FilePath        = filePath ?? String.Empty;
        OverwritePolicy = overwritePolicy;
    }

    /// <summary>
    /// Sentinel für „kein Artefakt" — etwa wenn ein Options-Flag die Generierung eines Artefakts
    /// abschaltet. Solche Specs werden vor dem Schreiben herausgefiltert.
    /// </summary>
    public static readonly CodeGenerationSpec Empty = new(content: null, filePath: null, overwritePolicy: OverwritePolicy.Never);

    /// <summary>Ob dieser Spec der <see cref="Empty"/>-Sentinel ist.</summary>
    public bool IsEmpty => this == Empty;

    /// <summary>Der zu schreibende Dateiinhalt.</summary>
    public string Content { get; }

    /// <summary>Der Zielpfad der Datei.</summary>
    public string FilePath { get; }

    /// <summary>Ob eine bereits vorhandene Zieldatei überschrieben werden darf.</summary>
    public OverwritePolicy OverwritePolicy { get; }

}