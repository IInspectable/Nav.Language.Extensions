#region Using Directives

using System;
using System.Threading;
using System.Collections.Immutable;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language;

public class SyntaxTree {

    internal SyntaxTree(SourceText sourceText,
                        SyntaxNode root,
                        SyntaxTokenList tokens,
                        ImmutableArray<Diagnostic> diagnostics) {

        Root        = root       ?? throw new ArgumentNullException(nameof(root));
        Tokens      = tokens     ?? SyntaxTokenList.Empty;
        SourceText  = sourceText ?? SourceText.Empty;
        Diagnostics = diagnostics;
    }

    [NotNull]
    public SyntaxNode Root { get; }

    [NotNull]
    public SourceText SourceText { get; }

    [NotNull]
    public SyntaxTokenList Tokens { get; }

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    public static SyntaxTree ParseText(string text, string filePath = null, CancellationToken cancellationToken = default) {
        return NavParser.Parse(text, filePath, cancellationToken);
    }

}
