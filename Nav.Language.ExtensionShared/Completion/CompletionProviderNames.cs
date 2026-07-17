namespace Pharmatechnik.Nav.Language.Extension.Completion; 

/// <summary>
/// Die MEF-Namen (<c>[Name]</c>) der Completion-Provider — als Konstanten, damit
/// <c>[Order]</c>-Verweise zwischen den Providern nicht auf Zeichenketten-Literale angewiesen sind.
/// </summary>
static class CompletionProviderNames {

    /// <summary>MEF-Name des <see cref="NavCompletionSourceProvider"/>.</summary>
    public const string NavCompletionSourceProvider        = nameof(NavCompletionSourceProvider);
    /// <summary>MEF-Name des Commit-Manager-Providers.</summary>
    public const string NavCompletionCommitManagerProvider = nameof(NavCompletionCommitManagerProvider);

}