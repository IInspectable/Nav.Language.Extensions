#region Using Directives

using System;
using System.Collections.Generic;

using Microsoft.VisualStudio.Text.Tagging;

using Pharmatechnik.Nav.Language.Extension.GoToLocation.Provider;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.GoToLocation; 

public class GoToTag: ITag {

    public GoToTag() {
        Provider = new List<ILocationInfoProvider>();
    }

    public GoToTag(ILocationInfoProvider provider) {
        if(provider == null) {
            throw new ArgumentNullException(nameof(provider));
        }
        Provider = new List<ILocationInfoProvider> { provider };
    }

    public List<ILocationInfoProvider> Provider { get; }
}