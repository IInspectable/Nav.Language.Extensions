#region Using Directives

using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen; 

/// <summary>
/// Fabrik für einen <see cref="IFileGenerator"/> — das Gegenstück zu
/// <see cref="ICodeGeneratorProvider"/> für die dateisystem-schreibende Stufe der Pipeline.
/// </summary>
public interface IFileGeneratorProvider {

    /// <summary>Erzeugt einen <see cref="IFileGenerator"/> mit den gegebenen <paramref name="options"/>.</summary>
    IFileGenerator Create(GenerationOptions options);

}

/// <summary>
/// Die dateisystem-schreibende Stufe der Codegen-Pipeline: nimmt die von einem
/// <see cref="ICodeGenerator"/> erzeugten Specs eines <see cref="CodeGenerationResult"/> entgegen
/// und materialisiert sie auf der Platte. <see cref="IDisposable"/> aus Symmetrie zum
/// <see cref="ICodeGenerator"/> und für künftige Ressourcen der Schreibstufe.
/// </summary>
public interface IFileGenerator: IDisposable {

    /// <summary>
    /// Schreibt die Artefakte eines <see cref="CodeGenerationResult"/> gemäß der jeweiligen
    /// <see cref="CodeGenerationSpec.OverwritePolicy"/> auf die Platte und liefert je Datei ein
    /// <see cref="FileGeneratorResult"/>.
    /// </summary>
    ImmutableArray<FileGeneratorResult> Generate(CodeGenerationResult codeGenerationResult);

}

/// <summary>Die Standard-Fabrik für den <see cref="FileGenerator"/> (zustandsloser Singleton, siehe <see cref="Default"/>).</summary>
public sealed class FileGeneratorProvider: IFileGeneratorProvider {

    FileGeneratorProvider() {

    }

    /// <summary>Der prozessweit geteilte Standard-Provider.</summary>
    public static readonly IFileGeneratorProvider Default = new FileGeneratorProvider();

    /// <summary>Erzeugt einen <see cref="FileGenerator"/> mit den gegebenen <paramref name="options"/>.</summary>
    public IFileGenerator Create(GenerationOptions options) {
        return new FileGenerator(options);
    }

}

/// <summary>
/// Schreibt die Artefakte eines <see cref="CodeGenerationResult"/> gemäß ihrer
/// <see cref="CodeGenerationSpec.OverwritePolicy"/> auf die Platte. Die Stufe ist bewusst
/// versionsfrei: welche und wie viele Artefakte entstehen, entscheidet allein der
/// <see cref="ICodeGenerator"/>; hier wird jeder Spec nur noch anhand seiner Policy und des
/// Ist-Zustands der Zieldatei geschrieben oder übersprungen.
/// </summary>
public class FileGenerator: Generator, IFileGenerator {

    /// <summary>Erzeugt den Schreib-Generator mit den gegebenen <paramref name="options"/> (z.B. <see cref="GenerationOptions.Force"/>, <see cref="GenerationOptions.Encoding"/>).</summary>
    public FileGenerator(GenerationOptions options): base(options) {
    }

    /// <summary>
    /// Schreibt alle Specs des <paramref name="codeGenerationResult"/> und liefert je Spec ein
    /// <see cref="FileGeneratorResult"/> (auch bei <see cref="FileGeneratorAction.Skiped"/>). Der
    /// abschließende <c>WhereNotNull</c> ist rein defensiv — <see cref="WriteFile"/> liefert im
    /// Normalfall stets ein Ergebnis.
    /// </summary>
    public ImmutableArray<FileGeneratorResult> Generate(CodeGenerationResult codeGenerationResult) {

        if (codeGenerationResult == null) {
            throw new ArgumentNullException(nameof(codeGenerationResult));
        }

        // Die Weiche über die Artefakt-Menge liegt vollständig im Generator: hier wird jeder Spec
        // schlicht gemäß seiner eigenen OverwritePolicy geschrieben — versionsfrei.
        var results = new List<FileGeneratorResult?>();

        foreach (var spec in codeGenerationResult.Specs) {
            results.Add(WriteFile(codeGenerationResult.TaskDefinition, spec));
        }

        return results.WhereNotNull()
                      .ToImmutableArray();
    }

    /// <summary>
    /// Schreibt einen einzelnen Spec — mit bis zu drei Versuchen (<see cref="Resilience"/>), um
    /// kurzzeitige Schreibkonflikte (etwa ein noch geöffnetes Handle) zu überbrücken. Legt bei
    /// Bedarf das Zielverzeichnis an, schreibt die Datei nur wenn <see cref="ShouldWrite"/> es
    /// erlaubt, und meldet das Ergebnis als <see cref="FileGeneratorAction.Updated"/> bzw.
    /// <see cref="FileGeneratorAction.Skiped"/>.
    /// </summary>
    FileGeneratorResult? WriteFile(ITaskDefinitionSymbol taskDefinition, CodeGenerationSpec codeGenerationSpec) {

        return Resilience.Execute(WriteFileImpl,
                                 maxAttempts: 3,
                                 retryDelay: TimeSpan.FromMilliseconds(10));

        FileGeneratorResult? WriteFileImpl() {

            EnsureDirectory(codeGenerationSpec.FilePath);

            var action = FileGeneratorAction.Skiped;

            if (ShouldWrite(codeGenerationSpec)) {
                File.WriteAllText(codeGenerationSpec.FilePath, codeGenerationSpec.Content, Options.Encoding);
                action = FileGeneratorAction.Updated;
            }

            return new FileGeneratorResult(taskDefinition, action, codeGenerationSpec.FilePath);
        }
    }

    /// <summary>Stellt sicher, dass das Zielverzeichnis der Datei <paramref name="fileName"/> existiert.</summary>
    static void EnsureDirectory(string fileName) {
        var dir = Path.GetDirectoryName(fileName);
        // ReSharper disable once AssignNullToNotNullAttribute Lass krachen
        Directory.CreateDirectory(dir);
    }

    /// <summary>
    /// Entscheidet, ob der Spec tatsächlich auf die Platte geschrieben wird. Eine fehlende (oder de
    /// facto leere, ≤ 4 Byte) Datei wird stets geschrieben; bei
    /// <see cref="OverwritePolicy.Never"/> bleibt eine vorhandene Datei sonst unangetastet; bei
    /// <see cref="OverwritePolicy.WhenChanged"/> wird nur bei tatsächlich geändertem Inhalt (oder
    /// erzwungen per <see cref="GenerationOptions.Force"/>) neu geschrieben.
    /// </summary>
    bool ShouldWrite(CodeGenerationSpec codeGenerationSpec) {

        // Wenn die Datei nicht existiert, wird sie neu geschrieben
        if (!File.Exists(codeGenerationSpec.FilePath)) {
            return true;
        }

        // Eine Datei mit der Größe 0 gilt als nicht existent, und wird neu geschrieben
        if (codeGenerationSpec.OverwritePolicy == OverwritePolicy.Never) {

            var fileInfo = new FileInfo(codeGenerationSpec.FilePath);
            // Wenn z.B. in Visual Studio der Inhalt einer Datei gelöscht wird, dann hat die Datei auf Grund der 
            // trotzdem geschriebenen BOM eine Länge von bis zu 4 Byte.
            // Es dürfte super unwahrscheinlich sein, dass eine Datei ohne BOM, aber troztzdem mit sinnvollen 4
            // Byte Inhalt existiert.
            // Deshalb gehen wir hier davon aus, dass jede Datei mit einer Länge kleiner als 4 Bytes de facto leer ist.
            return fileInfo.Length <= 4;
        }

        // => condition == OverwriteCondition.ContentChanged

        // Das Neuschreiben wurde per Order die Mufti angeordnet
        if (Options.Force) {
            return true;
        }

        // Ansonsten wird die Datei nur neu geschrieben, wenn sich deren Inhalt de facto geändert hat.
        var fileContent = File.ReadAllText(codeGenerationSpec.FilePath);

        return !String.Equals(fileContent, codeGenerationSpec.Content, StringComparison.Ordinal);
    }

}