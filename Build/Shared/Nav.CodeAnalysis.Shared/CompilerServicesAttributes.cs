// Polyfills, damit netstandard2.0-Generatorprojekte moderne C#-Sprachfeatures (Records mit
// init-Accessoren) nutzen können. Diese Typen sind erst ab .NET 5 im BCL enthalten.

namespace System.Runtime.CompilerServices {

    using ComponentModel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    static class IsExternalInit {

    }

}
