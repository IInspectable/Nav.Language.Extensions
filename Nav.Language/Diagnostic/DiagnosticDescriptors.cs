using System;

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Der Katalog aller vordefinierten <see cref="DiagnosticDescriptor"/> der Engine. Die konkreten
/// Deskriptoren sind auf partielle Klassen nach Kategorie verteilt — die geschachtelten Klassen
/// <see cref="Syntax"/> (Lexer/Parser), <see cref="Semantic"/> (semantische Analyse) und
/// <see cref="DeadCode"/> (toter Code). Hier liegen zusätzlich die Fabrikmethoden für Deskriptoren,
/// deren Meldungstext erst zur Laufzeit feststeht. Die Kennungen (<c>NavXXXX</c>) hält
/// <see cref="DiagnosticId"/>; die Bedeutung jedes Codes ist in <c>doc/Errors.md</c> belegt.
/// </summary>
public static partial class DiagnosticDescriptors {

    /// <summary>
    /// Erzeugt einen <see cref="DiagnosticDescriptor"/> für einen internen Engine-Fehler
    /// (<see cref="DiagnosticId.Nav0001"/>, Kategorie <see cref="DiagnosticCategory.Internal"/>,
    /// Schweregrad <see cref="DiagnosticSeverity.Error"/>). Die <see cref="Exception.Message"/> von
    /// <paramref name="ex"/> geht direkt in den Meldungstext ein, weshalb der Text nicht statisch
    /// vordefiniert werden kann.
    /// </summary>
    /// <param name="ex">Die zugrunde liegende Ausnahme, deren Meldung übernommen wird.</param>
    public static DiagnosticDescriptor NewInternalError(Exception ex) {
        return new DiagnosticDescriptor(
            id             : DiagnosticId.Nav0001,
            messageFormat  : "Internal error: " + ex.Message,
            category       : DiagnosticCategory.Internal,
            defaultSeverity: DiagnosticSeverity.Error
        );
    }

    /// <summary>
    /// Erzeugt einen <see cref="DiagnosticDescriptor"/> für einen Syntaxfehler mit frei gewähltem
    /// Meldungstext (<see cref="DiagnosticId.Nav0002"/>, Kategorie
    /// <see cref="DiagnosticCategory.Syntax"/>, Schweregrad <see cref="DiagnosticSeverity.Error"/>).
    /// </summary>
    /// <param name="errorMessage">Der bereits fertige Fehlertext (wird als
    /// <see cref="DiagnosticDescriptor.MessageFormat"/> übernommen).</param>
    public static DiagnosticDescriptor NewSyntaxError(string errorMessage) {
        return new DiagnosticDescriptor(
            id             : DiagnosticId.Nav0002,
            messageFormat  : errorMessage,
            category       : DiagnosticCategory.Syntax,
            defaultSeverity: DiagnosticSeverity.Error
        );
    }       
}