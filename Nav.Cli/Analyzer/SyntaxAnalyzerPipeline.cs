#region Using Directives

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Pharmatechnik.Nav.Language.Generator;

#endregion

namespace Pharmatechnik.Nav.Language.Analyzer; 

/// <summary>
/// Fährt einen <see cref="SyntaxNodeAnalyzer"/> über eine Menge von <see cref="FileSpec"/>: liest je Datei
/// den <c>SyntaxTree</c> über eine <see cref="ISyntaxProviderFactory"/> ein und lässt den Analyzer darüber
/// laufen. Die Grundlage des Analyse-Nebenpfads des CLI-Hosts (vgl. <see cref="SyntaxAnalyzerProgram"/>).
/// </summary>
class SyntaxAnalyzerPipeline  {

    readonly ILogger                _logger;
    readonly ISyntaxProviderFactory _syntaxProviderFactory;

    /// <summary>Erzeugt die Pipeline.</summary>
    /// <param name="logger">Der Logger für Diagnosen (z.B. nicht gefundene Dateien).</param>
    /// <param name="syntaxProviderFactory">Die Quelle der Syntaxbäume; <see langword="null"/> wählt
    /// <see cref="SyntaxProviderFactory.Default"/>.</param>
    public SyntaxAnalyzerPipeline(ILogger logger, ISyntaxProviderFactory syntaxProviderFactory = null) {
        _logger                = logger;
        _syntaxProviderFactory = syntaxProviderFactory ?? SyntaxProviderFactory.Default;
    }

    /// <summary>
    /// Lässt <paramref name="analyzer"/> nacheinander über die Syntaxbäume aller <paramref name="files"/>
    /// laufen. Je Datei werden <see cref="SyntaxNodeAnalyzer.CurrentFile"/> und
    /// <see cref="SyntaxNodeAnalyzer.Logger"/> gesetzt; eine Datei ohne einlesbaren Syntaxbaum wird als
    /// Fehler protokolliert und übersprungen.
    /// </summary>
    /// <typeparam name="T">Der konkrete Analyzer-Typ.</typeparam>
    /// <param name="files">Die zu analysierenden Dateien.</param>
    /// <param name="analyzer">Der über jede Datei laufende Analyzer.</param>
    public void Run<T>(IEnumerable<FileSpec> files, T analyzer) where T : SyntaxNodeAnalyzer {

        analyzer.Logger = _logger;
        //   using (var logger = new LoggerAdapter(_logger))
        using var syntaxProvider = _syntaxProviderFactory.CreateProvider();
        foreach (var file in files) {
            analyzer.CurrentFile = file;
            // 1. SyntaxTree
            var syntax = syntaxProvider.GetSyntax(file.FilePath);
            if (syntax == null) {
                _logger?.LogError(String.Format(DiagnosticDescriptors.Semantic.Nav0004File0NotFound.MessageFormat, file));
                continue;
            }

            analyzer.Walk(syntax);
        }

    }
}

/// <summary>
/// Basisklasse der von der <see cref="SyntaxAnalyzerPipeline"/> gefahrenen Analyzer: ein
/// <see cref="SyntaxNodeWalker"/>, der zusätzlich die gerade bearbeitete Datei
/// (<see cref="CurrentFile"/>) und einen <see cref="Logger"/> kennt. Konkrete Analyzer überschreiben die
/// Walk-Methoden des <see cref="SyntaxNodeWalker"/>.
/// </summary>
public abstract class SyntaxNodeAnalyzer : SyntaxNodeWalker  {
    /// <summary>Die gerade analysierte Datei; von der <see cref="SyntaxAnalyzerPipeline"/> je Datei gesetzt.</summary>
    public FileSpec CurrentFile { get; set; }
    /// <summary>Der Logger für Ausgaben des Analyzers; von der <see cref="SyntaxAnalyzerPipeline"/> gesetzt.</summary>
    public ILogger  Logger      { get; set; }
}

/// <summary>
/// Referenz-Analyzer: zählt die Syntaxknoten, deren Typname zu einem Regex-Muster passt, und meldet je
/// Treffer die betroffene Datei. Wird vom <see cref="SyntaxAnalyzerProgram"/> genutzt, um
/// <c>CodeNotImplemented</c>-Vorkommen (bzw. beliebige, per Muster gewählte Knoten) über einen
/// Verzeichnisbaum zu zählen.
/// </summary>
public class CodeNotImplementedAnalyzer: SyntaxNodeAnalyzer {

    /// <summary>Erzeugt den Analyzer.</summary>
    /// <param name="pattern">Das Teilmuster, das im Knotentypnamen (case-insensitiv) gesucht wird; ein
    /// leeres Muster passt auf jeden Knoten.</param>
    public CodeNotImplementedAnalyzer(string pattern) {
        pattern = String.IsNullOrEmpty(pattern) ? ".*": $".*{pattern}.*";

        Pattern = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Result  = 0;
    }

    /// <summary>Die Anzahl der bisher auf das <see cref="Pattern"/> passenden Knoten.</summary>
    public int   Result  { get; set; }
    /// <summary>Das aus dem Konstruktor-Muster gebaute, kompilierte, case-insensitive Suchmuster.</summary>
    public Regex Pattern { get; }

    /// <summary>
    /// Wird für jeden Knoten aufgerufen: passt der Typname des Knotens auf <see cref="Pattern"/>, wird die
    /// Datei protokolliert und <see cref="Result"/> erhöht. Delegiert anschließend an die Basis, um den
    /// Baum weiterzulaufen.
    /// </summary>
    /// <param name="node">Der aktuell besuchte Syntaxknoten.</param>
    /// <returns>Das Ergebnis des Basis-Walks (ob weiter abgestiegen wird).</returns>
    public override bool DefaultWalk(SyntaxNode node) {

        if (Pattern.IsMatch(node.GetType().Name)) {
            Logger.LogInfo($"'{CurrentFile.Identity}");
            Result += 1;
        }

        return base.DefaultWalk(node);
    }     
}