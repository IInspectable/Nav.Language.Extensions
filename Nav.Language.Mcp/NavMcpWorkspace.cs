#region Using Directives

using System;
using System.Collections.Generic;
using System.Linq;

using Pharmatechnik.Nav.Language;
using Pharmatechnik.Nav.Language.Mcp.Tools;
using Pharmatechnik.Nav.Utilities.IO;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp;

/// <summary>
/// Hält den Nav-Kontext für den MCP-Server. Anders als der LSP-Server (Push-Modell mit Overlay für
/// ungespeicherte Editor-Puffer) arbeitet MCP rein request/response gegen den Stand auf Platte: der
/// KI-Agent editiert die Datei und fragt danach den Zustand ab. Deshalb genügt der nicht-cachende
/// <see cref="SyntaxProvider.Default"/> — jeder Aufruf liest die Datei (und ihre Includes) frisch von
/// Platte, sodass eine gerade geschriebene Änderung sofort sichtbar ist.
/// </summary>
public sealed class NavMcpWorkspace {

    // Bewusst der nicht-cachende Default-Provider (liest bei jedem GetSyntax neu von Platte) — siehe oben.
    readonly ISemanticModelProvider _semanticModelProvider = new SemanticModelProvider(SyntaxProvider.Default);

    public NavMcpWorkspace(string root) {
        Root = root;
    }

    /// <summary>Workspace-Wurzel (für künftige solution-weite Tools wie find-references/workspace-symbols).</summary>
    public string Root { get; }

    /// <summary>
    /// Validiert eine einzelne <c>.nav</c>-Datei und liefert ihre Diagnostics (inkl. Cross-File-Diagnostics
    /// aus inkludierten Dateien, die beim Bauen des semantischen Modells frisch von Platte aufgelöst werden).
    /// </summary>
    public NavValidateResult Validate(string path) {

        var normalizedPath = PathHelper.NormalizePath(path) ?? path;

        var unit = _semanticModelProvider.GetSemanticModel(normalizedPath);
        if (unit == null) {
            return NavValidateResult.NotFound(path);
        }

        // Logik bewusst deckungsgleich zu Nav.Language.Server.DiagnosticsComputer.FromUnit gehalten
        // (Syntax- + semantische Diagnostics, auf das Dokument gefiltert, stabil sortiert). TODO: bei
        // Verstetigung des MCP-Servers diese Berechnung in einen gemeinsamen Engine-Host-Layer ziehen,
        // den LSP- und MCP-Server teilen ("eine Engine").
        var diagnostics = unit.Syntax.SyntaxTree.Diagnostics
                              .Concat(unit.Diagnostics)
                              .SelectMany(diagnostic => diagnostic.ExpandLocations())
                              .Where(diagnostic => BelongsToDocument(diagnostic, normalizedPath))
                              .OrderBy(diagnostic => diagnostic.Location.Start)
                              .Select(diagnostic => NavDiagnosticDto.From(diagnostic, normalizedPath))
                              .ToList();

        return NavValidateResult.From(path, diagnostics);
    }

    static bool BelongsToDocument(Diagnostic diagnostic, string normalizedPath) {

        var locationPath = diagnostic.Location.NormalizedFilePath;

        // Diagnostics ohne Pfad stammen aus dem aktuell geparsten Dokument.
        if (string.IsNullOrEmpty(locationPath)) {
            return true;
        }

        return string.Equals(locationPath, normalizedPath, StringComparison.OrdinalIgnoreCase);
    }
}
