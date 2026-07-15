#region Using Directives

using System;

using JetBrains.Annotations;

using Microsoft.VisualStudio.Text;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Basisklasse für Ergebnisse, die an einen bestimmten <see cref="ITextSnapshot"/> gebunden sind
/// und prüfen können, ob dieser Snapshot noch aktuell ist.
/// </summary>
abstract class AndSnapshot {

    /// <summary>
    /// Initialisiert die Basisklasse mit dem <see cref="ITextSnapshot"/>, auf den sich das
    /// Ergebnis bezieht.
    /// </summary>
    /// <param name="snapshot">Der zugrunde liegende Text-Snapshot; darf nicht <c>null</c> sein.</param>
    protected AndSnapshot([NotNull] ITextSnapshot snapshot) {
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    /// <summary>
    /// Der <see cref="ITextSnapshot"/>, auf den sich dieses Ergebnis bezieht.
    /// </summary>
    [NotNull]
    public ITextSnapshot Snapshot { get; }

    /// <summary>
    /// Prüft, ob der übergebene <see cref="SnapshotSpan"/> aus derselben Snapshot-Version wie
    /// <see cref="Snapshot"/> stammt.
    /// </summary>
    /// <param name="snapshotSpan">Der zu prüfende Span.</param>
    /// <returns><c>true</c>, wenn die Versionsnummern übereinstimmen; andernfalls <c>false</c>.</returns>
    public bool IsCurrent(SnapshotSpan snapshotSpan) {
        return Snapshot.Version.VersionNumber == snapshotSpan.Snapshot.Version.VersionNumber;
    }

    /// <summary>
    /// Prüft, ob der übergebene <see cref="ITextSnapshot"/> mit <see cref="Snapshot"/> identisch
    /// und in derselben iterierten Version ist.
    /// </summary>
    /// <param name="snapshot">Der zu prüfende Snapshot.</param>
    /// <returns><c>true</c>, wenn es sich um denselben, unveränderten Snapshot handelt.</returns>
    public bool IsCurrent(ITextSnapshot snapshot) {
        if (snapshot != Snapshot) {
            return false;
        }
        return Snapshot.Version.ReiteratedVersionNumber == snapshot.Version.ReiteratedVersionNumber;
    }

    /// <summary>
    /// Prüft, ob der aktuelle Snapshot des übergebenen <see cref="ITextBuffer"/> noch dem
    /// gebundenen <see cref="Snapshot"/> entspricht.
    /// </summary>
    /// <param name="textBuffer">Der Textpuffer, dessen aktueller Snapshot geprüft wird.</param>
    /// <returns><c>true</c>, wenn der Puffer unverändert ist.</returns>
    public bool IsCurrent(ITextBuffer textBuffer) {
        return IsCurrent(textBuffer.CurrentSnapshot);
    }
}