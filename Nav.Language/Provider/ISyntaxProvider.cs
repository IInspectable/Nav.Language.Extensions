#nullable enable

#region Using Directives

using System;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language;

public interface ISyntaxProvider : IDisposable {
    CodeGenerationUnitSyntax? GetSyntax(string filePath, CancellationToken cancellationToken = default);
}