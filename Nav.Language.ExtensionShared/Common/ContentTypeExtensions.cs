#region Using Directives

using System.Linq;
using System.Collections.Generic;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Common; 

/// <summary>
/// Erweiterungsmethoden für den <see cref="IContentType"/> des VS-Editors.
/// </summary>
static class ContentTypeExtensions {

    /// <summary>
    /// Prüft, ob der Content-Type mindestens einem der angegebenen Content-Type-Namen entspricht
    /// (direkt oder über eine Basis-Beziehung, vgl. <see cref="IContentType.IsOfType"/>).
    /// </summary>
    /// <param name="dataContentType">Der zu prüfende Content-Type.</param>
    /// <param name="extensionContentTypes">Die Namen der in Frage kommenden Content-Types.</param>
    /// <returns><c>true</c>, wenn eine Übereinstimmung besteht.</returns>
    public static bool MatchesAny(this IContentType dataContentType, IEnumerable<string> extensionContentTypes) {
        return extensionContentTypes.Any(dataContentType.IsOfType);
    }
}