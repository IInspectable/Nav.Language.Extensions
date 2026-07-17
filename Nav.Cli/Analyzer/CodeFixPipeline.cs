using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

using Pharmatechnik.Nav.Language.CodeFixes;
using Pharmatechnik.Nav.Language.CodeFixes.StyleFix;
using Pharmatechnik.Nav.Language.Generator;
using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language.Analyzer;

/// <summary>
/// Wendet Style-CodeFixes stapelweise auf eine Menge von <see cref="FileSpec"/> an: schlägt je Datei über
/// einen <see cref="CodeFixContext"/> Fixes vor, gibt die Datei (bei Treffern) über einen Checkout-Callback
/// zur Bearbeitung frei und schreibt die zusammengeführten Textänderungen zurück. Die Grundlage des
/// CodeFix-Nebenpfads des CLI-Hosts (vgl. <see cref="CodeFixProgram"/>).
/// </summary>
class CodeFixPipeline {

    readonly ILogger                _logger;
    readonly ISyntaxProviderFactory _syntaxProviderFactory;
    readonly ISemanticModelProvider _semanticModelProvider;

    /// <summary>Erzeugt die Pipeline.</summary>
    /// <param name="logger">Der Logger für Diagnosen und Fortschrittsmeldungen.</param>
    /// <param name="syntaxProviderFactory">Die Quelle der Syntaxbäume; <see langword="null"/> wählt
    /// <see cref="SyntaxProviderFactory.Default"/>. Das <see cref="ISemanticModelProvider"/> ist stets
    /// <see cref="SemanticModelProvider.Default"/>.</param>
    public CodeFixPipeline(ILogger logger, ISyntaxProviderFactory syntaxProviderFactory = null) {
        _logger                = logger;
        _syntaxProviderFactory = syntaxProviderFactory ?? SyntaxProviderFactory.Default;
        _semanticModelProvider = SemanticModelProvider.Default;
    }

    /// <summary>
    /// Läuft über alle <paramref name="files"/>: baut zu jeder Datei Syntaxbaum und Semantikmodell, lässt
    /// über <paramref name="suggestCodeFixes"/> die <see cref="StyleCodeFix"/>es ermitteln und — sofern es
    /// welche gibt und <paramref name="checkout"/> die Datei freigibt — wendet deren Textänderungen an und
    /// schreibt die Datei als UTF-8 zurück. Dateien ohne einlesbaren Syntaxbaum, ohne Fixes oder mit
    /// fehlgeschlagenem Checkout werden übersprungen.
    /// </summary>
    /// <param name="files">Die zu bearbeitenden Dateien.</param>
    /// <param name="checkout">Callback, der eine Datei vor dem Schreiben zur Bearbeitung freigibt (z.B.
    /// TFS-Checkout); <see langword="false"/> überspringt die Datei.</param>
    /// <param name="suggestCodeFixes">Callback, der zu einem <see cref="CodeFixContext"/> die
    /// anzuwendenden <see cref="StyleCodeFix"/>es liefert.</param>
    public void Run(IEnumerable<FileSpec> files, Func<string, bool> checkout, Func<CodeFixContext, CancellationToken, IEnumerable<StyleCodeFix>> suggestCodeFixes) {
        var settings = new TextEditorSettings(4, Environment.NewLine);

        using var syntaxProvider = _syntaxProviderFactory.CreateProvider();
        foreach (var file in files) {
            // 1. SyntaxTree
            var syntax = syntaxProvider.GetSyntax(file.FilePath);

            if (syntax == null) {
                _logger?.LogError(String.Format(DiagnosticDescriptors.Semantic.Nav0004File0NotFound.MessageFormat, file));
                continue;
            }

            var model = _semanticModelProvider.GetSemanticModel(syntax);

            var codeFixcontext = new CodeFixContext(syntax.Extent, model, settings);

            var fixes = suggestCodeFixes(codeFixcontext, default).ToList();

            if (!fixes.Any()) {
                continue;
            }

            // _logger?.LogError(file.FilePath);
            if (!checkout(file.FilePath)) {
                _logger?.LogError(file.FilePath);
                continue;
            }

            var changes   = fixes.SelectMany(fix => fix.GetTextChanges());
            var newString = ApplyChanges(syntax.SyntaxTree.SourceText.Text, changes);

            File.WriteAllText(file.FilePath, newString, Encoding.UTF8);

            _logger?.LogInfo($"Codefix applied for {file.FilePath}");

        }
    }

    /// <summary>Wendet eine Menge von <see cref="TextChange"/>s über einen <see cref="TextChangeWriter"/>
    /// auf den Ausgangstext an.</summary>
    /// <param name="text">Der Ausgangstext.</param>
    /// <param name="textChanges">Die anzuwendenden Textänderungen.</param>
    /// <returns>Der geänderte Text.</returns>
    string ApplyChanges(string text, IEnumerable<TextChange> textChanges) {
        var writer = new TextChangeWriter();
        return writer.ApplyTextChanges(text, textChanges);
    }

}