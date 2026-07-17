#region Using Directives

using System.IO;
using System.Text;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Der Standard-<see cref="ISyntaxProvider"/>: liest die Datei vom Dateisystem (BOM-Erkennung) und
/// parst sie. Ohne eigenen Cache — jeder Aufruf liest und parst neu (siehe
/// <see cref="CachedSyntaxProvider"/> für die cachende Variante).
/// </summary>
public class SyntaxProvider: ISyntaxProvider {

    /// <summary>Die gemeinsam nutzbare Standard-Instanz.</summary>
    public static readonly ISyntaxProvider Default = new SyntaxProvider();

    /// <inheritdoc/>
    public virtual CodeGenerationUnitSyntax? GetSyntax(string filePath, CancellationToken cancellationToken = default) {

        if (!File.Exists(filePath)) {
            return null;
        }

        var content    = ReadAllText(filePath);
        var syntaxTree = Syntax.ParseCodeGenerationUnit(text: content, filePath: filePath, cancellationToken: cancellationToken);

        return syntaxTree;
    }

    /// <inheritdoc/>
    public virtual void Dispose() {
    }

    static string ReadAllText(string filePath) {

        using var sr = new StreamReader(path: filePath,
                                        encoding: Encoding.Default,
                                        detectEncodingFromByteOrderMarks: true);
        return sr.ReadToEnd();

    }

}