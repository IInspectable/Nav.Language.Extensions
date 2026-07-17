#region Using Directives

using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.VisualStudio.Text;

using Pharmatechnik.Nav.Language.Text;
using Pharmatechnik.Nav.Language.Extension.Common;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.CodeFixes; 

/// <summary>
/// Bündelt eine Folge von Engine-<see cref="TextChange"/>s mit dem <see cref="ITextSnapshot"/>, auf den sie
/// sich beziehen. Die Snapshot-Bindung (geerbt von <see cref="AndSnapshot"/>) erlaubt dem
/// <see cref="ITextChangeService"/> zu erkennen, ob die Edits noch zum aktuellen Puffer-Stand passen, bzw.
/// sie beim Anwenden auf den aktuellen Snapshot nachzuführen.
/// </summary>
sealed class TextChangesAndSnapshot: AndSnapshot {
        
    /// <summary>Bindet das Edit-Set an den Snapshot, auf dem es berechnet wurde.</summary>
    /// <param name="textChanges">Die anzuwendenden Änderungen.</param>
    /// <param name="snapshot">Der Snapshot, auf den sich die Änderungen beziehen.</param>
    public TextChangesAndSnapshot(IEnumerable<TextChange> textChanges, ITextSnapshot snapshot): base(snapshot) {
        TextChanges = textChanges.ToImmutableList();
    }

    /// <summary>Die anzuwendenden Änderungen, bezogen auf den <see cref="AndSnapshot.Snapshot"/>.</summary>
    public ImmutableList<TextChange> TextChanges { get; }
}