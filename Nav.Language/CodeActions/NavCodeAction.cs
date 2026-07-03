#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.CodeFixes;
using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeActions;

/// <summary>
/// VS-freie Beschreibung einer anwendbaren Code-Aktion: ein Titel, die Kategorie (für das Mapping
/// auf den LSP-<c>CodeActionKind</c>) und die bereits aufgelösten, offset-basierten
/// <see cref="TextChange"/> innerhalb der zugehörigen Datei.
/// </summary>
public sealed class NavCodeAction {

    public NavCodeAction(string title, CodeFixCategory category, IReadOnlyList<TextChange> textChanges) {
        Title       = title       ?? throw new ArgumentNullException(nameof(title));
        TextChanges = textChanges ?? throw new ArgumentNullException(nameof(textChanges));
        Category    = category;
    }

    public string                    Title       { get; }
    public CodeFixCategory           Category    { get; }
    public IReadOnlyList<TextChange> TextChanges { get; }

}
