namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Symbol des Alias eines <c>task</c>-Knotens — der optionale zweite Bezeichner hinter dem
/// Task-Namen, z.B. <c>A1</c> in <c>task Unteraufgabe A1;</c>. Der Alias ist ein eigenes Symbol
/// mit eigener Fundstelle; der Task-Knoten übernimmt seinen Namen als effektiven Namen (siehe
/// <see cref="ITaskNodeSymbol.Alias"/>).
/// </summary>
public interface ITaskNodeAliasSymbol: ISymbol {

    /// <summary>Der Task-Knoten, dem dieser Alias gehört (Rückverweis zu <see cref="ITaskNodeSymbol.Alias"/>).</summary>
    ITaskNodeSymbol TaskNode { get; }

}
