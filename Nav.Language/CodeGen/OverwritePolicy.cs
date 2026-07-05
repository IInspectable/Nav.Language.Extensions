namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Legt fest, ob eine bereits vorhandene Zieldatei eines <see cref="CodeGenerationSpec"/> beim
/// Generieren überschrieben werden darf.
/// </summary>
public enum OverwritePolicy {

    /// <summary>
    /// Die Datei wird nur einmalig angelegt und danach nie überschrieben (z.B. Benutzer-Dateien
    /// und TO-Stubs, deren Inhalt außerhalb des Generators gepflegt wird).
    /// </summary>
    Never,

    /// <summary>
    /// Die Datei wird neu geschrieben, sobald sich ihr Inhalt tatsächlich geändert hat
    /// (die rein generierten Artefakte).
    /// </summary>
    WhenChanged

}