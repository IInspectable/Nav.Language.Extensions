#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

public sealed record CodeGenerationSpec {

    public CodeGenerationSpec(string? content, string? filePath) {
        Content  = content  ?? String.Empty;
        FilePath = filePath ?? String.Empty;
    }

    public static readonly CodeGenerationSpec Empty = new(content: null, filePath: null);

    public bool IsEmpty => this == Empty;

    public string Content  { get; }
    public string FilePath { get; }

}