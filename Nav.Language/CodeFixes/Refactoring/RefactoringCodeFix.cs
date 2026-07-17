namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring;

/// <summary>
/// Gemeinsame Basisklasse der Umgestaltungs-Fixes (Refactorings). Anders als die Style- und
/// Error-Familien behebt ein Refactoring keine Diagnose, sondern bietet an einer Position bzw.
/// Auswahl eine strukturerhaltende Umgestaltung an (z.B. eine Choice einführen oder ein Symbol
/// umbenennen). Legt <see cref="Category"/> fest; die eigentliche Umgestaltung samt der erzeugten
/// <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>s liefern die abgeleiteten Fixes.
/// </summary>
public abstract class RefactoringCodeFix: CodeFix {

    /// <summary>
    /// Initialisiert die Basis mit dem <paramref name="context"/>, der die auszugestaltende Position
    /// bzw. Auswahl samt <see cref="CodeGenerationUnit"/> und Editor-Einstellungen kapselt.
    /// </summary>
    protected RefactoringCodeFix(CodeFixContext context): base(context) {
    }

    /// <summary>
    /// Immer <see cref="CodeFixCategory.Refactoring"/> — fest verankert, da die gesamte Familie
    /// Umgestaltungen anbietet; nicht weiter überschreibbar.
    /// </summary>
    public sealed override CodeFixCategory Category => CodeFixCategory.Refactoring;

}