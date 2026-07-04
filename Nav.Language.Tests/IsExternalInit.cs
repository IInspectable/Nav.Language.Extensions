// Polyfill: `init`-Accessoren und `record`-Typen (C# 9) benötigen den Marker-Typ
// System.Runtime.CompilerServices.IsExternalInit. Er ist erst ab .NET 5 im BCL enthalten; für den
// net472-Build des Testprojekts liefern wir ihn hier nach. Auf net5.0+ (u.a. net10.0) stammt der Typ
// aus dem BCL — die Direktive blendet den Polyfill dort aus, damit es keinen Doppel-Typ gibt.

#if !NET5_0_OR_GREATER

using System.ComponentModel;
#pragma warning disable IDE0130

namespace System.Runtime.CompilerServices {

    [EditorBrowsable(EditorBrowsableState.Never)]
    static class IsExternalInit { }

}

#endif
