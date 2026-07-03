#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language;

sealed class CodeParameter: ICodeParameter {

    CodeParameter(string? parameterName, string? parameterType, Location location) {
        ParameterType = parameterType ?? string.Empty;
        ParameterName = parameterName ?? string.Empty;
        Location      = location      ?? throw new ArgumentNullException(nameof(location));
    }

    public string ParameterName { get; }

    public Location Location { get; }

    public string ParameterType { get; }

    public static ICodeParameter? FromResultDeclaration(CodeResultDeclarationSyntax? codeResult) {

        if (codeResult?.Result?.Type == null) {
            return null;
        }

        return new CodeParameter(
            parameterName: codeResult.Result.Identifier.ToString(),
            parameterType: codeResult.Result.Type.ToString(),
            location     : codeResult.GetLocation());
    }

}
