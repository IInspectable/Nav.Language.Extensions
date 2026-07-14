#region Using Directives

using System.Text.Json.Serialization;

using Pharmatechnik.Nav.Language.Mcp.Tools;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp;

/// <summary>
/// Source-generierter JSON-Metadaten-Kontext für die Tool-Ergebnis-DTOs (die 14 <c>Nav*Result</c>-Typen;
/// verschachtelte DTOs zieht der Generator transitiv mit). Notwendig fürs Trimming: getrimmt schaltet
/// <c>PublishTrimmed</c> den reflektionsbasierten System.Text.Json-Serializer ab
/// (<c>JsonSerializer.IsReflectionEnabledByDefault=false</c>), sodass der MCP-Server ohne diese
/// Metadaten beim Erzeugen der Tool-Ausgabeschemata mit <c>NotSupportedException</c> abbräche.
/// <para>
/// <see cref="JsonSourceGenerationMode.Metadata"/>: die Serialisierung läuft über die allgemeine
/// Konverter-Maschinerie, die die zur Laufzeit übergebenen <c>JsonSerializerOptions</c> respektiert —
/// der Kontext wird in eine Kopie von <c>McpJsonUtilities.DefaultOptions</c> eingehängt (siehe
/// <c>Program</c>), sodass Casing/Ignore-Verhalten identisch zum untrimmt-reflektierten Pfad bleiben.
/// Namensrichtlinie und Null-Behandlung sind zusätzlich hier fixiert, damit sie unabhängig von den
/// Zieloptionen stimmen.
/// </para>
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy    = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition  = JsonIgnoreCondition.WhenWritingNull,
    GenerationMode          = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(NavValidateResult))]
[JsonSerializable(typeof(NavDiagnosticsResult))]
[JsonSerializable(typeof(NavOutlineResult))]
[JsonSerializable(typeof(NavWorkspaceResult))]
[JsonSerializable(typeof(NavFindSymbolResult))]
[JsonSerializable(typeof(NavGotoResult))]
[JsonSerializable(typeof(NavReferencesResult))]
[JsonSerializable(typeof(NavRenameResult))]
[JsonSerializable(typeof(NavCodeActionsResult))]
[JsonSerializable(typeof(NavFormatResult))]
[JsonSerializable(typeof(NavGrammarResult))]
[JsonSerializable(typeof(NavPreviewCodegenResult))]
[JsonSerializable(typeof(NavCallHierarchyResult))]
[JsonSerializable(typeof(NavExitUsagesResult))]
internal sealed partial class NavMcpJsonSerializerContext: JsonSerializerContext {
}
