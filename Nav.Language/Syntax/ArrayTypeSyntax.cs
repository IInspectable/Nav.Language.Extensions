using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Ein Array-Typ wie <c>string[]</c> in einer Code-Annotation, z.B. in
/// <c>[params string[] args]</c> — ein Basistyp gefolgt von einem oder mehreren
/// <see cref="ArrayRankSpecifierSyntax"/>-Klammerpaaren (<see cref="RankSpecifiers"/>).
/// </summary>
[Serializable]
[SampleSyntax("Type[]")]
public partial class ArrayTypeSyntax: CodeTypeSyntax {

    internal ArrayTypeSyntax(TextExtent extent, CodeTypeSyntax type, IReadOnlyList<ArrayRankSpecifierSyntax> rankSpecifiers)
        : base(extent) {

        AddChildNode(Type            = type);
        AddChildNodes(RankSpecifiers = rankSpecifiers);
    }

    /// <summary>
    /// Der Basistyp des Arrays — ein <see cref="SimpleTypeSyntax"/> oder <see cref="GenericTypeSyntax"/>,
    /// nie selbst ein <see cref="ArrayTypeSyntax"/>: der Parser sammelt alle <c>[]</c>-Paare flach in
    /// <see cref="RankSpecifiers"/>, statt Array-Typen zu verschachteln.
    /// </summary>
    public CodeTypeSyntax Type { get; }

    /// <summary>
    /// Die Anzahl der <c>[]</c>-Paare (<see cref="RankSpecifiers"/>.Count) — <c>1</c> für
    /// <c>string[]</c>, <c>2</c> für <c>string[][]</c>.
    /// </summary>
    public int Rank => RankSpecifiers.Count;

    /// <summary>Die <c>[]</c>-Klammerpaare in Quelltext-Reihenfolge (mindestens eines).</summary>
    public IReadOnlyList<ArrayRankSpecifierSyntax> RankSpecifiers { get; }

}