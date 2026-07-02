#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

[Serializable]
[SampleSyntax("T1 param1, T2 param2")]
public partial class ParameterListSyntax: SyntaxNode, IReadOnlyList<ParameterSyntax> {

    readonly IReadOnlyList<ParameterSyntax> _parameters;

    internal ParameterListSyntax(TextExtent extent, IReadOnlyList<ParameterSyntax> parameters): base(extent) {
        AddChildNodes(_parameters = parameters);
    }

    public IEnumerator<ParameterSyntax> GetEnumerator() {
        return _parameters.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    public int Count => _parameters.Count;

    public ParameterSyntax this[int index] => _parameters[index];

}