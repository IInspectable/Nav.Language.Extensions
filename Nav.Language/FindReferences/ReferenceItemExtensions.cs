#region Using Directives

using System.Collections.Generic;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

/// <summary>
/// Erweiterungsmethoden für Folgen von <see cref="ReferenceItem"/>.
/// </summary>
public static class ReferenceItemExtensions {

    /// <summary>
    /// Sortiert die Fundstellen nach ihrer Position — zuerst nach Start-Zeile, dann nach Start-Spalte
    /// (siehe <see cref="ReferenceItem.Location"/>).
    /// </summary>
    /// <param name="referenceItems">Die zu sortierenden Fundstellen.</param>
    public static IOrderedEnumerable<ReferenceItem> OrderByLocation(this IEnumerable<ReferenceItem> referenceItems) {
        return referenceItems.OrderBy(s => s.Location.StartLine).ThenBy(s => s.Location.StartCharacter);

    }

}