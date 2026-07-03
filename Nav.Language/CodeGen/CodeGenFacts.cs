#region Using Directives

using System;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen; 

public static partial class CodeGenFacts {

    internal static string BuildQualifiedName(params string[] identifier) {
        var parts = identifier.Where(part => !String.IsNullOrEmpty(part)).ToList();

        if (!parts.Any()) {
            return String.Empty;
        }

        return String.Join(".", parts);
    }

}