#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Ergebnis von <c>nav_goto</c>: die Definition(en) des angefragten Namens. Ist der Name mehrdeutig
/// (z.B. ein Knotenname in mehreren Tasks), bleibt <see cref="Locations"/> leer und <see cref="Candidates"/>
/// listet die Treffer, damit der Agent per <c>task</c>-Parameter eingrenzen kann.
/// </summary>
public sealed class NavGotoResult {

    public string Path { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>Gesetzt bei „nicht gefunden" oder „mehrdeutig" (dann ggf. <see cref="Candidates"/>).</summary>
    public string? Error { get; set; }

    public List<NavLocationDto> Locations { get; set; } = new();

    /// <summary>Bei Mehrdeutigkeit: die in Frage kommenden Symbole.</summary>
    public List<NavSymbolRef> Candidates { get; set; } = new();
}

/// <summary>
/// Ergebnis von <c>nav_references</c>: alle solution-weiten Vorkommen des angefragten Symbols (die
/// Deklaration trägt <c>isDeclaration = true</c>). Mehrdeutigkeit wird wie bei <see cref="NavGotoResult"/>
/// über <see cref="Candidates"/> aufgelöst.
/// </summary>
public sealed class NavReferencesResult {

    public string Path { get; set; } = "";
    public string Name { get; set; } = "";

    public string? Error { get; set; }

    /// <summary>Anzahl der Vorkommen (inkl. Deklaration).</summary>
    public int Count { get; set; }

    public List<NavLocationDto> Locations { get; set; } = new();

    public List<NavSymbolRef> Candidates { get; set; } = new();
}
