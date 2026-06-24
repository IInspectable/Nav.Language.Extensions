#region Using Directives

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Pharmatechnik.Nav.Language;

using Lsp = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Server;

/// <summary>
/// Hält den aktuellen Nav-Workspace (alle *.nav-Dateien unterhalb der rootUri) und veröffentlicht
/// Diagnostics für sämtliche Dateien. Cross-File-Auflösung samt Caching liefert der
/// <see cref="CachedSyntaxProvider"/> der <see cref="NavSolution"/>. Das Overlay-Modell
/// (offenes Dokument schlägt Platte) folgt in Milestone 2.3.
/// </summary>
class NavWorkspace {

    NavSolution _solution = NavSolution.Empty;

    public int FileCount => _solution.SolutionFiles.Length;

    public async Task LoadAsync(string? rootPath, CancellationToken cancellationToken) {

        if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath)) {
            _solution = NavSolution.Empty;
            return;
        }

        _solution = await NavSolution.FromDirectoryAsync(new DirectoryInfo(rootPath), cancellationToken);
    }

    public Task PublishAllDiagnosticsAsync(Func<Uri, Lsp.Diagnostic[], Task> publishAsync, CancellationToken cancellationToken) {

        return _solution.ProcessCodeGenerationUnitsAsync(
            asyncAction: async unit => {

                var fullPath = unit.Syntax.SyntaxTree.SourceText.FileInfo?.FullName;
                if (string.IsNullOrEmpty(fullPath)) {
                    return;
                }

                var navDiagnostics = DiagnosticsComputer.FromUnit(unit, fullPath);
                var lspDiagnostics = navDiagnostics.Select(LspMapper.ToLsp).ToArray();

                await publishAsync(new Uri(fullPath), lspDiagnostics);
            },
            startingUnit: null,
            cancellationToken: cancellationToken);
    }
}
