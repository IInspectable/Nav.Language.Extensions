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
                             .Where(f => !f.EndsWith("expected.cs"));

        return files.Select(f => new FileTestCase {
                GeneratedFile = Path.GetFullPath(f),
                ExpectedFile  = Path.GetFullPath(Path.ChangeExtension(f, "expected.cs"))
            }
        );
    }

    static IEnumerable<FileSpec> CollectNavFiles() {
        var directory    = GetRegressiontestDirectory();
        var navFiles     = Directory.EnumerateFiles(directory, "*.nav", SearchOption.AllDirectories);
        var dirFileSpecs = navFiles.Select(file => new FileSpec(identity: PathHelper.GetRelativePath(directory, file), fileName: file));

        return dirFileSpecs;
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