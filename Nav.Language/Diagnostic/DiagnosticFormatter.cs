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
        
    /// <summary>
    /// Der gemeinsam genutzte Standard-Formatter: nur Startposition, kein Arbeitsverzeichnis (Dateipfad
    /// unverändert). Wird u.a. von <see cref="Diagnostic.ToString()"/> als Voreinstellung verwendet.
    /// </summary>
    public static readonly DiagnosticFormatter Instance = new(displayEndLocations: false, workingDirectory: null);

    /// <summary>
    /// Ob der Span zusätzlich die Endposition ausgibt (<c>(Zeile,Spalte,EndZeile,EndSpalte)</c>) statt
    /// nur der Startposition (<c>(Zeile,Spalte)</c>).
    /// </summary>
    public bool DisplayEndLocations { get; }

    /// <summary>
    /// Optionales Arbeitsverzeichnis, relativ zu dem der Dateipfad ausgegeben wird; <c>null</c> gibt den
    /// Pfad unverändert aus.
    /// </summary>
    public string? WorkingDirectory { get; }

    /// <summary>
    /// Rendert <paramref name="diagnostic"/> zur einzeiligen Gesamtdarstellung, indem die vier
    /// Bausteine (<see cref="FormatFilePath"/>, <see cref="FormatSpan"/>,
    /// <see cref="FormatCategoryAndCode"/>, <see cref="FormatMessage"/>) zum Muster
    /// <c>&lt;Pfad&gt;&lt;Span&gt;: &lt;Kategorie+Code&gt;: &lt;Message&gt;</c> zusammengesetzt werden.
    /// </summary>
    /// <param name="diagnostic">Die zu formatierende Diagnose.</param>
    /// <param name="formatter">Optionaler Format-Provider für die Positions-Zahlen (Kultur).</param>
    /// <exception cref="ArgumentNullException"><paramref name="diagnostic"/> ist <c>null</c>.</exception>
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

    /// <summary>
    /// Bildet den Dateipfad-Baustein: leer, wenn die Diagnose keinen Dateipfad hat; sonst der
    /// <see cref="Location.FilePath"/> — relativ zu <see cref="WorkingDirectory"/>, falls gesetzt.
    /// </summary>
    /// <param name="diagnostic">Die zu formatierende Diagnose.</param>
    /// <param name="formatter">Optionaler Format-Provider (hier ohne Wirkung).</param>
    protected virtual string FormatFilePath(Diagnostic diagnostic, IFormatProvider? formatter) {

        if (diagnostic.Location.FilePath == null) {
            return String.Empty;
        }

        if (WorkingDirectory == null) {
            return diagnostic.Location.FilePath;
        }

        return PathHelper.GetRelativePath(WorkingDirectory, diagnostic.Location.FilePath);
    }

    /// <summary>
    /// Bildet den Positions-Baustein in Klammern: <c>(Zeile,Spalte)</c> oder — bei
    /// <see cref="DisplayEndLocations"/> — <c>(Zeile,Spalte,EndZeile,EndSpalte)</c>. Zeilen und Spalten
    /// werden von der 0-basierten <see cref="Location"/> auf 1-basierte Ausgabe angehoben.
    /// </summary>
    /// <param name="diagnostic">Die zu formatierende Diagnose.</param>
    /// <param name="formatter">Optionaler Format-Provider für die Zahlen.</param>
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

    /// <summary>
    /// Bildet den Baustein aus Schweregrad und Code, z.B. <c>error Nav0001</c> — der
    /// <see cref="Diagnostic.Severity"/> in Kleinschreibung, gefolgt von der
    /// <see cref="DiagnosticDescriptor.Id"/>.
    /// </summary>
    /// <param name="diagnostic">Die zu formatierende Diagnose.</param>
    /// <param name="formatter">Optionaler Format-Provider (hier ohne Wirkung).</param>
    protected virtual string FormatCategoryAndCode(Diagnostic diagnostic, IFormatProvider? formatter) {
        // ReSharper disable once UseStringInterpolation
        return String.Format("{0} {1}",                
                             diagnostic.Severity.ToString().ToLower(),
                             diagnostic.Descriptor.Id
        );
    }

    /// <summary>
    /// Bildet den Meldungs-Baustein — die fertig formatierte <see cref="Diagnostic.Message"/>.
    /// </summary>
    /// <param name="diagnostic">Die zu formatierende Diagnose.</param>
    /// <param name="formatter">Optionaler Format-Provider (hier ohne Wirkung).</param>
    protected virtual string FormatMessage(Diagnostic diagnostic, IFormatProvider? formatter) {
        return diagnostic.Message;
    }
}