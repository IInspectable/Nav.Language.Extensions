#region Using Directives

using System.Collections.Immutable;
using System.Windows;

using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.FindReferences; 

/// <summary>
/// Eine unveränderliche, versionierte Momentaufnahme der Ergebniszeilen (<see cref="Entry"/>) für die
/// VS-Tabellensteuerung. Als <c>WpfTableEntriesSnapshotBase</c> reicht sie Zellwerte und selbst
/// gerenderten Spalteninhalt an die Einträge durch. Erzeugt vom <see cref="FindReferencesContext"/>.
/// </summary>
class TableEntriesSnapshot: WpfTableEntriesSnapshotBase {

    readonly FindReferencesContext _context;
    readonly ImmutableArray<Entry> _entries;

    public TableEntriesSnapshot(FindReferencesContext context, int versionNumber, ImmutableArray<Entry> items) {
        _context      = context;
        _entries      = items;
        VersionNumber = versionNumber;

    }

    /// <summary>Der Presenter mit den WPF-Darstellungshilfen.</summary>
    public FindReferencesPresenter Presenter => _context.Presenter;

    /// <inheritdoc/>
    public override int VersionNumber { get; }

    /// <inheritdoc/>
    public override int Count => _entries.Length;

    /// <summary>
    /// Bildet einen Index dieses Snapshots auf den gleichen Index eines neueren ab — zulässig, weil nur
    /// am Ende angehängt und nie umsortiert wird.
    /// </summary>
    public override int IndexOf(int currentIndex, ITableEntriesSnapshot newSnapshot) {
        // We only add items to the end of our list, and we never reorder.
        // As such, any index in us will map to the same index in any newer snapshot.
        return currentIndex;
    }

    /// <summary>Liefert den Zellwert der Zeile <paramref name="index"/> für den Spalten-Schlüssel.</summary>
    public override bool TryGetValue(int index, string keyName, out object content) {
        content = _entries[index].GetValue(keyName);
        return content != null;
    }

    /// <summary>Liefert für die Zeilentext-Spalte den selbst gerenderten Inhalt der Zeile; sonst Basisverhalten.</summary>
    public override bool TryCreateColumnContent(int index, string columnName, bool singleColumnView, out FrameworkElement content) {

        if (columnName == StandardTableColumnDefinitions2.LineText) {

            content = _entries[index].TryCreateColumnContent();

            return content != null;
        }

        return base.TryCreateColumnContent(index, columnName, singleColumnView, out content);

    }

}