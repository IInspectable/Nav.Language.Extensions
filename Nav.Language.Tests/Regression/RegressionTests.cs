#region Using Directives

using System.Collections.Generic;
using System.IO;
using System.Linq;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.Generator;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Nav.Language.Tests; 

[TestFixture]
public class RegressionTests {

    [Test, TestCaseSource(nameof(GetFileTestCases))]
    public void TestCase(FileTestCase pair) {

        var generatedContent = File.ReadAllText(pair.GeneratedFile);
        var expectedContent  = File.ReadAllText(pair.ExpectedFile);

        Assert.That(generatedContent, Is.EqualTo(expectedContent), $"File '{pair.GeneratedFile}' differes from expected file content '{pair.ExpectedFile}'");
    }

    [Test, Explicit]
    public void GenerateFiles() {
        GenerateNavCode();
    }

    public static IEnumerable<FileTestCase> GetFileTestCases() {

        GenerateNavCode();

        return PlainGetFileTestCases();
    }

    static void GenerateNavCode() {

        // Sicherstellen, dass auch wirklich alle Files (auch die "OneShots") neu geschrieben werden.
        foreach(var tc in PlainGetFileTestCases()) {
            File.Delete(tc.GeneratedFile);
        }

        var fileSpecs = CollectNavFiles();

        var pipeline = NavCodeGeneratorPipeline.CreateDefault();
        var result   = pipeline.Run(fileSpecs);

        Assert.That(result.Succeeded, Is.True);
    }

    static IEnumerable<FileTestCase> PlainGetFileTestCases() {

        var directory = GetRegressiontestDirectory();

        var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                             .Where(f => !f.EndsWith("expected.cs"))
                             .Where(f => !IsPendingVersion2Corpus(directory, f));

        return files.Select(f => new FileTestCase {
                GeneratedFile = Path.GetFullPath(f),
                ExpectedFile  = Path.GetFullPath(Path.ChangeExtension(f, "expected.cs"))
            }
        );
    }

    static IEnumerable<FileSpec> CollectNavFiles() {
        var directory    = GetRegressiontestDirectory();
        var navFiles     = Directory.EnumerateFiles(directory, "*.nav", SearchOption.AllDirectories)
                                    .Where(file => !IsPendingVersion2Corpus(directory, file));
        var dirFileSpecs = navFiles.Select(file => new FileSpec(identity: PathHelper.GetRelativePath(directory, file), fileName: file));

        return dirFileSpecs;
    }

    // Der V2-Golden-Input-Korpus liegt bereits unter Regression\Tests\V2\, kann aber noch nicht
    // durch die Pipeline laufen: Sprachversion 2 ist noch nicht freigeschaltet und die V2-Syntax
    // (o-^ / --^ / choice-Parameter) sowie der V2-Codegenerator fehlen. Bis der V2-Generator steht,
    // sind diese Dateien reine Referenz-Eingaben — hier übersprungen, damit die V1-Regression grün
    // bleibt. Sobald V2 generiert, entfällt dieser Filter und der Teilbaum erhält seine .expected.cs.
    static bool IsPendingVersion2Corpus(string rootDirectory, string filePath) {
        var relative = PathHelper.GetRelativePath(rootDirectory, filePath);
        return relative.StartsWith(@"V2\", System.StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith("V2/",  System.StringComparison.OrdinalIgnoreCase);
    }

    static string GetRegressiontestDirectory() {

        return TestDataDirectory.Resolve(@"Regression\Tests");
    }

    public class FileTestCase {

        public string GeneratedFile { get; set; }
        public string ExpectedFile  { get; set; }

        public string RelativeGeneratedFile => PathHelper.GetRelativePath(GetRegressiontestDirectory(), GeneratedFile);
        public string RelativeExpectedFile  => PathHelper.GetRelativePath(GetRegressiontestDirectory(), ExpectedFile);

        public override string ToString() {
            return $"{RelativeGeneratedFile} <-?-> {RelativeExpectedFile}";
        }

    }

}