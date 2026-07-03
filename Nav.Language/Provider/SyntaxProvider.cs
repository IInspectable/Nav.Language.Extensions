#nullable enable

#region Using Directives

using System.IO;
using System.Text;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language;

public class SyntaxProvider: ISyntaxProvider {

    public static readonly ISyntaxProvider Default = new SyntaxProvider();

    public virtual CodeGenerationUnitSyntax? GetSyntax(string filePath, CancellationToken cancellationToken = default) {

        if (!File.Exists(filePath)) {
            return null;
        }

        var content    = ReadAllText(filePath);
        var syntaxTree = Syntax.ParseCodeGenerationUnit(text: content, filePath: filePath, cancellationToken: cancellationToken);

        return syntaxTree;
    }

    public virtual void Dispose() {
    }

    static string ReadAllText(string filePath) {

        using var sr = new StreamReader(path: filePath,
                                        encoding: Encoding.Default,
                                        detectEncodingFromByteOrderMarks: true);
        return sr.ReadToEnd();

    }

}