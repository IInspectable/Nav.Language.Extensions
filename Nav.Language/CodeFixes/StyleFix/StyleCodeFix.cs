using Pharmatechnik.Nav.Language.Text;
using System.Collections.Generic;

namespace Pharmatechnik.Nav.Language.CodeFixes.StyleFix;

/// <summary>
/// Familien-Basis der Stil-/Aufräum-Fixes (<see cref="CodeFixCategory.StyleFix"/>). Anders als die
/// <see cref="CodeFixCategory.ErrorFix"/>-Familie behebt ein <see cref="StyleCodeFix"/> keinen echten
/// Fehler, sondern räumt stilistisch Unerwünschtes auf: ungenutzte Task-Deklarationen
/// (<see cref="RemoveUnusedTaskDeclarationCodeFix"/>), ungenutzte Knoten
/// (<see cref="RemoveUnusedNodesCodeFix"/>) und ungenutzte Include-Direktiven
/// (<see cref="RemoveUnusedIncludeDirectiveCodeFix"/>) entfernen sowie fehlende Semikola an
/// Include-Direktiven ergänzen (<see cref="AddMissingSemicolonsOnIncludeDirectivesCodeFix"/>). Wie jeder
/// <see cref="CodeFix"/> mutiert der Fix nichts selbst, sondern liefert nur ein Edit-Set aus
/// <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>en (siehe <see cref="GetTextChanges"/>).
/// </summary>
public abstract class StyleCodeFix: CodeFix {

    /// <summary>
    /// Initialisiert die Familien-Basis mit dem <paramref name="context"/>, aus dem der Fix seine Eingaben
    /// (<see cref="CodeGenerationUnit"/>, <see cref="SyntaxTree"/>, Editor-Einstellungen) bezieht.
    /// </summary>
    /// <param name="context">Der auslösende <see cref="CodeFixContext"/>.</param>
    protected StyleCodeFix(CodeFixContext context): base(context) {
    }

    /// <summary>Fest auf <see cref="CodeFixCategory.StyleFix"/> — die Kategorie der gesamten Familie.</summary>
    public sealed override CodeFixCategory Category => CodeFixCategory.StyleFix;

    /// <summary>
    /// Berechnet die zum Aufräumen nötigen <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>s. Die
    /// konkrete Unterklasse bestimmt, was entfernt bzw. eingefügt wird; die Liste ist leer, wenn nichts
    /// anzuwenden ist.
    /// </summary>
    /// <returns>Das Edit-Set des Fixes.</returns>
    public abstract IList<TextChange> GetTextChanges();

}