#region Using Directives

using System.Windows;
using System.Windows.Controls;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.UI; 

/// <summary>
/// WPF-<see cref="MenuItem"/> im Visual-Studio-Look; über einen eigenen Standard-Stil (Theme) an das
/// VS-Erscheinungsbild angepasst. Bildet die Einträge eines <see cref="VsContextMenu"/>.
/// </summary>
class VsMenuItem : MenuItem {

    /// <summary>Verknüpft den Typ mit seinem im Theme hinterlegten Standard-Stil.</summary>
    static VsMenuItem() {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(VsMenuItem), new FrameworkPropertyMetadata(typeof(VsMenuItem)));
    }
}