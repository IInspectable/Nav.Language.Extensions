#region Using Directives

using System;
using System.Windows;

using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.FindReferences; 

/// <summary>
/// Basisklasse einer Ergebniszeile im „Find All References"-Fenster (eine Fundstelle oder eine
/// Ersatzmeldung), zugeordnet einem <see cref="DefinitionEntry"/>-Bucket. Die VS-Tabellensteuerung fragt
/// Zellwerte über <see cref="GetValue"/> und optionalen Spalteninhalt über
/// <see cref="TryCreateColumnContent"/> ab. Konkrete Ausprägungen: <see cref="ReferenceEntry"/> und
/// <see cref="SimpleTextEntry"/>.
/// </summary>
abstract class Entry {

    protected Entry(FindReferencesPresenter presenter, DefinitionEntry definition) {
        Presenter  = presenter  ?? throw new ArgumentNullException(nameof(presenter));
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>Der Presenter mit den WPF-Darstellungshilfen.</summary>
    public FindReferencesPresenter Presenter  { get; }
    /// <summary>Der Definitionsknoten (Bucket), unter dem diese Zeile gruppiert ist.</summary>
    public DefinitionEntry         Definition { get; }

    /// <summary>Der reine Zeilentext (u.a. für die Suche im Ergebnisfenster).</summary>
    public abstract string Text { get; }

    /// <summary>Liefert den Zellwert für einen VS-Tabellenspalten-Schlüssel (Text, Definition …).</summary>
    public virtual object GetValue(string keyName) {
        switch (keyName) {
            case StandardTableKeyNames.Text:
                // Wird für die Suche verwendet...
                return Text;
            case StandardTableKeyNames2.Definition:
                return Definition;
        }

        return null;
    }

    /// <summary>Optionaler, selbst gerenderter Spalteninhalt (WPF) — Standard: keiner.</summary>
    public virtual FrameworkElement TryCreateColumnContent() => null;

}