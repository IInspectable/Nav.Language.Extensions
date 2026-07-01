namespace Pharmatechnik.Nav.Language.Visitor.SourceGenerator;

/// <summary>
/// Eine konkrete <c>SyntaxNode</c>-Ableitung, für die Accept-/Walk-Methoden erzeugt werden. Bewusst rein
/// wertbasiert (nur der Typname) — kein <c>ISymbol</c> —, damit das inkrementelle Caching der
/// Generator-Pipeline greift.
/// </summary>
/// <param name="TypeName">Der Klassenname der Knotenklasse (z.B. <c>TaskDefinitionSyntax</c>).</param>
sealed record SyntaxNodeInfo(string TypeName);

/// <summary>
/// Die Zuordnung einer konkreten Symbol-Klasse zu dem <c>ISymbol</c>-Interface, über das sie besucht wird.
/// Die Visitor-Methoden sind auf das Interface typisiert, die Accept-Implementierung steht in der Klasse.
/// Rein wertbasiert (zwei Namen) für das Caching der Pipeline.
/// </summary>
/// <param name="ClassName">Der Klassenname der konkreten Symbol-Klasse (z.B. <c>TaskNodeSymbol</c>).</param>
/// <param name="InterfaceName">Das zugehörige <c>ISymbol</c>-Interface (z.B. <c>ITaskNodeSymbol</c>).</param>
/// <param name="BaseInterfacesCsv">Die von <paramref name="InterfaceName"/> abgeleiteten <c>ISymbol</c>-
/// Interfaces (ohne <c>ISymbol</c> selbst), sortiert und komma-separiert. Daraus wird der hierarchische
/// Standard-Fallback der Besuchsmethoden gebildet (eine abgeleitete Methode ruft die ihres nächsten
/// besuchten Basis-Interfaces). Als String gehalten, damit die Wertgleichheit fürs Caching greift.</param>
sealed record SymbolMapping(string ClassName, string InterfaceName, string BaseInterfacesCsv);
