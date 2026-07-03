namespace Pharmatechnik.Nav.Language;

public class SyntaxProviderFactory  {
    public static readonly ISyntaxProviderFactory Default = new SyntaxProviderFactory<SyntaxProvider>();
    public static readonly ISyntaxProviderFactory Cached  = new SyntaxProviderFactory<CachedSyntaxProvider>();      
}

public class SyntaxProviderFactory<T> : ISyntaxProviderFactory
    where T : ISyntaxProvider, new() {

    public ISyntaxProvider CreateProvider() {
        return new T();
    }
}