namespace Pharmatechnik.Nav.Language.Extension.Options; 

/// <summary>
/// Nur-Lese-Sicht auf die erweiterten Editor-Optionen der Nav-Extension. Feature-Bausteine (Classification,
/// Highlight-References, Brace-Completion …) lesen ihre Einstellungen über diese Schnittstelle; die konkreten
/// Werte liefert die Optionsseite <see cref="AdvancedOptionsDialogPage"/>.
/// </summary>
public interface IAdvancedOptions {
    /// <summary>Ob die semantische Hervorhebung (Semantic Highlighting) aktiv ist.</summary>
    bool SemanticHighlighting            { get; }
    /// <summary>Ob Referenzen des Symbols unter dem Cursor hervorgehoben werden.</summary>
    bool HighlightReferencesUnderCursor  { get;  }
    /// <summary>
    /// Ob die Referenz-Hervorhebung auch über <c>include</c>-Grenzen hinweg gilt. Nur wirksam, wenn
    /// <see cref="HighlightReferencesUnderCursor"/> aktiv ist.
    /// </summary>
    bool HighlightReferencesUnderInclude { get;  }
    /// <summary>Ob Begrenzer (Klammern, Anführungszeichen) beim Tippen automatisch ergänzt werden.</summary>
    bool AutoInsertDelimiters            {get;}
}