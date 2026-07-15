#region Using Directives

using Microsoft.CodeAnalysis;

using Pharmatechnik.Nav.Language.Text;

using LinePosition = Pharmatechnik.Nav.Language.Text.LinePosition;

#endregion

namespace Pharmatechnik.Nav.Language.CodeAnalysis.Common; 

/// <summary>
/// Interop-Helfer, die Roslyn-Zeilenpositionstypen in die engine-eigenen Text-Typen übersetzen — die
/// Nahtstelle zwischen <see cref="Microsoft.CodeAnalysis.Text.LinePosition"/> /
/// <see cref="FileLinePositionSpan"/> und <see cref="LinePosition"/> / <see cref="LineRange"/>.
/// </summary>
public static class LinePositionExtensions {

    /// <summary>Übersetzt eine Roslyn-<see cref="Microsoft.CodeAnalysis.Text.LinePosition"/> in die engine-eigene <see cref="LinePosition"/>.</summary>
    /// <param name="linePosition">Die Roslyn-Zeilenposition (Zeile und Spalte).</param>
    public static LinePosition ToLinePosition(this Microsoft.CodeAnalysis.Text.LinePosition linePosition) {
        return new(linePosition.Line, linePosition.Character);
    }

    /// <summary>Übersetzt eine Roslyn-<see cref="FileLinePositionSpan"/> in die engine-eigene <see cref="LineRange"/> (Start- bis End-Zeilenposition).</summary>
    /// <param name="fileLinePositionSpan">Die Roslyn-Zeilen-Span (Start- und End-Position).</param>
    public static LineRange ToLineRange(this FileLinePositionSpan fileLinePositionSpan) {
        return new(
            fileLinePositionSpan.StartLinePosition.ToLinePosition(),
            fileLinePositionSpan.EndLinePosition.ToLinePosition());
    }

}