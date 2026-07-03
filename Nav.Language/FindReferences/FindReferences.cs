#region Using Directives

using System;
using System.Threading.Tasks;

#endregion

namespace Pharmatechnik.Nav.Language.FindReferences;

public class ReferenceFinder {

    public static Task FindReferencesAsync(FindReferencesArgs args) {

        if (args == null) {
            throw new ArgumentNullException(nameof(args));
        }

        return FindReferencesVisitor.Invoke(args);

    }

}