#region Using Directives

using System;
using System.IO;

using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Generator;

/// <summary>
/// Beschreibt eine einzelne <c>.nav</c>-Eingabedatei für einen Codegenerator-Lauf
/// (<see cref="NavCodeGeneratorPipeline.Run"/>). Trennt den zur Anzeige/Protokollierung genutzten
/// <see cref="Identity"/>-Namen (i.d.R. ein relativer Pfad) vom tatsächlich zum Öffnen verwendeten
/// <see cref="FilePath"/> (i.d.R. absolut). Erzeugt wird eine Instanz entweder direkt vom Aufrufer
/// (z.B. <c>Nav.Cli</c>, das die relative Identität selbst aus einem Wurzelverzeichnis ableitet) oder
/// über die Fabrik <see cref="FromFile"/>.
/// </summary>
public class FileSpec {
        
    /// <summary>
    /// Erzeugt eine <see cref="FileSpec"/> mit expliziter Identität und Pfad.
    /// </summary>
    /// <param name="identity">Der Anzeige-/Protokollname der Datei (typischerweise relativ zu einem
    /// Wurzelverzeichnis). Landet in <see cref="Identity"/>.</param>
    /// <param name="fileName">Der Pfad, unter dem die Datei geöffnet wird (typischerweise absolut).
    /// Landet in <see cref="FilePath"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> oder
    /// <paramref name="fileName"/> ist <see langword="null"/>.</exception>
    public FileSpec(string identity, string fileName) {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        FilePath = fileName ?? throw new ArgumentNullException(nameof(fileName));
    }

    /// <summary>
    /// Erzeugt eine <see cref="FileSpec"/> aus einem einzelnen Pfad. Ist <paramref name="file"/>
    /// absolut, wird als <see cref="Identity"/> der relative Pfad zum aktuellen Arbeitsverzeichnis
    /// abgeleitet und der übergebene Pfad als <see cref="FilePath"/> übernommen; ist er relativ, bleibt
    /// er selbst die <see cref="Identity"/> und wird zusätzlich zum absoluten <see cref="FilePath"/>
    /// aufgelöst.
    /// </summary>
    /// <param name="file">Der Pfad der Eingabedatei (absolut oder relativ).</param>
    /// <returns>Die zusammengesetzte <see cref="FileSpec"/>.</returns>
    public static FileSpec FromFile(string file) {

        if (Path.IsPathRooted(file)) {
            var identity = PathHelper.GetRelativePath(Environment.CurrentDirectory, file);
            return new FileSpec(identity, file);
        } else {
            var path = PathHelper.GetFullPathNoThrow(file);
            return new FileSpec(file, path);
        }            
    }
        
    /// <summary>Der Anzeige-/Protokollname der Datei (typischerweise relativ zu einem
    /// Wurzelverzeichnis).</summary>
    public string Identity { get; }
    /// <summary>Der zum Öffnen der Datei verwendete Pfad (typischerweise absolut).</summary>
    public string FilePath { get; }
}