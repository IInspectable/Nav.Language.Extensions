#region Using Directives

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

public static class ReferenceItemExtensions {

    public static IOrderedEnumerable<ReferenceItem> OrderByLocation(this IEnumerable<ReferenceItem> referenceItems) {
        return referenceItems.OrderBy(s => s.Location.StartLine).ThenBy(s => s.Location.StartCharacter);

    }

}