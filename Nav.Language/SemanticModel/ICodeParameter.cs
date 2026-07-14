namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Ein C#-Parameter aus einer Code-Deklaration einer <c>.nav</c>-Datei — konkret der
/// Task-Ergebniswert aus <c>[result Typ name]</c> (siehe
/// <see cref="ITaskDeclarationSymbol.CodeTaskResult"/>, Syntax:
/// <see cref="CodeResultDeclarationSyntax"/>). Typ und Name werden unverändert aus dem
/// Nav-Quelltext übernommen und fließen so in den generierten C#-Code ein.
/// </summary>
public interface ICodeParameter {

    /// <summary>
    /// Der Parametername, wie im Nav-Quelltext notiert — z.B. <c>ergebnis</c> in
    /// <c>[result bool ergebnis]</c>. Nie <c>null</c>; da der Name in der Grammatik optional
    /// ist, kann er leer sein — der Codegen setzt dann einen Default-Namen ein.
    /// </summary>
    string ParameterName { get; }
    /// <summary>
    /// Der C#-Typname, wie im Nav-Quelltext notiert — z.B. <c>bool</c> in
    /// <c>[result bool ergebnis]</c>. Nie <c>null</c>.
    /// </summary>
    string ParameterType { get; }

}
