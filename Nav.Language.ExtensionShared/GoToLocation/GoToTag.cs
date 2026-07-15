#region Using Directives

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation; 

/// <summary>
/// VS-Editor-Tag (<see cref="ITag"/>), das eine Textstelle als Sprungziel-Quelle markiert. Es trägt die
/// <see cref="ILocationInfoProvider"/>, aus denen der <see cref="GoToLocationService"/> beim „Go To…" die
/// tatsächlichen Ziele auflöst. Ein Tag kann mehrere Provider bündeln (etwa Deklaration plus Aufrufstelle).
/// </summary>
public class GoToTag: ITag {

    /// <summary>Erzeugt ein Tag ohne Provider; weitere lassen sich über <see cref="Provider"/> ergänzen.</summary>
    public GoToTag() {
        Provider = new List<ILocationInfoProvider>();
    }

    /// <summary>Erzeugt ein Tag mit einem einzelnen <paramref name="provider"/> als Sprungziel-Quelle.</summary>
    public GoToTag(ILocationInfoProvider provider) {
        if(provider == null) {
            throw new ArgumentNullException(nameof(provider));
        }
        Provider = new List<ILocationInfoProvider> { provider };
    }

    /// <summary>Die Provider, aus denen die Sprungziele dieses Tags aufgelöst werden.</summary>
    public List<ILocationInfoProvider> Provider { get; }
}