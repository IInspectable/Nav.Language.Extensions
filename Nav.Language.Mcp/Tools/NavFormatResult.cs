#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Ergebnis von <c>nav_format</c>: die Formatierungs-Änderungen (1-basiert) plus der komplett formatierte
/// Dokumenttext. Es wird NICHTS auf Platte geschrieben — der Agent wendet das Ergebnis selbst an (am
/// einfachsten: die Datei mit <see cref="FormattedText"/> überschreiben). Leere <see cref="Edits"/>
/// bedeuten: das Dokument bzw. der angefragte Zeilenbereich ist bereits kanonisch formatiert.
/// </summary>
public sealed class NavFormatResult {

    public string Path { get; set; } = "";

    /// <summary>Datei nicht gefunden / ungültiger Zeilenbereich / ungültige TabSize.</summary>
    public string? Error { get; set; }

    /// <summary>Die anzuwendenden Textänderungen (leer = bereits formatiert).</summary>
    public List<NavEditDto> Edits { get; set; } = new();

    /// <summary>
    /// Der komplette Dokumenttext nach Anwendung der Änderungen — auch beim Range-Format das ganze Dokument
    /// (mit nur den Änderungen des Bereichs angewendet). <c>null</c>, wenn nichts zu ändern ist oder der
    /// Aufrufer ihn abbestellt hat (<c>includeFormattedText=false</c>).
    /// </summary>
    public string? FormattedText { get; set; }

}
