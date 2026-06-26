using System.Text;

namespace Pharmatechnik.Nav.Language.CodeGen;

public record GenerationOptions {

    public static GenerationOptions Default => new() {
        Force               = false,
        GenerateToClasses   = true,
        GenerateWflClasses  = true,
        GenerateIwflClasses = true,
    };

    public bool Force               { get; init; }
    public bool Strict              { get; init; }
    public bool GenerateToClasses   { get; init; }
    public bool GenerateWflClasses  { get; init; }
    public bool GenerateIwflClasses { get; init; }

    // Schreibt '#nullable enable' in die generierten Dateien. Default: aus.
    // Bewusst opt-in, da der Nullable-Kontext non-nullable Referenztyp-Parameter
    // in die generierten Signaturen propagiert und damit Consumer-Builds brechen kann,
    // die mit möglicherweise-null aufrufen (CS8604/CS8625).
    public bool NullableContext     { get; init; }

    public string ProjectRootDirectory { get; init; }
    public string IwflRootDirectory    { get; init; }
    public string WflRootDirectory     { get; init; }

    public Encoding Encoding => Encoding.UTF8; // Ich sehe keinen Grund, ein anderes Encoding als UTF8 zu verwenden.

}