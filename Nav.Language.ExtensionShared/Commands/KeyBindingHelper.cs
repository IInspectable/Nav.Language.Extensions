#region Using Directives

using System;
using System.Collections;

using EnvDTE;

using Microsoft.VisualStudio.Shell;

using Pharmatechnik.Nav.Utilities.Logging;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Commands;

/// <summary>
/// Ermittelt die aktuell zugewiesene Tastenkombination eines Visual-Studio-Commands über das
/// DTE-Automationsmodell (<see cref="_DTE"/>.<c>Commands</c>). Wird von <c>NavMarginControl</c>
/// genutzt, um den Shortcut im Tooltip der Symbolleisten-Schaltflächen anzuzeigen.
/// </summary>
static class KeyBindingHelper {

    static readonly Logger Logger = Logger.Create(typeof(KeyBindingHelper));

    /// <summary>
    /// Liefert die zugewiesene Tastenkombination eines Commands ohne Scope-Präfix
    /// (z.B. <c>"Strg+K, Strg+D"</c>). Bevorzugt eine global gültige Bindung; existiert keine,
    /// wird die erste beliebige Bindung (z.B. im Texteditor-Scope) verwendet. Ohne Bindung bzw.
    /// bei unbekanntem Command wird <see cref="String.Empty"/> geliefert.
    /// </summary>
    /// <remarks>
    /// Der Scope-Name „Global" ist sprachunabhängig stabil; der lokalisierte Texteditor-Scope
    /// („Texteditor"/„Text Editor") wird bewusst nicht hart gematcht, sondern über den Fallback
    /// abgedeckt. Für die adressierten Commands (View Code, Nav-Generieren, Dokument formatieren)
    /// gibt es praktisch keine Bindungen in fremden Editor-Scopes, die im Nav-Editor nicht greifen
    /// würden – der Fallback ist damit gefahrlos.
    /// </remarks>
    public static string GetGlobalKeyBinding(Guid commandSet, int commandId) {

        ThreadHelper.ThrowIfNotOnUIThread();

        try {
            var command = NavLanguagePackage.DTE.Commands.Item(commandSet.ToString("B"), commandId);

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

        } catch (Exception ex) {
            // Commands.Item wirft bei unbekanntem Command; robust auf "keine Bindung" zurückfallen.
            Logger.Warn($"{nameof(GetGlobalKeyBinding)}: {ex.Message}");
            return String.Empty;
        }
    }

}
