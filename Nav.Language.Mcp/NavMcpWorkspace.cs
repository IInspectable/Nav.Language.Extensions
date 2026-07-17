#region Using Directives

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Pharmatechnik.Nav.Language.Mcp.Tools;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp;

/// <summary>
/// MCP-Schale über dem gemeinsamen <see cref="NavWorkspaceCore"/>. Anders als der LSP-Server (Push-Modell mit
/// Overlay für ungespeicherte Editor-Puffer) arbeitet MCP rein request/response gegen den Stand auf Platte:
/// der KI-Agent editiert die Datei und fragt danach den Zustand ab. Vor dem Lesen wird der Cache der Zieldatei
/// invalidiert, sodass eine gerade geschriebene Änderung sofort sichtbar ist (keine Editor-Overlays nötig).
/// </summary>
public sealed class NavMcpWorkspace {

    readonly NavWorkspaceCore _core = new();

    // Solution-Discovery (globt alle *.nav unter Root) wird nur für solution-weite Tools benötigt und daher
    // lazy + thread-safe einmalig ausgeführt — per-File-Tools (Validate, Outline) brauchen sie nicht.
    readonly SemaphoreSlim _loadGate = new(1, 1);
    bool                   _solutionLoaded;

    // Hierarchische .navignore-Treffer: ignorierte Dateien bleiben in der Solution (weiterhin als Include-Ziel
    // auflösbar und navigierbar), werden aber beim workspace-weiten Diagnostics-Sweep (nav_diagnostics)
    // übersprungen — das Pull-Äquivalent zur LSP-Stummschaltung. Beim Solution-Load einmalig befüllt.
    NavIgnore _ignore = NavIgnore.Empty;

    public NavMcpWorkspace(string root) {
        Root = root;
    }

    /// <summary>Workspace-Wurzel (Discovery-Basis für solution-weite Tools wie find-references/workspace).</summary>
    public string Root { get; }

    /// <summary>Die geladene Solution (alle <c>*.nav</c>) — erst nach <see cref="EnsureSolutionLoadedAsync"/> befüllt.</summary>
    public NavSolution Solution => _core.Solution;

    /// <summary>Anzahl der Dateien in der geladenen Solution.</summary>
    public int FileCount => _core.FileCount;

    /// <summary>Wurzelverzeichnis der geladenen Solution (oder <c>null</c>).</summary>
    public DirectoryInfo? SolutionDirectory => _core.SolutionDirectory;

    /// <summary>
    /// Lädt die Solution (alle <c>*.nav</c> unter <see cref="Root"/>) genau einmal. Idempotent und thread-safe;
    /// solution-weite Tools rufen das vor dem Zugriff auf <see cref="Solution"/>.
    /// </summary>
    public async Task EnsureSolutionLoadedAsync(CancellationToken cancellationToken = default) {

        if (_solutionLoaded) {
            return;
        }

        await _loadGate.WaitAsync(cancellationToken);
        try {
            if (_solutionLoaded) {
                return;
            }

            await _core.LoadAsync(Root, cancellationToken);

            _ignore = _core.SolutionDirectory == null
                ? NavIgnore.Empty
                : NavIgnore.Load(_core.SolutionDirectory.FullName);

            _solutionLoaded = true;
        } finally {
            _loadGate.Release();
        }
    }

    /// <summary>
    /// Ist die Datei durch eine <c>.navignore</c>-Regel vom workspace-weiten Diagnostics-Sweep ausgenommen?
    /// Die Datei bleibt in der Solution (als Include-Ziel auflösbar/navigierbar) — nur <c>nav_diagnostics</c>
    /// überspringt sie, analog zur LSP-Stummschaltung. Vor <see cref="EnsureSolutionLoadedAsync"/> stets
    /// <c>false</c> (dann ist noch keine <c>.navignore</c> geladen).
    /// </summary>
    public bool IsIgnored(string filePath) => _ignore.IsIgnored(filePath);

    /// <summary>
    /// Liefert das frisch von Platte gelesene semantische Modell einer Datei (der Cache der Datei wird vorher
    /// invalidiert — der Agent hat sie evtl. gerade editiert; MCP hält keine Overlays) samt normalisiertem Pfad.
    /// <c>null</c>, wenn die Datei nicht gefunden/nicht parsebar ist.
    /// </summary>
    public CodeGenerationUnit? GetFreshUnit(string path, out string normalizedPath) {

        normalizedPath = PathHelper.NormalizePath(path) ?? path;

        _core.InvalidateCache(normalizedPath);

        return _core.GetCodeGenerationUnit(normalizedPath);
    }

    /// <summary>
    /// Liefert den frisch von Platte gelesenen <see cref="SyntaxTree"/> einer Datei (Cache-Invalidierung wie
    /// bei <see cref="GetFreshUnit"/>) samt normalisiertem Pfad — für rein syntaktische Features wie den
    /// Formatter, die kein Semantik-Modell brauchen. <c>null</c>, wenn die Datei nicht gefunden wird.
    /// </summary>
    public SyntaxTree? GetFreshSyntaxTree(string path, out string normalizedPath) {

        normalizedPath = PathHelper.NormalizePath(path) ?? path;

        _core.InvalidateCache(normalizedPath);

        return _core.GetSyntaxTree(normalizedPath);
    }

    /// <summary>
    /// Validiert eine einzelne <c>.nav</c>-Datei und liefert ihre Diagnostics (inkl. Cross-File-Diagnostics aus
    /// inkludierten Dateien, die beim Bauen des semantischen Modells aufgelöst werden). Nutzt die gemeinsame
    /// Engine-Host-Schicht (<see cref="NavWorkspaceCore"/> + <see cref="DiagnosticsComputer"/>) — dieselbe wie der LSP-Server.
    /// </summary>
    public NavValidateResult Validate(string path) {

        var diagnostics = GetFileDiagnostics(path);
        if (diagnostics == null) {
            return NavValidateResult.NotFound(path);
        }

        return NavValidateResult.From(path, diagnostics);
    }

    /// <summary>
    /// Frisch von Platte gelesene Diagnostics einer einzelnen <c>.nav</c>-Datei als KI-DTOs (1-basierte
    /// Zeilen/Spalten). Wie <see cref="Validate"/> wird der Cache der Datei vorher invalidiert (Fresh-pro-Datei),
    /// sodass eine gerade geschriebene Änderung sofort sichtbar ist. <c>null</c>, wenn die Datei nicht
    /// gefunden/nicht parsebar ist. Gemeinsame Basis von <c>nav_validate</c> (Einzeldatei) und
    /// <c>nav_diagnostics</c> (workspace-weiter Sweep).
    /// </summary>
    public List<NavDiagnosticDto>? GetFileDiagnostics(string path) {

        var unit = GetFreshUnit(path, out var normalizedPath);
        if (unit == null) {
            return null;
        }

        return DiagnosticsComputer.FromUnit(unit, normalizedPath)
                                  .Select(diagnostic => NavDiagnosticDto.From(diagnostic, normalizedPath))
                                  .ToList();
    }

}
