#region Using Directives

using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen; 

public interface IFileGeneratorProvider {

    IFileGenerator Create(GenerationOptions options);

}

public interface IFileGenerator: IDisposable {

    ImmutableArray<FileGeneratorResult> Generate(CodeGenerationResult codeGenerationResult);

}

public sealed class FileGeneratorProvider: IFileGeneratorProvider {

    FileGeneratorProvider() {

    }

    public static readonly IFileGeneratorProvider Default = new FileGeneratorProvider();

    public IFileGenerator Create(GenerationOptions options) {
        return new FileGenerator(options);
    }

}

public class FileGenerator: Generator, IFileGenerator {

    public FileGenerator(GenerationOptions options): base(options) {
    }

    public ImmutableArray<FileGeneratorResult> Generate(CodeGenerationResult codeGenerationResult) {

        if (codeGenerationResult == null) {
            throw new ArgumentNullException(nameof(codeGenerationResult));
        }

        var results = new List<FileGeneratorResult?> {
            WriteFile(codeGenerationResult.TaskDefinition, codeGenerationResult.IWfsCodeSpec,      OverwritePolicy.WhenChanged),
            WriteFile(codeGenerationResult.TaskDefinition, codeGenerationResult.IBeginWfsCodeSpec, OverwritePolicy.WhenChanged),
            WriteFile(codeGenerationResult.TaskDefinition, codeGenerationResult.WfsBaseCodeSpec,   OverwritePolicy.WhenChanged),
            WriteFile(codeGenerationResult.TaskDefinition, codeGenerationResult.WfsCodeSpec,       OverwritePolicy.Never)
        };

        foreach (var toCodeSpec in codeGenerationResult.ToCodeSpecs) {
            results.Add(WriteFile(codeGenerationResult.TaskDefinition, toCodeSpec, OverwritePolicy.Never));
        }

        return results.WhereNotNull()
                      .ToImmutableArray();
    }

    FileGeneratorResult? WriteFile(ITaskDefinitionSymbol taskDefinition, CodeGenerationSpec codeGenerationSpec, OverwritePolicy overwritePolicy) {

        return Resilience.Execute(WriteFileImpl,
                                 maxAttempts: 3,
                                 retryDelay: TimeSpan.FromMilliseconds(10));

        FileGeneratorResult? WriteFileImpl() {

            if (codeGenerationSpec.IsEmpty) {
                return null;
            }

            EnsureDirectory(codeGenerationSpec.FilePath);

            var action = FileGeneratorAction.Skiped;

            if (ShouldWrite(codeGenerationSpec, overwritePolicy)) {
                File.WriteAllText(codeGenerationSpec.FilePath, codeGenerationSpec.Content, Options.Encoding);
                action = FileGeneratorAction.Updated;
            }

            return new FileGeneratorResult(taskDefinition, action, codeGenerationSpec.FilePath);
        }
    }

    static void EnsureDirectory(string fileName) {
        var dir = Path.GetDirectoryName(fileName);
        // ReSharper disable once AssignNullToNotNullAttribute Lass krachen
        Directory.CreateDirectory(dir);
    }

    bool ShouldWrite(CodeGenerationSpec codeGenerationSpec, OverwritePolicy overwritePolicy) {

        // Wenn die Datei nicht existiert, wird sie neu geschrieben
        if (!File.Exists(codeGenerationSpec.FilePath)) {
            return true;
        }

        // Eine Datei mit der Größe 0 gilt als nicht existent, und wird neu geschrieben 
        if (overwritePolicy == OverwritePolicy.Never) {

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

    enum OverwritePolicy {

        Never,
        WhenChanged

    }

}