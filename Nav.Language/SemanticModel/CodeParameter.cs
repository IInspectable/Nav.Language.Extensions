#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Die Implementierung von <see cref="ICodeParameter"/>: entsteht über
/// <see cref="FromResultDeclaration"/> aus der <c>[result …]</c>-Deklaration einer Task-Definition
/// bzw. -Deklaration (Konstruktionsstelle: <see cref="TaskDeclarationSymbolBuilder"/>) und
/// normalisiert fehlende Angaben auf <see cref="string.Empty"/>.
/// </summary>
sealed class CodeParameter: ICodeParameter {

    CodeParameter(string? parameterName, string? parameterType, Location location) {
        ParameterType = parameterType ?? string.Empty;
        ParameterName = parameterName ?? string.Empty;
        Location      = location      ?? throw new ArgumentNullException(nameof(location));
    }

    /// <inheritdoc/>
    public string ParameterName { get; }

    /// <summary>
    /// Die Fundstelle der zugrunde liegenden <c>[result …]</c>-Deklaration im Quelltext.
    /// </summary>
    public Location Location { get; }

    /// <inheritdoc/>
    public string ParameterType { get; }

    /// <summary>
    /// Erzeugt den Code-Parameter aus einer <c>[result …]</c>-Deklaration. Name und Typ werden
    /// als Quelltext-Wiedergabe übernommen; ein fehlender (in der Grammatik optionaler) Name
    /// wird zum leeren String.
    /// </summary>
    /// <param name="codeResult">Die <c>[result …]</c>-Deklaration, oder <c>null</c>, wenn keine
    /// vorhanden ist.</param>
    /// <returns>Der Code-Parameter, oder <c>null</c>, wenn <paramref name="codeResult"/>
    /// <c>null</c> ist oder keine Typ-Angabe trägt.</returns>
    public static ICodeParameter? FromResultDeclaration(CodeResultDeclarationSyntax? codeResult) {

        if (codeResult?.Result.Type == null) {
            return null;
        }

        return new CodeParameter(
            parameterName: codeResult.Result.Identifier.ToString(),
            parameterType: codeResult.Result.Type.ToString(),
            location     : codeResult.GetLocation());
    }

}
