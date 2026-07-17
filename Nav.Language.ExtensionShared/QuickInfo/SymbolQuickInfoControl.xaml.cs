#region Using Directives

using System.Windows.Controls;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.QuickInfo; 

/// <summary>
/// WPF-Code-Behind des QuickInfo-Kopfs eines einzelnen Elements: Icon (<c>CrispImage</c>) plus
/// klassifizierter Signatur-/Text-Inhalt (<c>TextContent</c>). Wird vom <see cref="QuickinfoBuilderService"/>
/// für Symbole, Schlüsselwörter und Datei-Infos befüllt; das Layout liegt im zugehörigen XAML.
/// </summary>
public partial class SymbolQuickInfoControl : StackPanel {
    /// <summary>Initialisiert das Control und lädt das zugehörige XAML-Layout.</summary>
    public SymbolQuickInfoControl() {
        InitializeComponent();
    }
}