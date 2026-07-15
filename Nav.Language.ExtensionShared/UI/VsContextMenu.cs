#region Using Directives

using System.Windows;
using System.Windows.Controls;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.UI; 

/// <summary>
/// WPF-<see cref="ContextMenu"/> im Visual-Studio-Look, das über einen eigenen Standard-Stil (Theme) an das
/// VS-Erscheinungsbild angepasst ist und eine zusätzliche <see cref="Header"/>-Eigenschaft mitbringt.
/// </summary>
class VsContextMenu : ContextMenu {

    /// <summary>Verknüpft den Typ mit seinem im Theme hinterlegten Standard-Stil.</summary>
    static VsContextMenu() {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(VsContextMenu), new FrameworkPropertyMetadata(typeof(VsContextMenu)));

    }

    #region DependencyProperty Header

    /// <summary>
    /// Die Abhängigkeitseigenschaft, die als Hintergrundspeicher der <see cref="Header"/>-Eigenschaft dient.
    /// </summary>
    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(object), typeof(VsContextMenu),
                                    new FrameworkPropertyMetadata(null,
                                                                  FrameworkPropertyMetadataOptions.AffectsRender |
                                                                  FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>
    /// Der Inhalt der Menü-Kopfzeile (Header) des Kontextmenüs.
    /// </summary>
    public object Header {
        get { return GetValue(HeaderProperty); }
        set { SetValue(HeaderProperty, value); }
    }

    #endregion
}