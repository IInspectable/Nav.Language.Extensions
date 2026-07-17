#region Using Directives

using System;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Liefert das semantische Modell (<see cref="CodeGenerationUnit"/>) zu einer <c>.nav</c>-Datei oder zu
/// einem bereits geparsten Syntaxbaum — die Stufe über dem <see cref="ISyntaxProvider"/>.
/// Implementierungen können das Ergebnis zwischenspeichern (<see cref="CachedSemanticModelProvider"/>).
/// </summary>
public interface ISemanticModelProvider: IDisposable {

    /// <summary>
    /// Liefert das semantische Modell zur Datei <paramref name="filePath"/>, oder <c>null</c>, wenn die
    /// Datei nicht existiert.
    /// </summary>
    /// <param name="filePath">Der Pfad der <c>.nav</c>-Datei.</param>
    /// <param name="cancellationToken">Token zum Abbrechen.</param>
    CodeGenerationUnit? GetSemanticModel(string filePath, CancellationToken cancellationToken = default);
    /// <summary>
    /// Baut das semantische Modell zum bereits geparsten <paramref name="syntax"/> auf.
    /// </summary>
    /// <param name="syntax">Der Syntaxbaum, aus dem das Modell entsteht.</param>
    /// <param name="cancellationToken">Token zum Abbrechen.</param>
    CodeGenerationUnit  GetSemanticModel(CodeGenerationUnitSyntax syntax, CancellationToken cancellationToken = default);

}