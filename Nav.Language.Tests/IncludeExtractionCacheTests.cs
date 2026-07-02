using System;
using System.IO;
using System.Linq;
using System.Threading;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;

namespace Nav.Language.Tests;

/// <summary>
/// Tests für die gecachte Deklarations-Extraktion inkludierter Dateien
/// (<c>TaskDeclarationSymbolBuilder</c>): Mehrere inkludierende Dateien teilen sich die
/// Extraktion derselben Include-Syntax, dürfen sich aber über die dabei geklonten
/// Deklarations-Symbole nicht gegenseitig beeinflussen.
/// </summary>
[TestFixture]
public class IncludeExtractionCacheTests {

    const string Lib = @"
task Sub
{
    init i;
    exit x;
    i --> x;
}
";

    static string Consumer(string taskName) => $@"
taskref ""lib.nav"";

task {taskName}
{{
    init i;
    task Sub s;
    exit e;
    i   --> s;
    s:x --> e;
}}
";

    [Test]
    public void IncludedTaskDeclarationsAreIsolatedPerIncludingFile() {

        using var tmp = new TempDir();
        tmp.Write("lib.nav", Lib);
        tmp.Write("a.nav", Consumer("A"));
        tmp.Write("b.nav", Consumer("B"));

        // Ein gemeinsamer CachedSyntaxProvider → beide Konsumenten sehen dieselbe
        // Syntax-Instanz von lib.nav, die zweite Extraktion kommt aus dem Cache.
        var provider = new CachedSyntaxProvider();
        var unitA    = Load(tmp, provider, "a.nav");
        var unitB    = Load(tmp, provider, "b.nav");

        var declA = unitA.Includes.Single().TaskDeclarations["Sub"];
        var declB = unitB.Includes.Single().TaskDeclarations["Sub"];

        // Jede inkludierende Datei bekommt ihr eigenes Deklarations-Symbol...
        Assert.That(declA, Is.Not.SameAs(declB));

        // ...dessen References ausschließlich die eigenen Task-Knoten enthalten
        Assert.That(declA.References.Select(r => r.ContainingTask.Name), Is.EqualTo(new[] { "A" }));
        Assert.That(declB.References.Select(r => r.ContainingTask.Name), Is.EqualTo(new[] { "B" }));

        // ...und dessen Connection Points am eigenen Symbol hängen, nicht am gecachten Prototyp
        Assert.That(declA.Exits().Single().TaskDeclaration, Is.SameAs(declA));
        Assert.That(declB.Exits().Single().TaskDeclaration, Is.SameAs(declB));

        // Die Exit-Transition ist in beiden Modellen gegen den jeweils eigenen Klon aufgelöst
        Assert.That(unitA.Diagnostics, Is.Empty);
        Assert.That(unitB.Diagnostics, Is.Empty);
    }

    [Test]
    public void IncludeWithErrorsReportsNav0005InEachIncludingFile() {

        using var tmp = new TempDir();
        // Syntaxfehler (fehlendes Semikolon) — die Diagnostics der Include-Datei bestehen aus
        // ihren Syntax-Fehlern plus den Diagnostics der Deklarations-Extraktion
        tmp.Write("lib.nav", @"
task Broken
{
    init i
    exit x;
    i --> x;
}
");
        tmp.Write("a.nav", @"taskref ""lib.nav"";");
        tmp.Write("b.nav", @"taskref ""lib.nav"";");

        var provider = new CachedSyntaxProvider();
        var unitA    = Load(tmp, provider, "a.nav");
        var unitB    = Load(tmp, provider, "b.nav");

        // Auch der zweite Konsument (Extraktion aus dem Cache) sieht die Fehler der Include-Datei
        Assert.That(unitA.Diagnostics.Select(d => d.Descriptor.Id), Has.Member(DiagnosticId.Nav0005));
        Assert.That(unitB.Diagnostics.Select(d => d.Descriptor.Id), Has.Member(DiagnosticId.Nav0005));
    }

    static CodeGenerationUnit Load(TempDir tmp, ISyntaxProvider provider, string fileName) {
        var syntax = provider.GetSyntax(tmp.Path(fileName), CancellationToken.None);
        Assert.That(syntax, Is.Not.Null);
        return CodeGenerationUnit.FromCodeGenerationUnitSyntax(syntax, CancellationToken.None, provider);
    }

    /// <summary>
    /// Echte Dateien im Temp-Verzeichnis — nötig, weil die Include-Auflösung über das
    /// Dateisystem geht (relative Pfade werden gegen das Verzeichnis der Quelldatei aufgelöst).
    /// </summary>
    sealed class TempDir: IDisposable {

        readonly string _dir;

        public TempDir() {
            _dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "navinc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public string Path(string fileName) => System.IO.Path.Combine(_dir, fileName);

        public void Write(string fileName, string content) => File.WriteAllText(Path(fileName), content);

        public void Dispose() {
            try {
                Directory.Delete(_dir, recursive: true);
            } catch {
                // Best effort — Temp-Aufräumen darf den Testlauf nicht zum Scheitern bringen.
            }
        }

    }

}
