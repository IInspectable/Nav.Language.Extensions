#region Using Directives

using System;
using System.Collections;

using EnvDTE;

using Microsoft.VisualStudio.Shell;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands;

/// <summary>
/// Ermittelt die aktuell zugewiesene Tastenkombination eines Visual-Studio-Commands über das
/// DTE-Automationsmodell (<see cref="_DTE"/>.<c>Commands</c>). Wird von <c>NavMarginControl</c>
/// genutzt, um den Shortcut im Tooltip der Symbolleisten-Schaltflächen anzuzeigen.
/// </summary>
/// <remarks>
/// Bewusst eigenständig statt <c>Microsoft.Internal.VisualStudio.Shell.KeyBindingHelper</c>: Deren
/// <c>GetGlobalKeyBinding</c> liefert nur <b>global</b> gescopte Bindungen. Befehle wie
/// „Dokument formatieren" sind aber im Texteditor-Scope gebunden (z.B. <c>Ctrl+E, D</c>) und blieben
/// damit im Tooltip leer.
/// </remarks>
static class NavKeyBindingHelper {

    /// <summary>
    /// Liefert die zugewiesene Tastenkombination eines Commands ohne Scope-Präfix
    /// (z.B. <c>"Ctrl+E, D"</c>). Bevorzugt eine global gültige Bindung; existiert keine,
    /// wird die erste beliebige Bindung (z.B. im Texteditor-Scope) verwendet. Ohne Bindung bzw.
    /// bei unbekanntem Command wird <see cref="String.Empty"/> geliefert.
    /// </summary>
    /// <remarks>
    /// Die von DTE gelieferten Bindungs-Strings sind sprachunabhängig englisch
    /// (<c>"Global::"</c>, <c>"Text Editor::Ctrl+E, D"</c>), daher ist der <c>Global::</c>-Vergleich
    /// stabil. <c>Commands.Item</c> wirft für nicht per Automation adressierbare Commands
    /// <see cref="ArgumentException"/> (E_INVALIDARG) – das wird als „keine Bindung" behandelt.
    /// </remarks>
    public static string GetKeyBinding(Guid commandSet, int commandId) {

        ThreadHelper.ThrowIfNotOnUIThread();

        try {
            var command = NavLanguagePackage.DTE?.Commands.Item(commandSet.ToString("B"), commandId);

            // Bindings ist ein SafeArray aus "<Scope>::<Tastenfolge>"-Strings (ggf. leer).
            if (command?.Bindings is not IEnumerable bindings) {
                return String.Empty;
            }

            string firstAny = null;
            foreach (var entry in bindings) {
                if (entry is not string binding) {
                    continue;
                }

                var separator = binding.IndexOf("::", StringComparison.Ordinal);
                var keys       = separator >= 0 ? binding.Substring(separator + 2) : binding;

                // Global gültige Bindung sofort bevorzugen ...
                if (binding.StartsWith("Global::", StringComparison.OrdinalIgnoreCase)) {
                    return keys;
                }

                // ... sonst die erste beliebige Bindung merken (z.B. Texteditor-Scope).
                firstAny ??= keys;
            }

            return firstAny ?? String.Empty;

        } catch (ArgumentException) {
            // Command ist nicht per DTE-Automation adressierbar (z.B. Custom-Command ohne Bindung).
            return String.Empty;
        }
    }

}
