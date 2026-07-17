namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Erzeugt <see cref="ISemanticModelProvider"/>-Instanzen über einem gegebenen
/// <see cref="ISyntaxProvider"/>.
/// </summary>
public interface ISemanticModelProviderFactory {

    /// <summary>Erzeugt einen semantischen Provider, der auf <paramref name="syntaxProvider"/> aufsetzt.</summary>
    /// <param name="syntaxProvider">Der zugrunde liegende Syntax-Provider.</param>
    ISemanticModelProvider CreateProvider(ISyntaxProvider syntaxProvider);

}