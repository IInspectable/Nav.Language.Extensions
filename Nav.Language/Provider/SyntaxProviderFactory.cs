namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Vordefinierte <see cref="ISyntaxProviderFactory"/>-Instanzen für die beiden gebräuchlichen
/// Provider-Strategien.
/// </summary>
public class SyntaxProviderFactory  {
    /// <summary>Factory für den nicht-cachenden <see cref="SyntaxProvider"/>.</summary>
    public static readonly ISyntaxProviderFactory Default = new SyntaxProviderFactory<SyntaxProvider>();
    /// <summary>Factory für den cachenden <see cref="CachedSyntaxProvider"/>.</summary>
    public static readonly ISyntaxProviderFactory Cached  = new SyntaxProviderFactory<CachedSyntaxProvider>();      
}

/// <summary>
/// Allgemeine <see cref="ISyntaxProviderFactory"/>, die je Aufruf eine neue Instanz des
/// parameterlos konstruierbaren Provider-Typs <typeparamref name="T"/> erzeugt.
/// </summary>
/// <typeparam name="T">Der zu erzeugende <see cref="ISyntaxProvider"/>-Typ.</typeparam>
public class SyntaxProviderFactory<T> : ISyntaxProviderFactory
    where T : ISyntaxProvider, new() {

    /// <inheritdoc/>
    public ISyntaxProvider CreateProvider() {
        return new T();
    }
}