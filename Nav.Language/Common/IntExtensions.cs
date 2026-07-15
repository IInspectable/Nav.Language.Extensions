#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Erweiterungsmethoden für <see cref="int"/>.
/// </summary>
public static class IntExtensions {

    /// <summary>
    /// Beschränkt <paramref name="value"/> auf das geschlossene Intervall
    /// [<paramref name="start"/>, <paramref name="end"/>]: kleinere Werte werden auf
    /// <paramref name="start"/>, größere auf <paramref name="end"/> gezogen.
    /// </summary>
    /// <param name="value">Der zu beschränkende Wert.</param>
    /// <param name="start">Die untere Grenze (einschließlich).</param>
    /// <param name="end">Die obere Grenze (einschließlich).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="end"/> ist kleiner als <paramref name="start"/>.</exception>
    public static int Trim(this int value, int start, int end) {
        if (end < start) {
            throw new ArgumentOutOfRangeException(nameof(end));
        }

        return Math.Max(start, Math.Min(end, value));
    }

}