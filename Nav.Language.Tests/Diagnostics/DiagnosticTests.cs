#region Using Directives

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

#endregion

namespace Nav.Language.Tests; 

[TestFixture]
public class DiagnosticTests {

    // TODO Nav2000IdentifierExpected vervollständigen

    [Test]
    public void Nav0003SourceFileNeedsToBeSavedBeforeIncludeDirectiveCanBeProcessed() {

        var nav = @"
            taskref ""foo.nav""
            //        ^------^ Nav0003SourceFileNeedsToBeSavedBeforeIncludeDirectiveCanBeProcessed
            task A
            {
                init I1;            
                exit e1;
                I1 --> e1;     
            }
            ";

        var unit = BuildCodeGenerationUnit(nav);

        ExpectExactly(unit, This(DiagnosticDescriptors.Semantic.Nav0003SourceFileNeedsToBeSavedBeforeIncludeDirectiveCanBeProcessed));
    }

    [Test]
    public void Nav0004File0NotFound() {

        var nav = @"
            taskref ""foo.nav""
            /         ^------^ Nav0004File0NotFound
            task A
            {
                init I1;            
                exit e1;
                I1 --> e1;     
            }
            ";

        var unit = BuildCodeGenerationUnit(nav, MkFileName("bar.nav"));

        ExpectExactly(unit, This(DiagnosticDescriptors.Semantic.Nav0004File0NotFound));
    }

    [Test]
    public void Nav0005IncludeFile0HasSomeErrors() {

        var includedNav = @"           
            task A
            {
                init I1;            
                exit e1;
                I1 ---> e1; // 
                //-^- Syntaxfehler
            }
            ";

        var testNav = @"          
            taskref ""includedNav.nav"";
            task B
            {
                init I1;            
                exit e1;
                task A;
                I1   --> A; 
                A:e1 --> e1;
            }
            ";

        var unit = BuildCodeGenerationUnit(new TestCaseFile {Content = testNav, FilePath     = MkFileName(nameof(testNav))},
                                           new TestCaseFile {Content = includedNav, FilePath = MkFileName(nameof(includedNav))});

        ExpectExactly(unit, This(DiagnosticDescriptors.Semantic.Nav0005IncludeFile0HasSomeErrors));
    }

    [Test]
    [Ignore("Dieser Test funktioniert in der Praxis nicht, da er bereits zu einem Syntaxfehler führt.")]
    public void Nav0102EndNodeMustNotContainLeavingEdges() {

        var nav = @"
          
            task A
            {
                init I1;            
                exit e1;
                end;
                
                I1  --> e1;
                end --> e1;
            }
            ";

        var unit = BuildCodeGenerationUnit(nav);
        ExpectExactly(unit, This(DiagnosticDescriptors.Semantic.Nav0102EndNodeMustNotContainLeavingEdges));
    }
        
    [Test, TestCaseSource(nameof(GetTestCases))]
    public void TestCase(FileTestCase testCase) {

        string source = testCase.SourceText();
        var    unit   = BuildCodeGenerationUnit(source, testCase.FilePath);

        var expected = ParseDiagnostics(source);

        var actualDiagnostics = GetActualDiagnostics(unit);
        var actual            = ToUnitTestString(actualDiagnostics);

        Assert.That(actual, Is.EqualTo(expected));
    }

    //[Test, Explicit]
    public void FixTests() {

        // Macht alles Tests grün...
        foreach(var testcase in GetTestCases()) {
            var source = File.ReadAllText(testcase.FilePath);
            var unit   = BuildCodeGenerationUnit(source);

            var actualDiagnostics = GetActualDiagnostics(unit);
            var actual            = ToUnitTestString(actualDiagnostics);

            var linePrefix = Regex.Escape(UnitTestDiagnosticFormatter.LinePrefix);
            var rawSource  = Regex.Replace(source, $"^{linePrefix}.*$", "", RegexOptions.Multiline);
            rawSource = Regex.Replace(rawSource, @"\s+\z", "");

            var newContentBuilder = new StringBuilder();
            newContentBuilder.AppendLine(rawSource);
            foreach(var line in actual) {
                newContentBuilder.AppendLine(line);
            }

            var newContent = newContentBuilder.ToString();

            File.WriteAllText(testcase.FilePath, newContent, Encoding.UTF8);
        }
    }

    static List<Diagnostic> GetActualDiagnostics(CodeGenerationUnit unit) {

        // Diagnostic in der stabilen Reihenfolge Error, Warning, Suggestion => Position => Syntaxfehler, Semantikfehler
        var allDiagnostics = unit.Syntax.SyntaxTree.Diagnostics
                                 .Concat(unit.Diagnostics)
                                 .SelectMany(d => d.ExpandLocations())
                                 .OrderBy(d => d.Location.Start)
                                 .ToList();

        var errors      = allDiagnostics.Errors();
        var warnings    = allDiagnostics.Warnings();
        var suggestions = allDiagnostics.Suggestions();

        var actualDiagnostics = errors.Concat(warnings).Concat(suggestions);
        return actualDiagnostics.ToList();
    }

    public static IEnumerable<FileTestCase> GetTestCases() {

        var directory = GetDiagnosticTestDirectory();
        var navFiles  = Directory.EnumerateFiles(directory, "*.nav", SearchOption.TopDirectoryOnly);
        return navFiles.Select(navFile => new FileTestCase {
            FilePath = navFile,
        });
    }

    #region Infrastructure

    static DiagnosticExpectation This(DiagnosticDescriptor diagnosticDescriptor, int locationCount = 1) {
        return new DiagnosticExpectation(diagnosticDescriptor, locationCount);
    }

    class DiagnosticExpectation {

        public DiagnosticExpectation(DiagnosticDescriptor diagnosticDescriptor, int locationCount = 1) {
            DiagnosticDescriptor = diagnosticDescriptor;
            LocationCount        = locationCount;
        }

        public DiagnosticDescriptor DiagnosticDescriptor { get; }
        public int                  LocationCount        { get; }

    }

    void ExpectExactly(CodeGenerationUnit unit, params DiagnosticExpectation[] diags) {
        ExpectExactly(unit.Diagnostics, diags);
    }

    void ExpectExactly(IReadOnlyList<Diagnostic> diagnostics, params DiagnosticExpectation[] expects) {
        Assert.That(diagnostics.Select(diag => diag.Descriptor), Is.EquivalentTo(expects.Select(e => e.DiagnosticDescriptor)));

        var expectedLocations = diagnostics.Join(expects,
                                                 diag => diag.Descriptor.Id,
                                                 exp => exp.DiagnosticDescriptor.Id,
                                                 (diag, exp) => new {Diagnostic = diag, ExpectedLocations = exp.LocationCount});

        foreach (var x in expectedLocations) {
            Assert.That(x.Diagnostic.GetLocations().Count(), Is.EqualTo(x.ExpectedLocations), x.Diagnostic.ToString());
        }
    }

    static string GetDiagnosticTestDirectory() {

        return TestDataDirectory.Resolve(@"Diagnostics\Tests");
    }

    List<string> ParseDiagnostics(string source) {

        return source.Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.None)
                     .Where(l => l.StartsWith(UnitTestDiagnosticFormatter.LinePrefix))
                     .Select(s => s.TrimEnd())
                     .ToList();
    }

    List<string> ToUnitTestString(IEnumerable<Diagnostic> diagnostics) {
        return diagnostics.Select(diagnostic => diagnostic.ToString(UnitTestDiagnosticFormatter.Instance)).ToList();
    }

    public class DiagnosticResult: IEquatable<DiagnosticResult> {

        public DiagnosticResult(string diagnosticText) {
            DiagnosticText = diagnosticText;

        }

        public string DiagnosticText { get; }

        public override string ToString() {
            return DiagnosticText;
        }

        #region Equality members

        public bool Equals(DiagnosticResult other) {
            if (other is null) {
                return false;
            }

            if (ReferenceEquals(this, other)) {
                return true;
            }

            return string.Equals(DiagnosticText, other.DiagnosticText);
        }

        public override bool Equals(object obj) {
            if (obj is null) {
                return false;
            }

            if (ReferenceEquals(this, obj)) {
                return true;
            }

            if (obj.GetType() != GetType()) {
                return false;
            }

            return Equals((DiagnosticResult) obj);
        }

        public override int GetHashCode() {
            return (DiagnosticText != null ? DiagnosticText.GetHashCode() : 0);
        }

        public static bool operator ==(DiagnosticResult left, DiagnosticResult right) {
            return Equals(left, right);
        }

        public static bool operator !=(DiagnosticResult left, DiagnosticResult right) {
            return !Equals(left, right);
        }

        #endregion

    }

    CodeGenerationUnit BuildCodeGenerationUnit(TestCaseFile navFile, params TestCaseFile[] includes) {

        var syntaxProvider = new TestSyntaxProvider();
        syntaxProvider.RegisterFile(navFile);
        foreach (var include in includes) {
            syntaxProvider.RegisterFile(include);
        }

        var syntax = syntaxProvider.GetSyntax(navFile.FilePath);
        var model  = CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax, syntaxProvider: syntaxProvider);
        return model;
    }

    CodeGenerationUnit BuildCodeGenerationUnit(string source, string filePath = null) {
        var syntax = Syntax.ParseCodeGenerationUnit(source, filePath);
        var model  = CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
        return model;
    }

    public class FileTestCase {

        public string FilePath { get; set; }
        public string SourceText() => File.ReadAllText(FilePath);

        public override string ToString() {
            if (FilePath == null) {
                return "Unknown";
            }

            return Path.GetFileName(FilePath);
        }

    }

    static string MkFileName(string filename) {
        if (String.IsNullOrEmpty(Path.GetExtension(filename))) {
            filename = Path.ChangeExtension(filename, "nav");
        }

        return $@"n:\av\{filename}";
    }

    #endregion

}