namespace Pharmatechnik.Nav.Language.CodeFixes.ErrorFix;

/// <summary>
/// Familien-Basis aller Fixes, die eine <b>Fehler</b>-Diagnose beheben (fehlende Exit-Transition ergänzen,
/// eine deplatzierte <c>#version</c>-Direktive an den Kopf verschieben, auf eine gültige bzw. unterstützte
/// Sprach-Version setzen). Legt für die ganze Familie <see cref="Category"/> auf
/// <see cref="CodeFixCategory.ErrorFix"/> fest; das übrige Edit-Set-Verhalten stammt aus <see cref="CodeFix"/>.
/// Schwesterfamilien sind der Stil-/Aufräum-Zweig (<see cref="StyleFix.StyleCodeFix"/>) und der
/// Umgestaltungs-Zweig (<see cref="Refactoring.RefactoringCodeFix"/>).
/// </summary>
public abstract class ErrorCodeFix: CodeFix {

    /// <summary>Initialisiert die Basis eines Fehler-Fixes mit dem gegebenen <paramref name="context"/>.</summary>
    /// <param name="context">Der Kontext, der den zu behebenden Fehler samt Symbol- und Syntaxzugriff bereitstellt.</param>
    protected ErrorCodeFix(CodeFixContext context): base(context) {
    }

    /// <summary>
    /// Fest auf <see cref="CodeFixCategory.ErrorFix"/> — jeder Fix dieser Familie behebt eine Fehler-Diagnose.
    /// <c>sealed</c>, damit konkrete Fixes die Familienzugehörigkeit nicht umdeklarieren.
    /// </summary>
    public sealed override CodeFixCategory Category => CodeFixCategory.ErrorFix;

}