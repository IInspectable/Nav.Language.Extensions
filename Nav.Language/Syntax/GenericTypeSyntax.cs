using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

namespace Pharmatechnik.Nav.Language; 

/// <summary>
/// Ein generischer Typ wie <c>List&lt;string&gt;</c> in einer Code-Annotation — ein Typname gefolgt
/// von einer <c>&lt;…&gt;</c>-Typargumentliste. Die Argumente sind selbst beliebige
/// <see cref="CodeTypeSyntax"/> und dürfen verschachtelt generisch sein, z.B.
/// <c>Dictionary&lt;string, List&lt;int&gt;&gt;</c>.
/// </summary>
[Serializable]
[SampleSyntax("Type<T1, T2<T3, T4>>")]
public partial class GenericTypeSyntax: CodeTypeSyntax {

    internal GenericTypeSyntax(TextExtent extent, IReadOnlyList<CodeTypeSyntax> genericArguments): base(extent) {
        AddChildNodes(GenericArguments = genericArguments);
    }

    /// <summary>Der Typname vor der öffnenden <c>&lt;</c>-Klammer, oder ein fehlendes Token, wenn er im Quelltext fehlt.</summary>
    public SyntaxToken Identifier => ChildTokens().FirstOrMissing(SyntaxTokenType.Identifier);

    /// <summary>Die Typargumente zwischen <c>&lt;</c> und <c>&gt;</c> in Quelltext-Reihenfolge (mindestens eines).</summary>
    public IReadOnlyList<CodeTypeSyntax> GenericArguments { get; }

}