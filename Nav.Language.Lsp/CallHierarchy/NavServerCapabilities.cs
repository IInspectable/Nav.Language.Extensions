#region Using Directives

using Newtonsoft.Json;

using Protocol = Microsoft.VisualStudio.LanguageServer.Protocol;

#endregion

namespace Pharmatechnik.Nav.Language.Lsp.CallHierarchy;

/// <summary>
/// Erweitert die Paket-<see cref="Protocol.ServerCapabilities"/> um Capabilities, die das verwendete
/// Protokoll-Paket 17.2.8 noch nicht kennt — derzeit <c>callHierarchyProvider</c>. Newtonsoft serialisiert
/// die Properties der konkreten Laufzeit-Klasse, also auch diese; der explizite <see cref="JsonPropertyAttribute"/>
/// -Name überlebt den CamelCase-Resolver (<c>OverrideSpecifiedNames = false</c>, s. Program.cs).
/// </summary>
sealed class NavServerCapabilities: Protocol.ServerCapabilities {

    [JsonProperty("callHierarchyProvider")]
    public bool CallHierarchyProvider { get; set; }

}
