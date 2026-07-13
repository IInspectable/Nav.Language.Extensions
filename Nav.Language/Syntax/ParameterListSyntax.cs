using System;
using System.Collections;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Eine kommagetrennte Parameterliste, z.B. <c>string name, int count</c> in
/// <c>[params string name, int count]</c> — der Inhalt einer <c>[params …]</c>-Annotation
/// (siehe <see cref="CodeParamsDeclarationSyntax"/>). Der Knoten ist selbst als
/// <see cref="IReadOnlyList{T}"/> seiner <see cref="ParameterSyntax"/>-Elemente iterierbar;
/// die trennenden Kommas sind Tokens am Knoten und nicht Teil der Liste.
/// </summary>
[Serializable]
[SampleSyntax("T1 param1, T2 param2")]
public partial class ParameterListSyntax: SyntaxNode, IReadOnlyList<ParameterSyntax> {

    readonly IReadOnlyList<ParameterSyntax> _parameters;

    internal ParameterListSyntax(TextExtent extent, IReadOnlyList<ParameterSyntax> parameters): base(extent) {
        AddChildNodes(_parameters = parameters);
    }

    /// <summary>Liefert einen Enumerator über die <see cref="ParameterSyntax"/>-Elemente in Quelltext-Reihenfolge.</summary>
    public IEnumerator<ParameterSyntax> GetEnumerator() {
        return _parameters.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    /// <summary>Die Anzahl der Parameter in dieser Liste (mindestens einer).</summary>
    public int Count => _parameters.Count;

    /// <summary>Der Parameter an der angegebenen Position (in Quelltext-Reihenfolge).</summary>
    /// <param name="index">Der nullbasierte Index des Parameters.</param>
    public ParameterSyntax this[int index] => _parameters[index];

}