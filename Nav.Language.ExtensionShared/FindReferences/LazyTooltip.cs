#region Using Directives

using System;
using System.Windows;
using System.Windows.Controls;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.FindReferences; 

/// <summary>
/// Hängt einen erst bei Bedarf erzeugten Tooltip an ein WPF-Element: Statt eines fertigen Tooltips wird
/// zunächst dieses Objekt gesetzt; beim <c>ToolTipOpening</c> tauscht es sich gegen den real erzeugten
/// Tooltip aus und gibt ihn beim Schließen wieder frei. So bleiben die (teuren) Tooltip-Inhalte der
/// „Find All References"-Zeilen ungebaut, bis sie tatsächlich angezeigt werden.
/// </summary>
class LazyTooltip {

    readonly FrameworkElement _element;
    readonly Func<ToolTip>    _createToolTip;

    private LazyTooltip(FrameworkElement element,
                        Func<ToolTip> createToolTip) {
        _element       = element;
        _createToolTip = createToolTip;

        // Set ourselves as the tooltip of this text block.  This will let WPF know that 
        // it should attempt to show tooltips here.  When WPF wants to show the tooltip 
        // though we'll hear about it "ToolTipOpening".  When that happens, we'll swap
        // out ourselves with a real tooltip that is lazily created.  When that tooltip
        // is the dismissed, we'll release the resources associated with it and we'll
        // reattach ourselves.
        _element.ToolTip = this;

        element.ToolTipOpening += OnToolTipOpening;
        element.ToolTipClosing += OnToolTipClosing;

    }

    /// <summary>Verknüpft mit <paramref name="element"/> einen lazy erzeugten Tooltip (via <paramref name="createToolTip"/>).</summary>
    public static void AttachTo(FrameworkElement element, Func<ToolTip> createToolTip) {
        var _ = new LazyTooltip(element, createToolTip);
    }

    private void OnToolTipOpening(object sender, ToolTipEventArgs e) {
        _element.ToolTip = _createToolTip();
    }

    private void OnToolTipClosing(object sender, ToolTipEventArgs e) {
        _element.ToolTip = this;

    }

}