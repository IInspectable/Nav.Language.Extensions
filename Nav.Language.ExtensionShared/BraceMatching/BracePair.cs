namespace Pharmatechnik.Nav.Language.Extension.BraceMatching; 

/// <summary>
/// Ein zusammengehöriges Klammerpaar aus öffnender und schließender Klammer. Als
/// <c>BracePair&lt;SyntaxTokenType&gt;</c> im <see cref="BraceMatchingTagger"/> genutzt, um zu einer
/// Klammer die passende Gegenklammer zu bestimmen.
/// </summary>
/// <typeparam name="T">Typ der Klammer-Kennung (z.B. <see cref="SyntaxTokenType"/>).</typeparam>
sealed class BracePair<T> {
        
    internal BracePair(T openBrace, T closeBrace) {
        OpenBrace  = openBrace;
        CloseBrace = closeBrace;
    }

    /// <summary>Die öffnende Klammer des Paars.</summary>
    public T OpenBrace  { get; }
    /// <summary>Die schließende Klammer des Paars.</summary>
    public T CloseBrace { get; }
}

/// <summary>Fabrikmethoden für <see cref="BracePair{T}"/> mit Typinferenz.</summary>
static class BracePair {
    /// <summary>Erzeugt ein <see cref="BracePair{T}"/> aus öffnender und schließender Klammer.</summary>
    public static BracePair<T> Create<T>(T openBrace, T closeBrace) {
        return new BracePair<T>(openBrace, closeBrace);
    }
}