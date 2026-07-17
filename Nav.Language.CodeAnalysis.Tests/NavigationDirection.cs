#region Using Directives

using System;

#endregion

namespace Nav.Language.CodeAnalysis.Tests;

/// <summary>
/// Navigationsrichtung eines Golden-Tests. Landet als Pfeil-Kopf (z.B. <c>Nav → C#</c>) in der
/// <c>.expected</c>-Datei und macht beim Diff sofort sichtbar, welche Sprungrichtung geprüft wird.
/// </summary>
enum NavigationDirection {

    /// <summary>Nav → C#: vom <c>.nav</c>-Modell auf den generierten/konkreten C#-Code.</summary>
    NavToCSharp,

    /// <summary>C# → Nav: Rücksprung aus dem C#-Code zurück auf das <c>.nav</c>-Modell.</summary>
    CSharpToNav,

    /// <summary>C# → C#: Navigation innerhalb des C#-Codes (z.B. Verweise auf die <c>{Choice}Logic</c>).</summary>
    CSharpToCSharp,

}

static class NavigationDirectionExtensions {

    /// <summary>Der Pfeil-Kopf für die Golden-Datei — echte U+2192-Pfeile.</summary>
    public static string ToArrowLabel(this NavigationDirection direction) => direction switch {
        NavigationDirection.NavToCSharp    => "Nav → C#",
        NavigationDirection.CSharpToNav    => "C# → Nav",
        NavigationDirection.CSharpToCSharp => "C# → C#",
        _                                  => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

}
