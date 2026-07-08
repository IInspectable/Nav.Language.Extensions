using System;

namespace Pharmatechnik.Nav.Language.CodeAnalysis.FindSymbols; 

public class LocationNotFoundException : Exception {

    public LocationNotFoundException(string message): base(message) {
            
    }       
}