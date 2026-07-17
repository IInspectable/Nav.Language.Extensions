#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Mcp.Tools;

/// <summary>
/// Ergebnis von <c>nav_preview_codegen</c>: der C#-Code, der aus einer <c>.nav</c>-Datei generiert
/// würde — ohne Plattenschreiben und ohne Build. Bewusst eine schlanke KI-Sicht: je Task-Definition
/// eine Liste der generierten Artefakte (Interfaces, abstrakte Basisklasse, Benutzer-Stub) mit Rolle,
/// Ziel-Dateiname und (optional) Inhalt. Der Agent liest daraus die exakten Methodennamen,
/// Begin-Overloads und vor allem die transitiv erreichbaren DI-Parameter je Logic-Methode ab, statt
/// erst <c>nav.exe</c> laufen zu lassen und die <c>*.generated.cs</c> zu suchen.
/// </summary>
public sealed class NavPreviewCodegenResult {

    /// <summary>Der Pfad der Quell-<c>.nav</c>-Datei (wie übergeben).</summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// Gesetzt, wenn die Datei nicht gefunden/nicht parsebar ist, ein angeforderter Task nicht existiert
    /// oder der Codegen wegen Fehler-Diagnostics nicht möglich ist (dann stehen die Fehler in
    /// <see cref="Diagnostics"/>).
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Die effektive Nav-Sprachversion der Datei (numerisch: <c>1</c>, <c>2</c> …) — bestimmt, welche
    /// Codegen-Generation die Artefakte erzeugt hat.
    /// </summary>
    public int LanguageVersion { get; set; }

    /// <summary>
    /// Fehler-Diagnostics, die den Codegen verhindern (nur gesetzt, wenn <see cref="Error"/> darauf
    /// verweist). Der Codegen läuft erst, wenn die Datei fehlerfrei ist — der Agent behebt zuerst diese
    /// Diagnostics (wie bei <c>nav_validate</c>).
    /// </summary>
    public List<NavDiagnosticDto> Diagnostics { get; set; } = new();

    /// <summary>
    /// <c>true</c>, wenn der Inhalt der Artefakte das Token-Budget gesprengt hätte und daher weggelassen
    /// wurde (nur das Manifest — Rollen, Dateinamen, Zeilen-/Zeichenzahlen — bleibt). Mit <c>task</c>
    /// eingrenzen oder <c>includeContent=false</c> setzen und die interessierende Rolle gezielt nachladen.
    /// </summary>
    public bool ContentOmitted { get; set; }

    /// <summary>Je Task-Definition der Datei die generierten Artefakte.</summary>
    public List<NavPreviewTaskDto> Tasks { get; set; } = new();

    public static NavPreviewCodegenResult NotFound(string path) => new() {
        Path  = path,
        Error = "Datei nicht gefunden oder nicht als Nav-Datei parsebar."
    };

}

/// <summary>Die generierten Artefakte einer einzelnen Task-Definition.</summary>
public sealed class NavPreviewTaskDto {

    /// <summary>Name der Task-Definition.</summary>
    public string Task { get; set; } = "";

    /// <summary>Die generierten C#-Artefakte dieser Task (Reihenfolge wie der Codegen sie liefert).</summary>
    public List<NavPreviewArtifactDto> Artifacts { get; set; } = new();

}

/// <summary>
/// Ein einzelnes generiertes C#-Artefakt: die abstrakte Basisklasse (trägt die Logic-Signaturen inkl.
/// DI-Parameter), ein Interface oder der einmalige Benutzer-Stub.
/// </summary>
public sealed class NavPreviewArtifactDto {

    /// <summary>
    /// Rolle des Artefakts:
    /// <list type="bullet">
    /// <item><c>base</c> — die abstrakte Basisklasse <c>{Task}WFS…Base</c>: trägt die abstrakten
    /// Logic-Methoden inkl. der transitiv erreichbaren DI-Parameter. <b>Der wichtigste Teil.</b></item>
    /// <item><c>iwfs</c> — das Interface <c>I{Task}WFS</c>.</item>
    /// <item><c>ibegin</c> — das Begin-Interface <c>IBegin{Task}WFS</c> (die Begin-Overloads).</item>
    /// <item><c>user</c> — der einmalige Benutzer-Stub <c>{Task}WFS</c> (wird nie überschrieben).</item>
    /// <item><c>to</c> — ein TO-Stub (nur bei aktivierten TO-Klassen).</item>
    /// </list>
    /// </summary>
    public string Role { get; set; } = "";

    /// <summary>Der Ziel-Dateiname des Artefakts (nur der Dateiname, ohne Verzeichnis).</summary>
    public string FileName { get; set; } = "";

    /// <summary>Anzahl der Zeilen des generierten Inhalts.</summary>
    public int LineCount { get; set; }

    /// <summary>Anzahl der Zeichen des generierten Inhalts.</summary>
    public int CharCount { get; set; }

    /// <summary>
    /// Überschreib-Politik des Artefakts: <c>WhenChanged</c> (generiert, wird bei Änderung überschrieben)
    /// oder <c>Never</c> (einmalig angelegt, danach Benutzer-Eigentum — <c>user</c>/<c>to</c>).
    /// </summary>
    public string OverwritePolicy { get; set; } = "";

    /// <summary>
    /// Der generierte C#-Inhalt. <c>null</c>, wenn der Aufrufer ihn abbestellt hat
    /// (<c>includeContent=false</c>) oder er wegen des Token-Budgets weggelassen wurde
    /// (<see cref="NavPreviewCodegenResult.ContentOmitted"/>).
    /// </summary>
    public string? Content { get; set; }

}
