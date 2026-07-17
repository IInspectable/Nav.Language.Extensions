#region Using Directives

using System;
using System.IO;
using System.Text;

using Pharmatechnik.Nav.Language.Mcp;

#endregion

namespace Nav.Language.Mcp.Tests.Infrastructure;

/// <summary>
/// Test-Fixture für die MCP-Tools: legt ein eindeutiges Temp-Wurzelverzeichnis an, in das
/// <c>.nav</c>-Fixtures geschrieben werden, und stellt einen darauf zeigenden <see cref="NavMcpWorkspace"/>
/// bereit. Da der MCP-Server rein request/response gegen den Stand auf Platte arbeitet (kein Overlay,
/// Cache-Invalidierung pro Datei), sind hingeschriebene Temp-Dateien der natürliche Fixture-Mechanismus —
/// kein Mocking nötig. <see cref="Dispose"/> räumt das Temp-Verzeichnis rekursiv wieder ab.
/// </summary>
public sealed class McpTestWorkspace: IDisposable {

    // UTF-8 mit BOM — die Repo-Konvention für Nav-Quelldateien; der Lexer verträgt beides, aber wir
    // schreiben Fixtures bewusst so, wie der Editor sie ablegen würde.
    static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    /// <summary>Das eindeutige Temp-Wurzelverzeichnis dieses Workspace (Discovery-Basis).</summary>
    public string Root { get; }

    NavMcpWorkspace? _workspace;

    public McpTestWorkspace() {
        Root = Path.Combine(Path.GetTempPath(), "nav-mcp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>
    /// Der auf <see cref="Root"/> zeigende <see cref="NavMcpWorkspace"/>. Lazy angelegt, damit Fixtures
    /// vor der ersten Solution-Discovery geschrieben werden können.
    /// </summary>
    public NavMcpWorkspace Workspace => _workspace ??= new NavMcpWorkspace(Root);

    /// <summary>
    /// Schreibt eine <c>.nav</c>-Fixture (UTF-8 mit BOM) unter dem angegebenen relativen Pfad; nötige
    /// Unterordner werden angelegt. Liefert den absoluten Pfad der geschriebenen Datei zurück — genau die
    /// Form, die an die datei-gebundenen MCP-Tools weitergereicht wird.
    /// </summary>
    public string WriteFile(string relativePath, string navContent) {
        var absolutePath = Path.Combine(Root, relativePath);

        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory)) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(absolutePath, navContent, Utf8WithBom);
        return absolutePath;
    }

    /// <summary>Löscht das Temp-Wurzelverzeichnis rekursiv (fehlertolerant — ein hängendes Handle darf den Testlauf nicht kippen).</summary>
    public void Dispose() {
        try {
            if (Directory.Exists(Root)) {
                Directory.Delete(Root, recursive: true);
            }
        } catch (IOException) {
            // Best effort — Temp wird ohnehin vom OS aufgeräumt.
        } catch (UnauthorizedAccessException) {
            // dito
        }
    }

}
