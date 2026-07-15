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

    /// <summary>Erzeugt eine Code-Aktion.</summary>
    /// <param name="title">Der Anzeigetitel (<see cref="Title"/>).</param>
    /// <param name="category">Die Kategorie (<see cref="Category"/>) — Grundlage für das Mapping auf den LSP-<c>CodeActionKind</c>.</param>
    /// <param name="textChanges">Die aufgelösten, offset-basierten Änderungen (<see cref="TextChanges"/>).</param>
    public NavCodeAction(string title, CodeFixCategory category, IReadOnlyList<TextChange> textChanges) {
        Title       = title       ?? throw new ArgumentNullException(nameof(title));
        TextChanges = textChanges ?? throw new ArgumentNullException(nameof(textChanges));
        Category    = category;
    }

    /// <summary>Der Anzeigetitel der Aktion (im Client als Lightbulb-/Menüeintrag).</summary>
    public string                    Title       { get; }
    /// <summary>Die Kategorie der zugrunde liegenden CodeFix — Grundlage für das Mapping auf den LSP-<c>CodeActionKind</c>.</summary>
    public CodeFixCategory           Category    { get; }
    /// <summary>Die dateilokalen, offset-basierten Textänderungen, die die Aktion anwendet.</summary>
    public IReadOnlyList<TextChange> TextChanges { get; }

}
