#region Using Directives

using System;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Liefert den geparsten Syntaxbaum (<see cref="CodeGenerationUnitSyntax"/>) zu einer <c>.nav</c>-Datei —
/// die unterste Stufe der Provider-Kette (Syntax → SemanticModel). Implementierungen können das Ergebnis
/// zwischenspeichern (<see cref="CachedSyntaxProvider"/>) oder aus einem Overlay bedienen.
/// </summary>
public interface ISyntaxProvider : IDisposable {
    /// <summary>
    /// Liefert den Syntaxbaum zur Datei <paramref name="filePath"/>, oder <c>null</c>, wenn die Datei
    /// nicht existiert.
    /// </summary>
    /// <param name="filePath">Der Pfad der zu parsenden <c>.nav</c>-Datei.</param>
    /// <param name="cancellationToken">Token zum Abbrechen des Parse-Vorgangs.</param>
    CodeGenerationUnitSyntax? GetSyntax(string filePath, CancellationToken cancellationToken = default);
}