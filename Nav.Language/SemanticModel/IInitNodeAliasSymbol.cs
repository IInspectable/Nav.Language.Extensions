namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Symbol des Alias eines <c>init</c>-Knotens — der optionale Bezeichner hinter <c>init</c>, z.B.
/// <c>I1</c> in <c>init I1;</c>. Der Alias ist ein eigenes Symbol mit eigener Fundstelle; der
/// Init-Knoten übernimmt seinen Namen als effektiven Namen (siehe
/// <see cref="IInitNodeSymbol.Alias"/>).
/// </summary>
public interface IInitNodeAliasSymbol: ISymbol {

    /// <summary>Der Init-Knoten, dem dieser Alias gehört (Rückverweis zu <see cref="IInitNodeSymbol.Alias"/>).</summary>
    IInitNodeSymbol InitNode { get; }

}
