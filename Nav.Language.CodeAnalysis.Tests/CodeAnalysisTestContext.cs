#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.CodeGen;
using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Language.CodeAnalysis.Annotation;
using Pharmatechnik.Nav.Language.CodeAnalysis.FindReferences;
using Pharmatechnik.Nav.Language.FindReferences;

using Location           = Pharmatechnik.Nav.Language.Location;
using DiagnosticSeverity = Pharmatechnik.Nav.Language.DiagnosticSeverity;
using VersionStamp       = Microsoft.CodeAnalysis.VersionStamp;

#endregion

namespace Nav.Language.CodeAnalysis.Tests;

/// <summary>
/// Baut aus einem Inline-<c>.nav</c> die komplette Roslyn-Bühne, gegen die die C#-GoTo-Features von
/// <c>Nav.Language.CodeAnalysis</c> ohne Visual Studio getestet werden:
/// <list type="number">
///   <item>parst das <c>.nav</c> und erzeugt daraus — versionsbewusst (V1/V2) — echten C#-Code;</item>
///   <item>legt die generierten Artefakte (inkl. der konkreten <c>{Task}WFS</c> mit ihren
///         Override-Stubs) plus Framework-/TO-Stubs als Dokumente in einen <see cref="AdhocWorkspace"/>;</item>
///   <item>stellt das Nav-Semantikmodell, das Roslyn-<see cref="Project"/> und Lese-Helfer bereit,
///         mit denen Tests Sprungziele als Klartext prüfen.</item>
/// </list>
/// </summary>
public sealed class CodeAnalysisTestContext {

    // Fake-Pfade (kein Dateisystemzugriff) — analog zum @"n:\av\…"-Pun der übrigen Engine-Tests.
    const string NavPath = @"n:\av\a.nav";
    const string GenDir  = @"n:\av\gen";

    readonly Dictionary<string, string> _generatedTextByPath;

    CodeAnalysisTestContext(CodeGenerationUnit unit,
                            string navSource,
                            AdhocWorkspace workspace,
                            Project project,
                            Dictionary<string, string> generatedTextByPath) {
        Unit                 = unit;
        NavSource            = navSource;
        Workspace            = workspace;
        Project              = project;
        _generatedTextByPath = generatedTextByPath;
    }

    /// <summary>Das Nav-Semantikmodell (Quelle für Choice-/Task-Symbole).</summary>
    public CodeGenerationUnit Unit { get; }

    /// <summary>Der ungeschnittene <c>.nav</c>-Quelltext (Ziel der C#→Nav-Rücksprünge).</summary>
    public string NavSource { get; }

    public AdhocWorkspace Workspace { get; }

    /// <summary>Das Roslyn-Projekt über dem generierten C#-Code — Eingang für den <c>LocationFinder</c>.</summary>
    public Project Project { get; }

    public Solution Solution => Project.Solution;

    #region FromNav

    /// <param name="navSource">Der Inline-<c>.nav</c>-Quelltext.</param>
    /// <param name="userCode">
    /// Optionaler, nutzerseitiger C#-Code (z.B. eine <c>partial class {Task}WFS</c>, die einen
    /// <c>next.{Choice}(…)</c>-Forward aufruft). Nötig für die Aufrufstellen-Pfade: die generierten
    /// Override-Stubs rufen die Forwards nicht auf, also entsteht die <c>&lt;NavChoiceCall&gt;</c>-fähige
    /// Aufrufstelle erst durch Nutzer-Code.
    /// </param>
    public static CodeAnalysisTestContext FromNav(string navSource, string userCode = null) {

        if (navSource == null) {
            throw new ArgumentNullException(nameof(navSource));
        }

        // 1. Nav parsen und Semantikmodell bilden — beide Stufen müssen fehlerfrei sein.
        var syntax = Syntax.ParseCodeGenerationUnit(navSource, filePath: NavPath);
        AssertNoErrors(syntax.SyntaxTree.Diagnostics, navSource);

        var unit = CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax);
        AssertNoErrors(unit.Diagnostics, navSource);

        // 2. Versionsbewusst generieren (V2-Units über den CallContext-Codegen, sonst V1).
        var options = GenerationOptions.Default;
        ImmutableArray<CodeGenerationResult> results = unit.LanguageVersion == NavLanguageVersion.Version2
            ? new CodeGeneratorV2(options).Generate(unit)
            : new CodeGeneratorV1(options).Generate(unit);

        // 3. AdhocWorkspace mit den generierten Artefakten + Stubs bestücken.
        var generatedTextByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var documentInfos       = new List<DocumentInfo>();
        var projectId           = ProjectId.CreateNewId();

        void AddDocument(string path, string content) {
            generatedTextByPath[path] = content;
            var textAndVersion = TextAndVersion.Create(
                Microsoft.CodeAnalysis.Text.SourceText.From(content), VersionStamp.Create(), path);
            documentInfos.Add(DocumentInfo.Create(
                                  id      : DocumentId.CreateNewId(projectId),
                                  name    : Path.GetFileName(path),
                                  loader  : TextLoader.From(textAndVersion),
                                  filePath: path));
        }

        foreach (var result in results) {
            foreach (var spec in result.Specs) {
                // Dateiname behalten (bestimmt WFS/WFSBase/generated-Unterscheidung), unter GenDir verankern.
                AddDocument(Path.Combine(GenDir, Path.GetFileName(spec.FilePath)), spec.Content);
            }
        }

        // Die TO-Typen liefert in Produktion der externe GUI-Generator; fürs In-Memory-Kompilieren des
        // generierten Codes werden sie — wie der Framework-Stub — als Quelltext beigelegt.
        AddDocument(Path.Combine(GenDir, "ToStubCode.cs"),        GetToStubCode(unit));
        AddDocument(Path.Combine(GenDir, "FrameworkStubCode.cs"), Resources.FrameworkStubsCode);

        if (!String.IsNullOrEmpty(userCode)) {
            AddDocument(Path.Combine(GenDir, "UserCode.cs"), userCode);
        }

        var references = new MetadataReference[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ImmutableArray).Assembly.Location),
        };

        var projectInfo = ProjectInfo.Create(
            id                : projectId,
            version           : VersionStamp.Create(),
            name              : "NavGenTest",
            assemblyName      : "NavGenTest",
            language          : LanguageNames.CSharp,
            compilationOptions: new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            documents         : documentInfos,
            metadataReferences: references);

        var workspace = new AdhocWorkspace();
        var project   = workspace.AddProject(projectInfo);

        return new CodeAnalysisTestContext(unit, navSource, workspace, project, generatedTextByPath);
    }

    #endregion

    #region Symbol-/Annotation-Zugriff

    /// <summary>Der Choice-Knoten mit dem angegebenen Namen (aus dem Nav-Semantikmodell).</summary>
    public IChoiceNodeSymbol Choice(string name) {
        return Unit.TaskDefinitions
                   .SelectMany(t => t.NodeDeclarations.OfType<IChoiceNodeSymbol>())
                   .Single(n => n.Name == name);
    }

    /// <summary>Die versionsrichtige <see cref="ChoiceCodeInfo"/> zum Choice-Knoten (Nav→C#-Anker).</summary>
    public ChoiceCodeInfo ChoiceInfo(string name) {
        return ChoiceCodeInfo.FromChoiceNode(Choice(name));
    }

    /// <summary>Liest alle Nav-Annotationen aus dem generierten C#-Code (über den echten <see cref="AnnotationReader"/>).</summary>
    public IReadOnlyList<NavTaskAnnotation> ReadAnnotations() {
        var annotations = new List<NavTaskAnnotation>();
        foreach (var document in Project.Documents) {
            annotations.AddRange(AnnotationReader.ReadNavTaskAnnotations(document));
        }

        return annotations;
    }

    /// <summary>Die <c>&lt;NavChoice&gt;</c>-Annotation der <c>{Choice}Logic</c> mit dem angegebenen Choice-Namen.</summary>
    public NavChoiceAnnotation ChoiceAnnotation(string choiceName) {
        return ReadAnnotations().OfType<NavChoiceAnnotation>().First(a => a.ChoiceName == choiceName);
    }

    /// <summary>Die erste <c>&lt;NavChoiceCall&gt;</c>-Annotation eines <c>{Choice}(…)</c>-Forwards mit dem angegebenen Choice-Namen.</summary>
    public NavChoiceCallAnnotation ChoiceCallAnnotation(string choiceName) {
        return ReadAnnotations().OfType<NavChoiceCallAnnotation>().First(a => a.ChoiceName == choiceName);
    }

    /// <summary>
    /// Die C#-„Referenzen" einer Choice: die <c>{Choice}(…)</c>-Forward-Aufrufstellen im generierten Code,
    /// wie sie <see cref="WfsReferenceFinder"/> über den Roslyn-Workspace findet.
    /// </summary>
    public IReadOnlyList<ReferenceItem> FindChoiceReferences(string choiceName) {

        var context = new CollectingFindReferencesContext();
        var args    = new FindReferencesArgs(
            originatingSymbol            : Choice(choiceName),
            originatingCodeGenerationUnit: Unit,
            solution                     : NavSolution.Empty,
            context                      : context);

        WfsReferenceFinder.FindReferencesAsync(Solution, args).GetAwaiter().GetResult();

        return context.References;
    }

    #endregion

    #region Assertion-Helfer

    /// <summary>Der Quelltext an der Location — aufgelöst gegen den generierten C#-Code oder das <c>.nav</c>.</summary>
    public string TextAt(Location location) {
        if (location == null) {
            throw new ArgumentNullException(nameof(location));
        }

        var source = location.FilePath != null && _generatedTextByPath.TryGetValue(location.FilePath, out var generated)
            ? generated
            : NavSource;

        return source.Substring(location.Start, location.Length);
    }

    /// <summary>Die Location zeigt in generierten C#-Code.</summary>
    public bool IsInGeneratedCSharp(Location location) {
        return location?.FilePath != null && _generatedTextByPath.ContainsKey(location.FilePath);
    }

    /// <summary>Die Location zeigt zurück ins <c>.nav</c>.</summary>
    public bool IsInNav(Location location) => !IsInGeneratedCSharp(location);

    /// <summary>Die Location zeigt in die konkrete, nutzerseitige <c>{Task}WFS</c> (nicht die generierte Basis).</summary>
    public bool IsInConcreteWfs(Location location) {
        var path = location?.FilePath;
        return path != null &&
               path.EndsWith("WFS.cs",           StringComparison.Ordinal) &&
              !path.EndsWith(".generated.cs",     StringComparison.Ordinal);
    }

    /// <summary>Löst einen generierten Typ über seinen Metadaten-Namen in der Kompilation auf (oder <c>null</c>).</summary>
    public INamedTypeSymbol ResolveGeneratedType(string metadataName) {
        var compilation = Project.GetCompilationAsync().GetAwaiter().GetResult();
        return compilation.GetTypeByMetadataName(metadataName);
    }

    /// <summary>Der voll qualifizierte Name der generierten <c>{Task}WFSBase</c>, die den Choice trägt.</summary>
    public string WfsBaseFullyQualifiedName(string choiceName) {
        return ChoiceInfo(choiceName).ContainingTask.FullyQualifiedWfsBaseName;
    }

    /// <summary>Compiler-Fehler des generierten Codes (leer ⇒ die Bühne kompiliert sauber).</summary>
    public IReadOnlyList<string> GeneratedCompilationErrors() {
        var compilation = Project.GetCompilationAsync().GetAwaiter().GetResult();
        return compilation.GetDiagnostics()
                          .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                          .Select(d => d.ToString())
                          .ToList();
    }

    #endregion

    #region Interna

    static void AssertNoErrors(IEnumerable<Pharmatechnik.Nav.Language.Diagnostic> diagnostics, string navSource) {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.That(errors, Is.Empty,
                    "Unerwartete Nav-Diagnosen:\n" +
                    String.Join("\n", errors.Select(e => $"{e.Descriptor.Id}: {e.Message}")) +
                    "\n--- Quelle ---\n" + navSource);
    }

    // Erzeugt je referenziertem GUI-Knoten eine 'partial class {View}TO : TO' im IWFL-Namespace des
    // Tasks — dieselbe Knoten-Auswahl wie der TO-Stub in Nav.Language.Tests\CodeGenTests.
    static string GetToStubCode(CodeGenerationUnit codeGenerationUnit) {

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

        return sb.ToString();
    }

    #endregion

    /// <summary>Sammelt die vom <see cref="WfsReferenceFinder"/> gemeldeten Referenzen (statt sie an eine UI zu reichen).</summary>
    sealed class CollectingFindReferencesContext: IFindReferencesContext {

        public List<ReferenceItem>   References  { get; } = new();
        public List<DefinitionItem>  Definitions { get; } = new();

        public CancellationToken CancellationToken => CancellationToken.None;

        public Task SetSearchTitleAsync(string title)   => Task.CompletedTask;
        public Task ReportMessageAsync(string message)  => Task.CompletedTask;

        public Task OnDefinitionFoundAsync(DefinitionItem definitionItem) {
            Definitions.Add(definitionItem);
            return Task.CompletedTask;
        }

        public Task OnReferenceFoundAsync(ReferenceItem referenceItem) {
            References.Add(referenceItem);
            return Task.CompletedTask;
        }
    }
}
