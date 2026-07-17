#region Using Directives

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes;

/// <summary>
/// Die Priorität eines <see cref="CodeFix"/> innerhalb eines Vorschlags-Sets. Höhere Werte werden zuerst
/// angeboten: Die VS-Extension sortiert die Aktionen einer Position absteigend nach diesem Wert (siehe
/// <see cref="CodeFix.Prio"/>).
/// </summary>
public enum CodeFixPrio {

    /// <summary>Keine besondere Priorität.</summary>
    None,
    /// <summary>Niedrige Priorität — nachrangig angeboten.</summary>
    Low,
    /// <summary>Mittlere Priorität.</summary>
    Medium,
    /// <summary>Hohe Priorität — bevorzugt angeboten.</summary>
    High,

}