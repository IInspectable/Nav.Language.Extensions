using System;

namespace Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols; 

/// <summary>
/// Wird von <see cref="LocationFinder"/> geworfen, wenn sich zu einem Nav-Symbol bzw. einer Annotation keine
/// <see cref="Location"/> im Ziel auflösen lässt (fehlendes Symbol/Interface/Member oder eine ungültige
/// Location). Die <see cref="System.Exception.Message"/> nennt das nicht auffindbare Element.
/// </summary>
public class LocationNotFoundException : Exception {

    /// <summary>Erzeugt die Ausnahme mit einer Meldung, die das nicht auffindbare Element beschreibt.</summary>
    /// <param name="message">Die Fehlermeldung.</param>
    public LocationNotFoundException(string message): base(message) {
            
    }       
}