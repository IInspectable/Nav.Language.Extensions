#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language;

public static class IntExtensions {

    public static int Trim(this int value, int start, int end) {
        if (end < start) {
            throw new ArgumentOutOfRangeException(nameof(end));
        }

        return Math.Max(start, Math.Min(end, value));
    }

}