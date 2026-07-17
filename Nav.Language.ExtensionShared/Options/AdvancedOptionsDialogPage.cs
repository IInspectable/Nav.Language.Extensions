#region Using Directives

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

using Microsoft.VisualStudio.Shell;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Options; 

// TODO Default Settings?
/// <summary>
/// Die Visual-Studio-Optionsseite „Advanced" der Nav-Extension. Als <see cref="UIElementDialogPage"/> stellt
/// sie das WPF-Steuerelement <see cref="AdvancedOptionsControl"/> im Optionsdialog dar, persistiert die Werte
/// und stellt sie als <see cref="IAdvancedOptions"/> den Feature-Bausteinen bereit.
/// </summary>
[Guid("2D380106-1E1D-489C-B0AE-707041CB79A0")]
class AdvancedOptionsDialogPage: UIElementDialogPage, IAdvancedOptions {

    AdvancedOptionsControl _advancedOptionsControl;
    bool                   _highlightReferencesUnderInclude;

    /// <summary>Legt die Optionsseite mit den Standardwerten an (alle Optionen eingeschaltet).</summary>
    public AdvancedOptionsDialogPage() {
        SemanticHighlighting           = true;
        HighlightReferencesUnderCursor = true;
        AutoInsertDelimiters           = true;
    }

    /// <summary>Der Name der Optionsseite unterhalb der Nav-Language-Kategorie.</summary>
    public const string PageName = "Advanced";

    /// <summary>Das im Optionsdialog angezeigte WPF-Steuerelement; wird bei Bedarf erzeugt.</summary>
    protected override UIElement Child {
        get { return _advancedOptionsControl ??= new AdvancedOptionsControl(); }
    }

    /// <inheritdoc/>
    public bool SemanticHighlighting           { get; set; }
    /// <inheritdoc/>
    public bool HighlightReferencesUnderCursor { get; set; }

    /// <inheritdoc/>
    public bool HighlightReferencesUnderInclude {
        get => _highlightReferencesUnderInclude && HighlightReferencesUnderCursor;
        set => _highlightReferencesUnderInclude = value;
    }

    /// <inheritdoc/>
    public bool AutoInsertDelimiters { get; set; }

    /// <summary>
    /// Überträgt beim Öffnen der Seite die persistierten Werte in die Kontrollkästchen des Steuerelements.
    /// </summary>
    protected override void OnActivate(CancelEventArgs e) {
        base.OnActivate(e);
        _advancedOptionsControl.SemanticHighlighting.IsChecked            = SemanticHighlighting;
        _advancedOptionsControl.HighlightReferencesUnderCursor.IsChecked  = HighlightReferencesUnderCursor;
        _advancedOptionsControl.HighlightReferencesUnderInclude.IsChecked = HighlightReferencesUnderInclude;
        _advancedOptionsControl.AutoInsertDelimiters.IsChecked            = AutoInsertDelimiters;
    }

    /// <summary>
    /// Übernimmt beim Bestätigen des Dialogs (<c>ApplyKind.Apply</c>) die Werte der Kontrollkästchen
    /// zurück in die persistierten Eigenschaften.
    /// </summary>
    protected override void OnApply(PageApplyEventArgs args) {
        if (args.ApplyBehavior == ApplyKind.Apply) {
            SemanticHighlighting            = _advancedOptionsControl.SemanticHighlighting.IsChecked.GetValueOrDefault();
            HighlightReferencesUnderCursor  = _advancedOptionsControl.HighlightReferencesUnderCursor.IsChecked.GetValueOrDefault();
            HighlightReferencesUnderInclude = _advancedOptionsControl.HighlightReferencesUnderInclude.IsChecked.GetValueOrDefault();
            AutoInsertDelimiters            = _advancedOptionsControl.AutoInsertDelimiters.IsChecked.GetValueOrDefault();
        }

        base.OnApply(args);
    }

}