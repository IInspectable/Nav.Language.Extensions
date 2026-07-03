#region Using Directives

using System.Collections.Generic;
using System.Linq;


#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Ergebnis von <c>nav_outline</c>: die Struktur einer <c>.nav</c>-Datei (Task-Definitionen mit ihren Knoten),
/// damit der Agent den Aufbau erfassen kann, ohne die ganze Datei zu lesen. 1-basierte Positionen.
/// </summary>
public sealed class NavOutlineResult {

    public string Path { get; set; } = "";

    /// <summary>Gesetzt, wenn die Datei nicht gefunden/nicht parsebar ist (dann keine Tasks).</summary>
    public string? Error { get; set; }

    public List<NavTaskOutline> Tasks { get; set; } = new();

    public static NavOutlineResult NotFound(string path) => new() {
        Path  = path,
        Error = "Datei nicht gefunden oder nicht als Nav-Datei parsebar."
    };

    public static NavOutlineResult From(string path, CodeGenerationUnit unit) => new() {
        Path  = path,
        Tasks = unit.TaskDefinitions.Select(NavTaskOutline.From).ToList()
    };

}

/// <summary>Eine Task-Definition mit ihren deklarierten Knoten.</summary>
public sealed class NavTaskOutline {

    public string Name { get; set; } = "";

    /// <summary>1-basierte Zeile des Task-Namens.</summary>
    public int Line { get; set; }

    /// <summary>1-basierte Spalte des Task-Namens.</summary>
    public int Column { get; set; }

    public List<NavNodeOutline> Nodes { get; set; } = new();

    public static NavTaskOutline From(ITaskDefinitionSymbol task) => new() {
        Name   = task.Name,
        Line   = task.Location.StartLine      + 1,
        Column = task.Location.StartCharacter + 1,
        Nodes  = task.NodeDeclarations.Select(NavNodeOutline.From).ToList()
    };

}

/// <summary>Ein deklarierter Knoten innerhalb einer Task-Definition.</summary>
public sealed class NavNodeOutline {

    public string Name { get; set; } = "";

    /// <summary>Art des Knotens: init | exit | end | choice | gui | tasknode | node.</summary>
    public string Kind { get; set; } = "";

    public int Line   { get; set; }
    public int Column { get; set; }

    public static NavNodeOutline From(INodeSymbol node) => new() {
        Name   = string.IsNullOrEmpty(node.Name) ? "<node>" : node.Name,
        Kind   = NavSymbolKind.Of(node),
        Line   = node.Location.StartLine      + 1,
        Column = node.Location.StartCharacter + 1
    };

}