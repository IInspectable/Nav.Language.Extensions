#region Using Directives

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NUnit.Framework;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.CallHierarchy;
using Pharmatechnik.Nav.Language.FindReferences;

#endregion

namespace Nav.Language.Tests.CallHierarchy;

[TestFixture]
public class NavCallHierarchyReproTests {

    [Test]
    public async Task CrossFolder_RelativeInclude_IncomingMatchesReferenceFinder() {

        var root = Path.Combine(Path.GetTempPath(), "navchrepro_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Pflegehilfsmittel"));
        Directory.CreateDirectory(Path.Combine(root, "Verkauf.Shared"));

        var defPath    = Path.Combine(root, "Pflegehilfsmittel", "Def.nav");
        var callerPath = Path.Combine(root, "Verkauf.Shared", "Caller.nav");

        // Definition in Unterordner Pflegehilfsmittel
        File.WriteAllText(defPath,
            "task T\n" +
            "{\n" +
            "    init i;\n" +
            "    exit x;\n" +
            "    i --> x;\n" +
            "}\n");

        // Aufrufer in anderem Ordner, inkludiert via RELATIVEM Pfad mit ..\
        File.WriteAllText(callerPath,
            "taskref \"..\\Pflegehilfsmittel\\Def.nav\";\n" +
            "task C\n" +
            "{\n" +
            "    init i;\n" +
            "    task T t;\n" +
            "    exit e;\n" +
            "    i    --> t;\n" +
            "    t:x  --> e;\n" +
            "}\n");

        try {
            var solution = await NavSolution.FromDirectoryAsync(new DirectoryInfo(root), CancellationToken.None);
            var defUnit  = solution.SemanticModelProvider.GetSemanticModel(defPath, CancellationToken.None);

            ITaskDefinitionSymbol task = null;
            foreach (var t in defUnit.TaskDefinitions) {
                if (t.Name == "T") { task = t; }
            }

            Assert.That(task, Is.Not.Null, "task T nicht gefunden");

            // ReferenceFinder (das, was CodeLens nutzt)
            var collector = new CountingContext();
            await ReferenceFinder.FindReferencesAsync(new FindReferencesArgs(task!, defUnit, solution, collector));

            // Mein Service
            var incoming = await NavCallHierarchyService.GetIncomingCallsAsync(task, solution, CancellationToken.None);

            Assert.That(incoming.Count, Is.GreaterThan(0), "Incoming leer trotz erwarteter Aufrufer!");
        } finally {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    sealed class CountingContext: IFindReferencesContext {
        public int ReferenceCount;
        public CancellationToken CancellationToken => CancellationToken.None;
        public Task OnDefinitionFoundAsync(DefinitionItem definition) => Task.CompletedTask;
        public Task OnReferenceFoundAsync(ReferenceItem reference) { ReferenceCount++; return Task.CompletedTask; }
        public Task ReportProgressAsync(int current, int maximum) => Task.CompletedTask;
        public Task ReportMessageAsync(string message) => Task.CompletedTask;
        public Task SetSearchTitleAsync(string title) => Task.CompletedTask;
    }
}
