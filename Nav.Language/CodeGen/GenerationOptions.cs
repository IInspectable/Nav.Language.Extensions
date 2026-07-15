using System;
using System.Text;

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Steuert die C#-Codegenerierung aus einer <c>.nav</c>-Datei: welche Artefakte erzeugt werden,
/// wie ihre Namespaces gebildet werden und wohin die generierten Dateien geschrieben werden.
/// </summary>
/// <remarks>
/// Unveränderlicher Wert (<c>record</c> mit <c>init</c>-Settern). Ausgangspunkt ist
/// <see cref="Default"/>; abweichende Konfigurationen werden per <c>with</c>-Ausdruck abgeleitet.
/// </remarks>
public record GenerationOptions {

    /// <summary>
    /// Die Standardkonfiguration: erzeugt WFL- und IWFL-Klassen, überschreibt unveränderte
    /// Ausgabedateien nicht (<see cref="Force"/> = <c>false</c>) und lässt den
    /// Nullable-Referenztyp-Kontext aus.
    /// </summary>
    public static GenerationOptions Default => new() {
        Force               = false,
        GenerateWflClasses  = true,
        GenerateIwflClasses = true,
    };

    /// <summary>
    /// Überschreibt die Ausgabedatei(en) auch dann, wenn sich ihr Inhalt nicht geändert hat.
    /// Default: <c>false</c> (unveränderte Dateien werden übersprungen).
    /// </summary>
    public bool Force { get; init; }

    /// <summary>
    /// Aktiviert strikte Namespaces: In IWFL-Dateien werden z.B. nur Namespaces mit der Endung
    /// IWFL generiert. Default: <c>false</c>.
    /// </summary>
    public bool Strict { get; init; }

    /// <summary>
    /// Erzeugt die TO-Klassen (Transfer-Objekte): je referenziertem View-Knoten einen einmaligen
    /// <c>partial class {View}TO : TO</c>-Platzhalter, dessen Inhalt der externe GUI-Generator besitzt.
    /// Bewusst <b>opt-in</b>, Default <c>false</c> — nur die wenigen Projekte, die den Stub brauchen,
    /// schalten ihn ein (MSBuild: <c>&lt;NavGenerateToClasses&gt;true&lt;/…&gt;</c>). Nur in Sprach-Generation 1
    /// (V1) wirksam; der V2-Codegenerator erzeugt keine TO-Stubs.
    /// </summary>
    public bool GenerateToClasses { get; init; }

    /// <summary>
    /// Erzeugt die WFL-Klassen (Workflow-Logik: Basis- und abgeleitete Klasse). Default: <c>true</c>.
    /// </summary>
    public bool GenerateWflClasses { get; init; }

    /// <summary>
    /// Erzeugt die IWFL-Klassen (Workflow-Logik-Schnittstelle). Default: <c>true</c>.
    /// </summary>
    public bool GenerateIwflClasses { get; init; }

    /// <summary>
    /// Schreibt <c>#nullable enable</c> in die generierten Dateien (Nullable-Referenztyp-Kontext).
    /// Default: <c>false</c>.
    /// </summary>
    /// <remarks>
    /// Bewusst opt-in, da der Nullable-Kontext non-nullable Referenztyp-Parameter in die generierten
    /// Signaturen propagiert und damit Consumer-Builds brechen kann, die mit möglicherweise-null
    /// aufrufen (CS8604/CS8625).
    /// </remarks>
    public bool NullableContext { get; init; }

    /// <summary>
    /// Das Wurzelverzeichnis des Projekts. Basis, relativ zu der die Namespaces der generierten
    /// Dateien gebildet werden, und Bezugspunkt für <see cref="IwflRootDirectory"/> und
    /// <see cref="WflRootDirectory"/>.
    /// </summary>
    public string ProjectRootDirectory { get; init; } = String.Empty;

    /// <summary>
    /// Ein alternatives Wurzelverzeichnis für die IWFL-Dateien. Setzt
    /// <see cref="ProjectRootDirectory"/> voraus. Bleibt es leer, gilt
    /// <see cref="ProjectRootDirectory"/>.
    /// </summary>
    public string IwflRootDirectory { get; init; } = String.Empty;

    /// <summary>
    /// Ein alternatives Wurzelverzeichnis für die WFL-Dateien. Setzt
    /// <see cref="ProjectRootDirectory"/> voraus. Bleibt es leer, gilt
    /// <see cref="ProjectRootDirectory"/>.
    /// </summary>
    public string WflRootDirectory { get; init; } = String.Empty;

    /// <summary>
    /// Die Kodierung der generierten Dateien. Stets <see cref="System.Text.Encoding.UTF8"/> —
    /// ein anderes Encoding ist nicht vorgesehen.
    /// </summary>
    public Encoding Encoding => Encoding.UTF8;

}