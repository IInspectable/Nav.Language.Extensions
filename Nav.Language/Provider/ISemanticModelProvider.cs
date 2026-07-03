#region Using Directives

using System;
using System.Threading;

#endregion

namespace Pharmatechnik.Nav.Language;

public interface ISemanticModelProvider: IDisposable {

    CodeGenerationUnit? GetSemanticModel(string filePath, CancellationToken cancellationToken = default);
    CodeGenerationUnit  GetSemanticModel(CodeGenerationUnitSyntax syntax, CancellationToken cancellationToken = default);

}