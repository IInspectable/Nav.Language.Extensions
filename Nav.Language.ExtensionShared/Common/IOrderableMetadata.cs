#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// MEF-Metadaten, die eine benannte Erweiterung relativ zu anderen einordnen und so vom
/// <see cref="ExtensionOrderer"/> topologisch sortiert werden können.
/// </summary>
interface IOrderableMetadata {
    /// <summary>Eindeutiger Name der Erweiterung, über den sie referenziert wird.</summary>
    string                Name   { get; }
    /// <summary>Namen der Erweiterungen, die nach dieser Erweiterung einzuordnen sind.</summary>
    IReadOnlyList<string> Before { get; }
    /// <summary>Namen der Erweiterungen, die vor dieser Erweiterung einzuordnen sind.</summary>
    IReadOnlyList<string> After  { get; }
}