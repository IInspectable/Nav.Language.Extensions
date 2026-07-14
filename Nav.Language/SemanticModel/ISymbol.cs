using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Basis-Vertrag aller Symbole des semantischen Modells — der benannten Sprachelemente einer
/// <c>.nav</c>-Datei (Task-Definitionen und -Deklarationen, Knoten, Knoten-Referenzen, Trigger,
/// Connection Points, Includes), die beim Binden des Syntaxbaums zu einer
/// <see cref="CodeGenerationUnit"/> entstehen. Ein Symbol ist über seinen <see cref="Name"/>
/// adressierbar und über seine <see cref="Location"/> im Quelltext verortet; via
/// <see cref="IExtent"/> ist es zusätzlich positions-adressierbar (Grundlage von
/// <see cref="SymbolList"/> und der Caret-Auflösung).
/// </summary>
public partial interface ISymbol: IExtent {

    /// <summary>
    /// Der Name des Symbols, wie er im Quelltext steht — z.B. der Task-Name in
    /// <c>task A { … }</c> oder der Knotenname einer Referenz. Er dient als Schlüssel in einer
    /// <see cref="SymbolCollection{T}"/> (siehe <see cref="SymbolCollection{T}.GetKeyForItem"/>).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Die Fundstelle des Symbols im Quelltext: Datei, Zeichen-Ausschnitt und Zeilen-/Spalten-
    /// Positionen — die Grundlage für Navigation (GoTo, References) und Diagnostik.
    /// </summary>
    Location Location { get; }

    /// <summary>
    /// Liefert den Syntaxbaum, aus dem dieses Symbol entstanden ist.
    /// Kann bei importierten TaskDeclarations <c>null</c> sein — eine per Include-Direktive
    /// (<c>taskref "datei.nav";</c>) hereingeholte <see cref="ITaskDeclarationSymbol">
    /// Task-Deklaration</see> verwirft ihren Syntax-Verweis bewusst (nur die
    /// <see cref="Location"/> bleibt erhalten).
    /// </summary>
    SyntaxTree? SyntaxTree { get; }

}
