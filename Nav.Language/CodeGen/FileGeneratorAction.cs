namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Was der <see cref="FileGenerator"/> mit einer Zieldatei tatsächlich getan hat. Die Codegen-
/// Statistik der Pipeline zählt daran die geschriebenen und übersprungenen Dateien.
/// </summary>
public enum FileGeneratorAction {
    /// <summary>Die Datei blieb unverändert (Inhalt gleich bzw. <see cref="OverwritePolicy.Never"/> und Datei vorhanden).</summary>
    Skiped,
    /// <summary>Die Datei wurde neu geschrieben.</summary>
    Updated,
}