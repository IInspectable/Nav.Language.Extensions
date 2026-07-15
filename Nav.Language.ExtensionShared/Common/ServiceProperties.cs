#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Zentrale Sammlung der Drossel-Zeiten (Throttle-Zeiten), mit denen die editornahen Dienste der
/// Extension ihre Neuberechnungen entzerren.
/// </summary>
static class ServiceProperties {
    /// <summary>Drossel-Zeit für den Parser-Dienst.</summary>
    public static TimeSpan ParserServiceThrottleTime        = TimeSpan.FromMilliseconds(200);
    /// <summary>Drossel-Zeit für den Semantikmodell-Dienst.</summary>
    public static TimeSpan SemanticModelServiceThrottleTime = TimeSpan.FromMilliseconds(200);
    /// <summary>Drossel-Zeit für das Klammer-Matching.</summary>
    public static TimeSpan BraceMatchingThrottleTime        = TimeSpan.FromMilliseconds(500);
    /// <summary>Drossel-Zeit für die Referenz-Hervorhebung.</summary>
    public static TimeSpan ReferenceHighlighting            = TimeSpan.FromMilliseconds(500);
    /// <summary>Drossel-Zeit für den GoTo-Nav-Tagger.</summary>
    public static TimeSpan GoToNavTaggerThrottleTime        = TimeSpan.FromMilliseconds(500);
}