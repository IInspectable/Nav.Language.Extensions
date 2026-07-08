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

    // Continuation- und Choice-Fixtures des V2-Korpus (unter Regression\Tests\V2\) brauchen den
    // V2-Continuation- bzw. -Choice-Codegen (S6/S7), der noch fehlt; bis dahin bleiben sie reine
    // Referenz-Eingaben und werden hier übersprungen, damit die Regression grün bleibt. BasicFlow
    // (CallContext-Grundform) generiert dagegen ab dem V2-Gerüst (S5) und hat seine .expected.cs.
    static readonly string[] PendingVersion2Fixtures = { "ContinuationFlow", "ChoiceFlow" };

    static bool IsPendingVersion2Corpus(string rootDirectory, string filePath) {

        var relative = PathHelper.GetRelativePath(rootDirectory, filePath);
        var underV2  = relative.StartsWith(@"V2\", System.StringComparison.OrdinalIgnoreCase) ||
                       relative.StartsWith("V2/",  System.StringComparison.OrdinalIgnoreCase);
        if (!underV2) {
            return false;
        }

        // Sowohl die .nav-Eingabe als auch alle daraus generierten .cs tragen den Fixture-Namen im
        // Dateinamen (z.B. ContinuationFlow.nav, ContinuationFlowWFSBase.generated.cs).
        var fileName = Path.GetFileName(filePath);
        return PendingVersion2Fixtures.Any(f => fileName.IndexOf(f, System.StringComparison.OrdinalIgnoreCase) >= 0);
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