using Microsoft.CSharp;

namespace Pharmatechnik.Nav.Language.CodeGen; 

// TODO Gerne in eine andere Klasse
static class CSharp {

    static readonly CSharpCodeProvider CodeProvider =new();

    public static bool IsValidIdentifier(string value) {
        return CodeProvider.IsValidIdentifier(value);
    }
}