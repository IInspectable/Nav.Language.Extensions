namespace Pharmatechnik.Nav.Language.Extension.Commands; 

/// <summary>
/// Stabile, zentral gepflegte Namen der Nav-Command-Handler. Jeder Name identifiziert einen Handler
/// gegenüber dem VS-Kompositionsmodell (MEF) und dessen Prioritäts-/Reihenfolgesteuerung — er wird
/// als <c>[Name(...)]</c> am jeweiligen <c>[Export(typeof(ICommandHandler))]</c> gesetzt und über
/// <c>[Order]</c>/<c>[HandlerAfter]</c> referenziert. Die <see langword="const"/>-Werte entsprechen per
/// <see langword="nameof"/> exakt dem Klassennamen des zugehörigen Handlers.
/// </summary>
static class CommandHandlerNames {

    /// <summary>Name von <see cref="NavigateToHighlightReferenceCommandHandler"/>.</summary>
    public const string NavigateToHighlightReferenceCommandHandler = nameof(NavigateToHighlightReferenceCommandHandler);
    /// <summary>Name von <see cref="CommentUncommentSelectionCommandHandler"/>.</summary>
    public const string CommentUncommentSelectionCommandHandler    = nameof(CommentUncommentSelectionCommandHandler);
    /// <summary>Name von <see cref="GoToDefinitionCommandCommandHandler"/>.</summary>
    public const string GoToDefinitionCommandCommandHandler        = nameof(GoToDefinitionCommandCommandHandler);
    /// <summary>Name von <see cref="ViewCSharpCodeCommandHandler"/>.</summary>
    public const string ViewCSharpCodeCommandHandler               = nameof(ViewCSharpCodeCommandHandler);
    /// <summary>Name von <see cref="RenameCommandHandler"/>.</summary>
    public const string RenameCommandHandler                       = nameof(RenameCommandHandler);
    /// <summary>Name von <see cref="PasteCommandHandler"/>.</summary>
    public const string PasteCommandHandler                        = nameof(PasteCommandHandler);
    /// <summary>Name von <see cref="FindReferencesCommandHandler"/>.</summary>
    public const string FindReferencesCommandHandler               = nameof(FindReferencesCommandHandler);
    /// <summary>Name von <see cref="ViewCallHierarchyCommandHandler"/>.</summary>
    public const string ViewCallHierarchyCommandHandler            = nameof(ViewCallHierarchyCommandHandler);
    /// <summary>Name von <see cref="FormatCommandHandler"/>.</summary>
    public const string FormatCommandHandler                       = nameof(FormatCommandHandler);

}