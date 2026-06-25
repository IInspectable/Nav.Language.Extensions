#region Using Directives

using System.Linq;

using Pharmatechnik.Nav.Language;
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

    public NavMcpWorkspace(string root) {
        Root = root;
    }

    /// <summary>Workspace-Wurzel (für künftige solution-weite Tools wie find-references/workspace-symbols).</summary>
    public string Root { get; }

    /// <summary>
    /// Validiert eine einzelne <c>.nav</c>-Datei und liefert ihre Diagnostics (inkl. Cross-File-Diagnostics aus
    /// inkludierten Dateien, die beim Bauen des semantischen Modells aufgelöst werden). Nutzt die gemeinsame
    /// Engine-Host-Schicht (<see cref="NavWorkspaceCore"/> + <see cref="DiagnosticsComputer"/>) — dieselbe wie der LSP-Server.
    /// </summary>
    public NavValidateResult Validate(string path) {

        var normalizedPath = PathHelper.NormalizePath(path) ?? path;

        // Frisch von Platte: der Agent hat die Datei evtl. gerade editiert (MCP hält keine Overlays).
        _core.InvalidateCache(normalizedPath);

        var unit = _core.GetCodeGenerationUnit(normalizedPath);
        if (unit == null) {
            return NavValidateResult.NotFound(path);
        }

        var diagnostics = DiagnosticsComputer.FromUnit(unit, normalizedPath)
                                             .Select(diagnostic => NavDiagnosticDto.From(diagnostic, normalizedPath))
                                             .ToList();

        return NavValidateResult.From(path, diagnostics);
    }
}
