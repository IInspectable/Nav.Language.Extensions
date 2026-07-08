namespace Pharmatechnik.Nav.Language.Extension.BraceMatching; 

sealed class BracePair<T> {
        
    internal BracePair(T openBrace, T closeBrace) {
        OpenBrace  = openBrace;
        CloseBrace = closeBrace;
    }

    public T OpenBrace  { get; }
    public T CloseBrace { get; }
}

static class BracePair {
    public static BracePair<T> Create<T>(T openBrace, T closeBrace) {
        return new BracePair<T>(openBrace, closeBrace);
    }
}