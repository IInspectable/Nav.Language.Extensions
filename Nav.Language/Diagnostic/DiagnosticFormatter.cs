#region Using Directives

using System;

using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Rendert eine <see cref="Diagnostic"/> zu einer einzeiligen Textdarstellung nach dem Muster
/// <c>&lt;Dateipfad&gt;&lt;Span&gt;: &lt;Schweregrad&gt; &lt;Code&gt;: &lt;Message&gt;</c>
/// (z.B. <c>C:\Work\Foo.nav(12,5): error Nav0001: …</c>). Die vier Bausteine liefern die
/// <c>protected virtual</c> <c>Format*</c>-Methoden; Ableitungen (etwa
/// <see cref="UnitTestDiagnosticFormatter"/>) verändern das Format durch deren Überschreiben.
/// Gegenstück zu <see cref="Diagnostic.ToString(DiagnosticFormatter)"/>, das über diesen Formatter läuft.
/// </summary>
public class DiagnosticFormatter {

    /// <summary>
    /// Erzeugt einen Formatter.
    /// </summary>
    /// <param name="displayEndLocations">Ob der Span zusätzlich die Endposition ausgibt
    /// (<c>(Zeile,Spalte,EndZeile,EndSpalte)</c>) statt nur der Startposition
    /// (<c>(Zeile,Spalte)</c>) — siehe <see cref="DisplayEndLocations"/>.</param>
    /// <param name="workingDirectory">Optionales Arbeitsverzeichnis, relativ zu dem der Dateipfad
    /// ausgegeben wird; <c>null</c> gibt den Pfad unverändert aus — siehe <see cref="WorkingDirectory"/>.</param>
    public DiagnosticFormatter(bool displayEndLocations, string? workingDirectory = null) {
        DisplayEndLocations = displayEndLocations;
        WorkingDirectory    = workingDirectory;
    }
        
    public static readonly DiagnosticFormatter Instance = new(displayEndLocations: false, workingDirectory: null);

    public bool DisplayEndLocations { get; }

    public string? WorkingDirectory { get; }

    public virtual string Format(Diagnostic diagnostic, IFormatProvider? formatter = null) {

        if (diagnostic == null) {
            throw new ArgumentNullException(nameof(diagnostic));
        }

        // ReSharper disable once UseStringInterpolation
        return String.Format("{0}{1}: {2}: {3}",
                             FormatFilePath(diagnostic, formatter),
                             FormatSpan(diagnostic, formatter),
                             FormatCategoryAndCode(diagnostic, formatter),
                             FormatMessage(diagnostic, formatter)
        );
    }

    protected virtual string FormatFilePath(Diagnostic diagnostic, IFormatProvider? formatter) {

        if (diagnostic.Location.FilePath == null) {
            return String.Empty;
        }

        if (WorkingDirectory == null) {
            return diagnostic.Location.FilePath;
        }

        return PathHelper.GetRelativePath(WorkingDirectory, diagnostic.Location.FilePath);
    }

    protected virtual string FormatSpan(Diagnostic diagnostic, IFormatProvider? formatter) {
            
        var location = diagnostic.Location;

        if (DisplayEndLocations) {
            return string.Format(formatter, "({0},{1},{2},{3})",
                                 location.StartLine      + 1,
                                 location.StartCharacter + 1,
                                 location.EndLine        + 1,
                                 location.EndCharacter   + 1);
        } else {
            return String.Format(formatter, "({0},{1})",
                                 location.StartLine      + 1,
                                 location.StartCharacter + 1);
        }
    }

    protected virtual string FormatCategoryAndCode(Diagnostic diagnostic, IFormatProvider? formatter) {
        // ReSharper disable once UseStringInterpolation
        return String.Format("{0} {1}",                
                             diagnostic.Severity.ToString().ToLower(),
                             diagnostic.Descriptor.Id
        );
    }

    protected virtual string FormatMessage(Diagnostic diagnostic, IFormatProvider? formatter) {
        return diagnostic.Message;
    }
}