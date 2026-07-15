#region Using Directives

using System;
using JetBrains.Annotations;
using Microsoft.VisualStudio.Text;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension; 

/// <summary>
/// Bündelt eine <see cref="CodeGenerationUnit"/> (das Semantikmodell) mit dem <see cref="ITextSnapshot"/>,
/// aus dem sie berechnet wurde. Über <see cref="AndSnapshot.IsCurrent(ITextBuffer)"/> lässt sich prüfen, ob
/// das Modell noch zum aktuellen Pufferstand passt.
/// </summary>
sealed class CodeGenerationUnitAndSnapshot: AndSnapshot {

    /// <summary>
    /// Bündelt <paramref name="codeGenerationUnit"/> mit dem <paramref name="snapshot"/>, aus dem sie entstand.
    /// </summary>
    internal CodeGenerationUnitAndSnapshot([NotNull] CodeGenerationUnit codeGenerationUnit, [NotNull] ITextSnapshot snapshot): base(snapshot) {
        CodeGenerationUnit = codeGenerationUnit ?? throw new ArgumentNullException(nameof(codeGenerationUnit));
    }

    /// <summary>Das zum <see cref="AndSnapshot.Snapshot"/> berechnete Semantikmodell.</summary>
    [NotNull]
    public CodeGenerationUnit CodeGenerationUnit { get; }         
}