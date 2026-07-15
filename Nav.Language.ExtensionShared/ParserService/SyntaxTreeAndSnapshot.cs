#region Using Directives

using JetBrains.Annotations;

using Microsoft.VisualStudio.Text;

using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension; 

/// <summary>
/// Bündelt einen <see cref="SyntaxTree"/> mit dem <see cref="ITextSnapshot"/>, aus dem er geparst wurde.
/// Über <see cref="AndSnapshot.IsCurrent(ITextBuffer)"/> lässt sich prüfen, ob der Baum noch zum aktuellen
/// Pufferstand passt.
/// </summary>
sealed class SyntaxTreeAndSnapshot : AndSnapshot {

    /// <summary>
    /// Bündelt <paramref name="syntaxTree"/> mit dem <paramref name="snapshot"/>, aus dem er entstand.
    /// </summary>
    internal SyntaxTreeAndSnapshot([NotNull] SyntaxTree syntaxTree, ITextSnapshot snapshot) : base(snapshot) {
        SyntaxTree = syntaxTree;
    }

    /// <summary>Der zum <see cref="AndSnapshot.Snapshot"/> geparste Syntaxbaum.</summary>
    public SyntaxTree SyntaxTree { get; }

}