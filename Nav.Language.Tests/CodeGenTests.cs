#region Using Directives

using System;
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
        var codeGenerator = new CodeGeneratorV1(options);

        var results = codeGenerator.Generate(codeGenerationUnit);

        Assert.That(results.Length, Is.EqualTo(1));

        var codeGenResult = results[0];

        // Default-Optionen erzeugen für TaskA die vier WFL-/IWFL-Artefakte.
        Assert.That(codeGenResult.Specs.Any(IsIBeginWfs), Is.True);
        Assert.That(codeGenResult.Specs.Any(IsIWfs),      Is.True);
        Assert.That(codeGenResult.Specs.Any(IsWfsBase),   Is.True);
        Assert.That(codeGenResult.Specs.Any(IsWfs),       Is.True);

        Assert.That(codeGenResult.Specs.All(spec => !String.IsNullOrEmpty(spec.Content)), Is.True);
    }

    [Test]
    public void NullableContextDisabledByDefault() {

        var codeGenResult = GenerateTaskA(GenerationOptions.Default);

        // Default: aus → keine '#nullable'-Direktive in der generierten Datei. Dadurch erbt sie
        // den Nullable-Kontext des Consumer-Projekts (in der Praxis aus), statt non-nullable
        // Referenztyp-Annotationen in Consumer-Builds zu propagieren (CS8604/CS8625).
        Assert.That(codeGenResult.Specs.Single(IsWfsBase).Content,   Does.Not.Contain("#nullable"));
        Assert.That(codeGenResult.Specs.Single(IsIBeginWfs).Content, Does.Not.Contain("#nullable"));
        Assert.That(codeGenResult.Specs.Single(IsIWfs).Content,      Does.Not.Contain("#nullable"));
    }

    [Test]
    public void NullableContextEnabledWhenOptedIn() {

        var codeGenResult = GenerateTaskA(GenerationOptions.Default with { NullableContext = true });

        Assert.That(codeGenResult.Specs.Single(IsWfsBase).Content,   Does.Contain("#nullable enable"));
        Assert.That(codeGenResult.Specs.Single(IsWfsBase).Content,   Does.Not.Contain("#nullable disable"));
        Assert.That(codeGenResult.Specs.Single(IsIBeginWfs).Content, Does.Contain("#nullable enable"));
        Assert.That(codeGenResult.Specs.Single(IsIWfs).Content,      Does.Contain("#nullable enable"));
    }

    static CodeGenerationResult GenerateTaskA(GenerationOptions options) {

        var codeGenerationUnitSyntax = Syntax.ParseCodeGenerationUnit(Resources.TaskA, filePath: MkFilename("TaskA.nav"));
        var codeGenerationUnit       = CodeGenerationUnit.FromCodeGenerationUnitSyntax(codeGenerationUnitSyntax);

        var codeGenerator = new CodeGeneratorV1(options);
        var results       = codeGenerator.Generate(codeGenerationUnit);

        Assert.That(results.Length, Is.EqualTo(1));

        return results[0];
    }

    [Test]
    public void SimpleCodegenOnlyIwflTest() {

        var codeGenerationUnitSyntax = Syntax.ParseCodeGenerationUnit(Resources.TaskA, filePath: MkFilename("TaskA.nav"));
        var codeGenerationUnit       = CodeGenerationUnit.FromCodeGenerationUnitSyntax(codeGenerationUnitSyntax);

        var options       = GenerationOptions.Default with { GenerateWflClasses = false };
        var codeGenerator = new CodeGeneratorV1(options);

        var results = codeGenerator.Generate(codeGenerationUnit);

        Assert.That(results.Length, Is.EqualTo(1));

        var codeGenResult = results[0];

        // Ohne WFL-Klassen bleibt nur das IWFS-Interface übrig.
        Assert.That(codeGenResult.Specs.Any(IsIWfs),      Is.True);
        Assert.That(codeGenResult.Specs.Any(IsIBeginWfs), Is.False);
        Assert.That(codeGenResult.Specs.Any(IsWfsBase),   Is.False);
        Assert.That(codeGenResult.Specs.Any(IsWfs),       Is.False);
    }

    [Test]
    public void SimpleCodegenOnlyWflTest() {

        var codeGenerationUnitSyntax = Syntax.ParseCodeGenerationUnit(Resources.TaskA, filePath: MkFilename("TaskA.nav"));
        var codeGenerationUnit       = CodeGenerationUnit.FromCodeGenerationUnitSyntax(codeGenerationUnitSyntax);

        var options       = GenerationOptions.Default with { GenerateIwflClasses = false };
        var codeGenerator = new CodeGeneratorV1(options);

        var results = codeGenerator.Generate(codeGenerationUnit);

        Assert.That(results.Length, Is.EqualTo(1));

        var codeGenResult = results[0];

        // Ohne IWFL-Klassen fehlt nur das IWFS-Interface; IBeginWFS/WFSBase/WFS bleiben.
        Assert.That(codeGenResult.Specs.Any(IsIBeginWfs), Is.True);
        Assert.That(codeGenResult.Specs.Any(IsIWfs),      Is.False);
        Assert.That(codeGenResult.Specs.Any(IsWfsBase),   Is.True);
        Assert.That(codeGenResult.Specs.Any(IsWfs),       Is.True);
    }

    [Test]
    public void DispatcherRoutesVersion1ToCodeGeneratorV1() {

        var codeGenerationUnitSyntax = Syntax.ParseCodeGenerationUnit(Resources.TaskA, filePath: MkFilename("TaskA.nav"));
        var codeGenerationUnit       = CodeGenerationUnit.FromCodeGenerationUnitSyntax(codeGenerationUnitSyntax);

        // TaskA trägt kein #version ⇒ Version 1. Die Weiche (CodeGeneratorProvider.Default) muss für
        // sie exakt dasselbe erzeugen wie der direkt instanziierte V1-Generator.
        Assert.That(codeGenerationUnit.LanguageVersion, Is.EqualTo(NavLanguageVersion.Version1));

        var options = GenerationOptions.Default;

        using var dispatcher = CodeGeneratorProvider.Default.Create(options, PathProviderFactory.Default);
        var       viaSwitch  = dispatcher.Generate(codeGenerationUnit);

        var viaV1 = new CodeGeneratorV1(options).Generate(codeGenerationUnit);

        var switchContents = viaSwitch.SelectMany(r => r.Specs).Select(s => s.Content).ToList();
        var v1Contents     = viaV1.SelectMany(r => r.Specs).Select(s => s.Content).ToList();

        Assert.That(switchContents, Is.EqualTo(v1Contents));
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
        },
        new(new TestCase {
            NavFiles = {
                new TestCaseFile {
                    FilePath = MkFilename("ContinuationCompile.nav"),
                    // V2-Continuation: deckt BEIDE Modi ab — o-^ (→ OpenModalTask) und --^ (→ GotoTask).
                    // Der generierte Code muss gegen die erweiterte .Concat-Typfläche der Stubs
                    // kompilieren (kein Laufzeit-Test, §3.8/⑥). Self-contained: der Folge-Task Msg ist
                    // lokal definiert und [result bool] (kein externer Result-Typ nötig).
                    Content = """
                              #version 2

                              [namespaceprefix Nav.Language.Tests.V2.ContinuationCompile]

                              [using Pharmatechnik.Apotheke.XTplus.Framework.Core.WFL]
                              [using Pharmatechnik.Apotheke.XTplus.Framework.Core.IWFL]

                              task Msg [result bool] {
                                  init I [params string text];
                                  exit Done;
                                  I --> Done;
                              }

                              task ContinuationCompile [base StandardWFS : IWFServiceBase]
                                  [result bool]
                              {
                                  init Init1;
                                  view Home;
                                  task Msg Warn;
                                  task Msg Drill;
                                  exit Ok;

                                  Init1 --> Home;

                                  // o-^ : Home zeigen, dann modal Warn obendrauf → GotoGUI(to).Concat(OpenModalTask(...))
                                  Home --> Home o-^ Warn on OnShowWarn;

                                  // --^ : Home zeigen, per Goto in Drill → GotoGUI(to).Concat(GotoTask(...))
                                  Home --> Home --^ Drill on OnDrillDown;

                                  Warn:Done  --> Home;
                                  Drill:Done --> Home;
                                  Home --> Ok on OnClose;
                              }
                              """
                }
            }
        }) {
            TestName = "V2 Continuation (o-^ and --^) should compile against stubs"
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

            var options = GenerationOptions.Default;

            // 3. Code aus Semantic Model erstellen — versionsbewusst (V2-Units über den CallContext-Codegen,
            //    sonst V1). Entspricht der Dispatcher-Weiche, ohne den internen Dispatcher zu benötigen.
            var codeGenerationResults = codeGenerationUnit.LanguageVersion == NavLanguageVersion.Version2
                ? new CodeGeneratorV2(options).Generate(codeGenerationUnit)
                : new CodeGeneratorV1(options).Generate(codeGenerationUnit);

            foreach (var codeGenerationResult in codeGenerationResults) {

                // 4. C#-Syntaxbäume des generierten Codes mittels Roslyn erstellen
                foreach (var spec in codeGenerationResult.Specs) {
                    syntaxTrees.Add(CSharpSyntaxTree.ParseText(spec.Content, path: spec.FilePath));
                }
            }

            // Die TO-Typen sind kein Generator-Artefakt mehr — in Produktion liefert sie der externe
            // GUI-Generator. Für die In-Memory-Kompilierung des generierten Codes werden sie hier als
            // Stubs bereitgestellt (analog zum Framework-Stub).
            syntaxTrees.Add(GetToStubCode(codeGenerationUnit));
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

    // Erzeugt je View-Knoten eine 'partial class {View}TO : TO' im IWFL-Namespace des Tasks — die
    // Typen, die die generierten I{Task}WFS-/WFSBase-Signaturen referenzieren. nav.exe legt diese
    // Stubs nicht mehr an (der GUI-Generator besitzt den TO-Inhalt); für die Kompilierung des
    // generierten Codes werden sie hier bereitgestellt (dieselbe Knoten-Auswahl wie einst TOCodeModel).
    static RoslynSyntaxTree GetToStubCode(CodeGenerationUnit codeGenerationUnit) {

        var sb = new StringBuilder();
        sb.AppendLine($"using {CodeGenFacts.NavigationEngineIwflNamespace};");

        foreach (var task in codeGenerationUnit.TaskDefinitions) {

            var iwflNamespace = TaskCodeInfo.FromTaskDefinition(task).IwflNamespace;

            var toClassNames = task.NodeDeclarations
                                   .OfType<IGuiNodeSymbol>()
                                   .Where(guiNode => guiNode.References.Any())
                                   .Select(guiNode => $"{guiNode.Name.ToPascalcase()}{CodeGenInvariants.ToClassNameSuffix}")
                                   .Distinct()
                                   .ToList();

            if (toClassNames.Count == 0) {
                continue;
            }

            sb.AppendLine($"namespace {iwflNamespace} {{");
            foreach (var toClassName in toClassNames) {
                sb.AppendLine($"    public partial class {toClassName} : TO {{ }}");
            }

            sb.AppendLine("}");
        }

        return CSharpSyntaxTree.ParseText(sb.ToString(), path: MkFilename("ToStubCode.cs"));
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

    // Die Produktions-Specs sind bewusst namenlos (nur Content/FilePath/OverwritePolicy) — die
    // Artefakt-Menge ist eine Sache des Generators, nicht des Konvergenzpunkts. Die Tests sind die
    // einzige Stelle, die ein bestimmtes Artefakt adressieren muss; sie tun das über den Dateinamen.
    static string FileNameOf(CodeGenerationSpec spec) => Path.GetFileName(spec.FilePath);

    static bool IsIBeginWfs(CodeGenerationSpec spec) => FileNameOf(spec).StartsWith("IBegin", StringComparison.Ordinal);
    static bool IsIWfs(CodeGenerationSpec spec)      => FileNameOf(spec).StartsWith("I",      StringComparison.Ordinal) && !IsIBeginWfs(spec);
    static bool IsWfsBase(CodeGenerationSpec spec)   => FileNameOf(spec).EndsWith("WFSBase.generated.cs", StringComparison.Ordinal);
    static bool IsWfs(CodeGenerationSpec spec)       => FileNameOf(spec).EndsWith("WFS.cs", StringComparison.Ordinal) &&
                                                        !FileNameOf(spec).EndsWith(".generated.cs", StringComparison.Ordinal);

    public class TestCase {

        public List<TestCaseFile> NavFiles { get; } = new();
        public List<TestCaseFile> CsFiles  { get; } = new();

    }

}