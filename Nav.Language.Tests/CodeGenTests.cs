#region Using Directives

using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.CSharp;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.CodeGen;

using NUnit.Framework;

using Pharmatechnik.Nav.Language.Text;

using RoslynDiagnostic = Microsoft.CodeAnalysis.Diagnostic;
using RoslynSyntaxTree = Microsoft.CodeAnalysis.SyntaxTree;
using RoslynDiagnosticSeverity = Microsoft.CodeAnalysis.DiagnosticSeverity;
using Diagnostic = Pharmatechnik.Nav.Language.Diagnostic;
using DiagnosticSeverity = Pharmatechnik.Nav.Language.DiagnosticSeverity;

#endregion

namespace Nav.Language.Tests; 

[TestFixture]
public class CodeGenTests {

    [Test]
    public void SimpleCodegenTest() {

        var codeGenerationUnitSyntax = Syntax.ParseCodeGenerationUnit(Resources.TaskA, filePath: MkFilename("TaskA.nav"));
        var codeGenerationUnit       = CodeGenerationUnit.FromCodeGenerationUnitSyntax(codeGenerationUnitSyntax);

        var options       = GenerationOptions.Default;
        var codeGenerator = new CodeGenerator(options);

        var results = codeGenerator.Generate(codeGenerationUnit);

        Assert.That(results.Length, Is.EqualTo(1));

        var codeGenResult = results[0];

        Assert.That(codeGenResult.IBeginWfsCodeSpec.Content, Is.Not.Empty);
        Assert.That(codeGenResult.IWfsCodeSpec.Content,      Is.Not.Empty);
        Assert.That(codeGenResult.WfsBaseCodeSpec.Content,   Is.Not.Empty);
        Assert.That(codeGenResult.WfsCodeSpec.Content,       Is.Not.Empty);

        Assert.That(codeGenResult.IBeginWfsCodeSpec.IsEmpty, Is.False);
        Assert.That(codeGenResult.IWfsCodeSpec.IsEmpty,      Is.False);
        Assert.That(codeGenResult.WfsBaseCodeSpec.IsEmpty,   Is.False);
        Assert.That(codeGenResult.WfsCodeSpec.IsEmpty,       Is.False);
    }

    [Test]
    public void NullableContextDisabledByDefault() {

        var codeGenResult = GenerateTaskA(GenerationOptions.Default);

        // Default: aus → keine '#nullable'-Direktive in der generierten Datei. Dadurch erbt sie
        // den Nullable-Kontext des Consumer-Projekts (in der Praxis aus), statt non-nullable
        // Referenztyp-Annotationen in Consumer-Builds zu propagieren (CS8604/CS8625).
        Assert.That(codeGenResult.WfsBaseCodeSpec.Content,   Does.Not.Contain("#nullable"));
        Assert.That(codeGenResult.IBeginWfsCodeSpec.Content, Does.Not.Contain("#nullable"));
        Assert.That(codeGenResult.IWfsCodeSpec.Content,      Does.Not.Contain("#nullable"));
    }

    [Test]
    public void NullableContextEnabledWhenOptedIn() {

        var codeGenResult = GenerateTaskA(GenerationOptions.Default with { NullableContext = true });

        Assert.That(codeGenResult.WfsBaseCodeSpec.Content,   Does.Contain("#nullable enable"));
        Assert.That(codeGenResult.WfsBaseCodeSpec.Content,   Does.Not.Contain("#nullable disable"));
        Assert.That(codeGenResult.IBeginWfsCodeSpec.Content, Does.Contain("#nullable enable"));
        Assert.That(codeGenResult.IWfsCodeSpec.Content,      Does.Contain("#nullable enable"));
    }

    static CodeGenerationResult GenerateTaskA(GenerationOptions options) {

        var codeGenerationUnitSyntax = Syntax.ParseCodeGenerationUnit(Resources.TaskA, filePath: MkFilename("TaskA.nav"));
        var codeGenerationUnit       = CodeGenerationUnit.FromCodeGenerationUnitSyntax(codeGenerationUnitSyntax);

        var codeGenerator = new CodeGenerator(options);
        var results       = codeGenerator.Generate(codeGenerationUnit);

        Assert.That(results.Length, Is.EqualTo(1));

        return results[0];
    }

    [Test]
    public void SimpleCodegenOnlyIwflTest() {

        var codeGenerationUnitSyntax = Syntax.ParseCodeGenerationUnit(Resources.TaskA, filePath: MkFilename("TaskA.nav"));
        var codeGenerationUnit       = CodeGenerationUnit.FromCodeGenerationUnitSyntax(codeGenerationUnitSyntax);

        var options       = GenerationOptions.Default with { GenerateWflClasses = false };
        var codeGenerator = new CodeGenerator(options);

        var results = codeGenerator.Generate(codeGenerationUnit);

        Assert.That(results.Length, Is.EqualTo(1));

        var codeGenResult = results[0];

        Assert.That(codeGenResult.IBeginWfsCodeSpec.Content, Is.Empty);
        Assert.That(codeGenResult.IWfsCodeSpec.Content,      Is.Not.Empty);
        Assert.That(codeGenResult.WfsBaseCodeSpec.Content,   Is.Empty);
        Assert.That(codeGenResult.WfsCodeSpec.Content,       Is.Empty);

        Assert.That(codeGenResult.IBeginWfsCodeSpec.IsEmpty, Is.True);
        Assert.That(codeGenResult.IWfsCodeSpec.IsEmpty,      Is.False);
        Assert.That(codeGenResult.WfsBaseCodeSpec.IsEmpty,   Is.True);
        Assert.That(codeGenResult.WfsCodeSpec.IsEmpty,       Is.True);
    }

    [Test]
    public void SimpleCodegenOnlyWflTest() {

        var codeGenerationUnitSyntax = Syntax.ParseCodeGenerationUnit(Resources.TaskA, filePath: MkFilename("TaskA.nav"));
        var codeGenerationUnit       = CodeGenerationUnit.FromCodeGenerationUnitSyntax(codeGenerationUnitSyntax);

        var options       = GenerationOptions.Default with { GenerateIwflClasses = false };
        var codeGenerator = new CodeGenerator(options);

        var results = codeGenerator.Generate(codeGenerationUnit);

        Assert.That(results.Length, Is.EqualTo(1));

        var codeGenResult = results[0];

        Assert.That(codeGenResult.IBeginWfsCodeSpec.Content, Is.Not.Empty);
        Assert.That(codeGenResult.IWfsCodeSpec.Content,      Is.Empty);
        Assert.That(codeGenResult.WfsBaseCodeSpec.Content,   Is.Not.Empty);
        Assert.That(codeGenResult.WfsCodeSpec.Content,       Is.Not.Empty);

        Assert.That(codeGenResult.IBeginWfsCodeSpec.IsEmpty, Is.False);
        Assert.That(codeGenResult.IWfsCodeSpec.IsEmpty,      Is.True);
        Assert.That(codeGenResult.WfsBaseCodeSpec.IsEmpty,   Is.False);
        Assert.That(codeGenResult.WfsCodeSpec.IsEmpty,       Is.False);
    }

    public static TestCaseData[] CompileTestCases = {

        new(
            new TestCase {
                NavFiles = {
                    new TestCaseFile {FilePath = MkFilename("TaskA.nav"), Content = Resources.TaskA}
                }
            }
        ) {
            TestName = "TaskA should be compilable"
        },
        new(new TestCase {
            NavFiles = {
                new TestCaseFile {FilePath = MkFilename($"{nameof(Resources.TaskA)}.nav"), Content = Resources.TaskB}
            }
        }) {
            TestName = "TaskB should be compilable"
        },
        new(new TestCase {
            NavFiles = {
                new TestCaseFile {FilePath = MkFilename($"{nameof(Resources.TaskA)}.nav"), Content = Resources.TaskA},
                new TestCaseFile {FilePath = MkFilename($"{nameof(Resources.TaskB)}.nav"), Content = Resources.TaskB}
            }
        }) {
            TestName = "TaskA and TaskB should be compilable at the same time"
        },
        new(new TestCase {
            NavFiles = {
                new TestCaseFile {FilePath = MkFilename($"{nameof(Resources.SingleFileNav)}.nav"), Content = Resources.SingleFileNav},
            }
        }) {
            TestName = "TestNavGeneratorOnSingleFile"
        },
        new(new TestCase {
            NavFiles = {
                new TestCaseFile {FilePath = MkFilename($"{nameof(Resources.TaskA)}.nav"), Content = Resources.TaskA},
                new TestCaseFile {FilePath = MkFilename($"{nameof(Resources.TaskB)}.nav"), Content = Resources.TaskB},
                new TestCaseFile {FilePath = MkFilename($"{nameof(Resources.TaskC)}.nav"), Content = Resources.TaskC},
            }
        }) {
            TestName = "Task C depends on Task A and Task B"
        },
        new(new TestCase {
            NavFiles = {
                new TestCaseFile {FilePath = MkFilename($"{nameof(Resources.NestedChoices)}.nav"), Content = Resources.NestedChoices},
            }
        }) {
            TestName = "Nested choices"
        },
        new(new TestCase {
            NavFiles = {
                new TestCaseFile {
                    FilePath = MkFilename("TaskA.nav"),
                    Content = @"                           
                            task A [result string] {
                                init i;
                                exit e;
                                i--> e;
                            }"
                }
            }
        }) {
            TestName = "Tasksresult without explizit name"
        },
        new(new TestCase {
            CsFiles = {
                new TestCaseFile {
                    FilePath = MkFilename("FrameworkStubsWithoutNS.cs"),
                    Content  = Resources.FrameworkStubsWithoutNS
                }
            },
            NavFiles = {
                new TestCaseFile {
                    FilePath = MkFilename("TaskA.nav"),
                    Content = @"                           
                        task A{
                            init i;
                            exit e;
                            i --> e;
                        }
                        task B{
                                init i;
                                exit e;
                                exit f;
                                i --> e;
                        }
                        task taskA [code ""public enum MessageBoxResult { Ok, Abbrechen }""]
                            [base StandardWFS : IWFService]
                            [params int foo]

                            [result MessageBoxResult f] 
                            {
                            init I1[params string message];
                            task A;
                            task B;
                            task B c;
                            choice Foo;
                            view TestView;

                            exit Ok;

                            I1    --> TestView;  
    
                            TestView --> A on Ok; 
                            A:e --> Foo;
                            Foo o-> B;
                            Foo --> c;
                            B:e --> Ok;
                            B:f --> Ok;
    
                            c:e --> Ok;
                            c:f --> A;
                            TestView --> Ok on OnFoo; 
                }"
                }
            }
        }) {
            TestName = "Complex Task w/o namespaceprefix"
        }
    };

    [Test, TestCaseSource(nameof(CompileTestCases))]
    public void CompileTest(TestCase testCase) {

        var syntaxProvider         = new TestSyntaxProvider();
        var semenaticModelProvider = SemanticModelProviderFactory.Default.CreateProvider(syntaxProvider);

        // Dateien bekanntgeben - wir haben hier keine echten Dateien zur Hand!
        foreach (var navFile in testCase.NavFiles) {
            syntaxProvider.RegisterFile(navFile);
        }

        var syntaxTrees = new List<RoslynSyntaxTree>();

        foreach (var navFile in testCase.NavFiles) {

            // 1. Syntaxbaum aus Nav-File erstellen
            var codeGenerationUnitSyntax = syntaxProvider.GetSyntax(navFile.FilePath);
            Assert.That(codeGenerationUnitSyntax, Is.Not.Null, $"File '{navFile.FilePath}' not found");
            AssertNoDiagnosticErrors(codeGenerationUnitSyntax.SyntaxTree.Diagnostics, codeGenerationUnitSyntax.SyntaxTree.SourceText);

            // 2. Semantic Model erstellen aus Syntax erstellen
            var codeGenerationUnit = semenaticModelProvider.GetSemanticModel(codeGenerationUnitSyntax);
            AssertNoDiagnosticErrors(codeGenerationUnit.Diagnostics, codeGenerationUnitSyntax.SyntaxTree.SourceText);

            var options       = GenerationOptions.Default;
            var codeGenerator = new CodeGenerator(options);

            // 3. Code aus Semantic Model erstellen
            var codeGenerationResults = codeGenerator.Generate(codeGenerationUnit);

            foreach (var codeGenerationResult in codeGenerationResults) {

                // 4. C#-Syntaxbäume des generierten Codes mittels Roslyn erstellen
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(codeGenerationResult.IBeginWfsCodeSpec.Content, path: codeGenerationResult.IBeginWfsCodeSpec.FilePath));
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(codeGenerationResult.IWfsCodeSpec.Content,      path: codeGenerationResult.IWfsCodeSpec.FilePath));
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(codeGenerationResult.WfsBaseCodeSpec.Content,   path: codeGenerationResult.WfsBaseCodeSpec.FilePath));
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(codeGenerationResult.WfsCodeSpec.Content,       path: codeGenerationResult.WfsCodeSpec.FilePath));
                foreach (var toCodeSpec in codeGenerationResult.ToCodeSpecs) {
                    syntaxTrees.Add(CSharpSyntaxTree.ParseText(toCodeSpec.Content, path: toCodeSpec.FilePath));
                }
            }
        }

        // Pseudo Framework Code hinzufügen
        syntaxTrees.Add(GetFrameworkStubCode());
        foreach (var csFile in testCase.CsFiles) {
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(csFile.Content, path: csFile.FilePath));
        }

        foreach (var syntaxTree in syntaxTrees) {
            AssertNoDiagnosticErrors(syntaxTree.GetDiagnostics());
        }

        // 6. C# Compilation In-Memmory erstellen
        string assemblyName = Path.GetRandomFileName();
        MetadataReference[] references = {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ImmutableArray).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var  ms     = new MemoryStream();
        EmitResult result = compilation.Emit(ms);

        if (!result.Success) {
            AssertNoDiagnosticErrors(result.Diagnostics);
        }

    }

    RoslynSyntaxTree GetFrameworkStubCode() {
        return CSharpSyntaxTree.ParseText(Resources.FrameworkStubsCode, path: MkFilename("FrameworkStubCode.cs"));
    }

    void AssertNoDiagnosticErrors(IEnumerable<RoslynDiagnostic> diagnostics) {
        var errors = diagnostics.Where(d => d.IsWarningAsError || d.Severity == RoslynDiagnosticSeverity.Error).ToList();
        Assert.That(errors.Any(), Is.False, FormatDiagnostics(errors) + errors.FirstOrDefault()?.Location.SourceTree);
    }

    string FormatDiagnostics(IEnumerable<RoslynDiagnostic> diagnostics) {
        return diagnostics.Aggregate(new StringBuilder(), (sb, d) => sb.AppendLine(FormatDiagnostic(d)), sb => sb.ToString());
    }

    static string FormatDiagnostic(RoslynDiagnostic diagnostic) {
        return $"{diagnostic.Id}: {diagnostic.Location} {diagnostic.GetMessage()}";
    }

    void AssertNoDiagnosticErrors(IEnumerable<Diagnostic> diagnostics, SourceText sourceText) {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors.Any(), Is.False, FormatDiagnostics(errors) + sourceText.Text);
    }

    string FormatDiagnostics(IEnumerable<Diagnostic> diagnostics) {
        return diagnostics.Aggregate(new StringBuilder(), (sb, d) => sb.AppendLine(FormatDiagnostic(d)), sb => sb.ToString());
    }

    static string FormatDiagnostic(Diagnostic diagnostic) {
        return $"{diagnostic.Descriptor.Id}: {diagnostic.Location} {diagnostic.Message}";
    }

    static string MkFilename(string fileName) {
        return Path.Combine(@"n:\av", fileName);
    }

    public class TestCase {

        public List<TestCaseFile> NavFiles { get; } = new();
        public List<TestCaseFile> CsFiles  { get; } = new();

    }

}