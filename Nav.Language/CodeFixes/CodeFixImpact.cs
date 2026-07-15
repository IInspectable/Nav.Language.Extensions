namespace Pharmatechnik.Nav.Language.CodeFixes;

/// <summary>
/// Die Tragweite der Änderung, die ein <see cref="CodeFix"/> bewirkt — ein Maß dafür, wie stark sich der
/// Fix auf den Quelltext auswirkt. Die VS-Extension leitet daraus u.a. das Warn-Icon eines Vorschlags ab
/// (siehe <see cref="CodeFix.Impact"/>).
/// </summary>
public enum CodeFixImpact {

    /// <summary>Keine nennenswerte Auswirkung (z.B. rein lokale, verlustfreie Umbenennung).</summary>
    None,
    /// <summary>Mittlere Auswirkung.</summary>
    Medium,
    /// <summary>Hohe Auswirkung — der Nutzer sollte das Ergebnis prüfen.</summary>
    High

}