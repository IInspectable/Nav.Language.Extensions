#region Using Directives

using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;

using Microsoft.VisualStudio.Shell.FindAllReferences;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;
using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.Extension.Images;
using Pharmatechnik.Nav.Language.FindReferences;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.FindReferences; 

/// <summary>
/// Der Definitionsknoten (Gruppierungs-Bucket) im „Find All References"-Fenster: Unter ihm sammeln sich
/// die <see cref="ReferenceEntry"/>s einer gefundenen Definition. Als <c>DefinitionBucket</c> liefert er
/// Anzeigename, Icon (aus dem Symbol oder einer Override) und die per <see cref="GetValue"/> abgefragten
/// Zellwerte; Definitionen werden per <see cref="DefinitionItem"/>-Identität zusammengeführt.
/// </summary>
class DefinitionEntry: DefinitionBucket {

    readonly ImageMoniker? _imageMonikerOverride;

    DefinitionEntry(FindReferencesPresenter presenter,
                    DefinitionItem definitionItem,
                    ImageMoniker? imageMoniker)
        // Es wird nach Name sortiert. Expandierte Definitionen sollen immer vor den übrigen stehen
        // TODO Evtl. Hashcode für stabile Sortierung einbauen?
        : base(definitionItem.SortText,
               FindReferencesContext.FindAllReferencesSourceTypeIdentifier,
               FindReferencesContext.FindAllReferencesIdentifier,
               tooltip: null,
               comparer: null,
               expandedByDefault: true /* TODO check definitionItem.ExpandedByDefault*/) {

        Presenter      = presenter;
        DefinitionItem = definitionItem;

        _imageMonikerOverride = imageMoniker;
    }

    /// <summary>Factory für einen Definitionsknoten (optional mit erzwungenem Icon statt Symbol-Icon).</summary>
    public static DefinitionEntry Create(FindReferencesPresenter presenter,
                                         DefinitionItem definitionItem,
                                         ImageMoniker? imageMoniker = null) {
        return new DefinitionEntry(presenter, definitionItem, imageMoniker);
    }

    /// <summary>Der Presenter mit den WPF-Darstellungshilfen.</summary>
    public FindReferencesPresenter Presenter      { get; }
    /// <summary>Die zugrunde liegende Engine-Definition (Symbol, Location, Anzeigeteile).</summary>
    public DefinitionItem          DefinitionItem { get; }

    /// <summary>Icon des Knotens: die Override, sonst aus dem Symbol abgeleitet, sonst keins.</summary>
    public ImageMoniker? ImageMoniker {
        get {
            if (_imageMonikerOverride != null) {
                return _imageMonikerOverride.Value;
            }

            if (DefinitionItem.Symbol != null) {
                return ImageMonikers.FromSymbol(DefinitionItem.Symbol);
            }

            return null;
        }
    }

    /// <summary>VS-Tabellen-Abfrage eines Zellwerts; liefert <c>false</c>, wenn der Schlüssel keinen Wert hat.</summary>
    public override bool TryGetValue(string key, out object content) {
        content = GetValue(key);
        return content != null;
    }

    /// <summary>Liefert den Zellwert des Definitionsknotens für einen VS-Tabellenspalten-Schlüssel.</summary>
    public object GetValue(string key) {
        switch (key) {
            case StandardTableKeyNames.Text:
                // Wird für die Suche verwendet...
                return DefinitionItem.Text;

            case StandardTableKeyNames2.DefinitionIcon:
                if (ImageMoniker != null) {
                    return ImageMoniker.Value;
                }

                break;

            case StandardTableKeyNames2.TextInlines:

                if (DefinitionItem.TextParts.Any()) {
                    // Damit er Text nicht so am Icon klebt...
                    var inlines = new List<Inline> {new Run(" ")};
                    inlines.AddRange(Presenter.ToInlines(DefinitionItem.TextParts, (run, _, _) => Presenter.SetBold(run)));
                    return inlines;
                }

                break;
            case StandardTableKeyNames.DocumentName:
                return DefinitionItem.Location?.FilePath;
            case StandardTableKeyNames.Line:
                return DefinitionItem.Location?.StartLine;
            case StandardTableKeyNames.Column:
                return DefinitionItem.Location?.StartCharacter;
        }

        return null;
    }

}