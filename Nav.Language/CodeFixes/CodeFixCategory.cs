namespace Pharmatechnik.Nav.Language.CodeFixes;

/// <summary>
/// Die fachliche Familie, in die ein <see cref="CodeFix"/> fällt. Die Kategorie steuert, wie ein Host den
/// Fix gruppiert und anbietet: Der LSP-Server bildet sie auf einen CodeAction-Kind ab, der MCP-Server
/// unterscheidet damit <c>refactor</c> von <c>quickfix</c>, und die VS-Extension bündelt gleichkategorige
/// Aktionen zu einem Vorschlags-Set.
/// </summary>
public enum CodeFixCategory {

    /// <summary>Neutrale Standard-Kategorie ohne besondere fachliche Zuordnung.</summary>
    CodeFix,
    /// <summary>Behebt eine gemeldete Fehler-Diagnose (Basis <see cref="ErrorFix.ErrorCodeFix"/>).</summary>
    ErrorFix,
    /// <summary>Stil-/Aufräum-Fix (Basis <see cref="StyleFix.StyleCodeFix"/>) — etwa ungenutzte Deklarationen entfernen oder fehlende Semikola ergänzen.</summary>
    StyleFix,
    /// <summary>Umgestaltung ohne Fehlerbezug (Basis <see cref="Refactoring.RefactoringCodeFix"/>) — etwa Umbenennen oder Einführen einer Choice.</summary>
    Refactoring

}