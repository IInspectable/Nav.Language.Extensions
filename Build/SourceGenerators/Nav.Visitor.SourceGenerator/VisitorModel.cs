namespace Pharmatechnik.Nav.Language.Visitor.SourceGenerator;

/// <summary>
/// Eine konkrete <c>SyntaxNode</c>-Ableitung, für die Accept-/Walk-Methoden erzeugt werden. Bewusst rein
/// wertbasiert (nur der Typname) — kein <c>ISymbol</c> —, damit das inkrementelle Caching der
/// Generator-Pipeline greift.
/// </summary>
sealed record SyntaxNodeInfo {

    /// <summary>Der Klassenname der Knotenklasse (z.B. <c>TaskDefinitionSyntax</c>).</summary>
    public required string TypeName { get; init; }

}

/// <summary>
/// Die Zuordnung einer konkreten Symbol-Klasse zu dem <c>ISymbol</c>-Interface, über das sie besucht wird.
/// Die Visitor-Methoden sind auf das Interface typisiert, die Accept-Implementierung steht in der Klasse.
/// Rein wertbasiert (zwei Namen) für das Caching der Pipeline.
/// </summary>
sealed record SymbolMapping {

    /// <summary>Der Klassenname der konkreten Symbol-Klasse (z.B. <c>TaskNodeSymbol</c>).</summary>
    public required string ClassName { get; init; }

    /// <summary>Das zugehörige <c>ISymbol</c>-Interface (z.B. <c>ITaskNodeSymbol</c>).</summary>
    public required string InterfaceName { get; init; }

    /// <summary>
    /// Die von <see cref="InterfaceName"/> abgeleiteten <c>ISymbol</c>-Interfaces (ohne <c>ISymbol</c>
    /// selbst), sortiert und komma-separiert. Daraus wird der hierarchische Standard-Fallback der
    /// Besuchsmethoden gebildet (eine abgeleitete Methode ruft die ihres nächsten besuchten
    /// Basis-Interfaces). Als String gehalten, damit die Wertgleichheit fürs Caching greift.
    /// </summary>
    public required string BaseInterfacesCsv { get; init; }

}

/// <summary>
/// Eine konkrete <c>NavTaskAnnotation</c>-Ableitung, für die Accept-/Visit-Methoden erzeugt werden. Anders
/// als beim Symbol-Besucher sind die Besuchsmethoden auf die konkreten Klassen typisiert (nicht auf ein
/// Interface). Rein wertbasiert (Name + Wurzelkennzeichen) fürs Caching der Pipeline.
/// </summary>
sealed record AnnotationMapping {

    /// <summary>Der Klassenname der Annotation (z.B. <c>NavChoiceAnnotation</c>).</summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// Ob dies die Wurzelklasse <c>NavTaskAnnotation</c> ist. Nur sie deklariert die <c>Accept</c>-Methoden
    /// (<c>virtual</c>), alle übrigen überschreiben sie (<c>override</c>).
    /// </summary>
    public required bool IsRoot { get; init; }

}
