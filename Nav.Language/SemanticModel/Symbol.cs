using System;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Gemeinsame Basisklasse aller <see cref="ISymbol"/>-Implementierungen des semantischen Modells:
/// hält das unveränderliche Paar aus <see cref="Name"/> und <see cref="Location"/> und leitet die
/// <see cref="IExtent"/>-Positionen daraus ab. Die konkreten Symbole entstehen ausschließlich in
/// den Buildern (<see cref="TaskDefinitionSymbolBuilder"/>, <see cref="TaskDeclarationSymbolBuilder"/>,
/// <see cref="CodeGenerationUnitBuilder"/>).
/// </summary>
abstract partial class Symbol: ISymbol {

    /// <summary>
    /// Initialisiert das Symbol mit seinem Namen und seiner Fundstelle.
    /// </summary>
    /// <param name="name">Der Name des Symbols (siehe <see cref="ISymbol.Name"/>).</param>
    /// <param name="location">Die Fundstelle im Quelltext; darf nicht <c>null</c> sein.</param>
    /// <exception cref="ArgumentNullException"><paramref name="location"/> ist <c>null</c>.</exception>
    protected Symbol(string name, Location location) {
        Name     = name;
        Location = location ?? throw new ArgumentNullException(nameof(location));
    }

    /// <inheritdoc/>
    public abstract SyntaxTree? SyntaxTree { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// Virtuell, damit Ableitungen den effektiven Namen umdefinieren können — Init- und
    /// Task-Knoten mit Alias liefern den Alias-Namen statt des deklarierten Namens.
    /// </remarks>
    public virtual string Name { get; }

    /// <inheritdoc/>
    public Location Location { get; }

    int IExtent.Start => Location.Start;
    int IExtent.End   => Location.End;

}
