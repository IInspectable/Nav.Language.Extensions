#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

// ReSharper disable InconsistentNaming

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen; 

public sealed class CodeGenerationResult {

    public CodeGenerationResult(
        ITaskDefinitionSymbol taskDefinition,
        CodeGenerationSpec iBeginWfsCodeSpec,
        CodeGenerationSpec iWfsCodeSpec,
        CodeGenerationSpec wfsBaseCodeSpec,
        CodeGenerationSpec wfsCodeSpec,
        IEnumerable<CodeGenerationSpec>? toCodeSpecs) {

        TaskDefinition    = taskDefinition    ?? throw new ArgumentNullException(nameof(taskDefinition));
        IBeginWfsCodeSpec = iBeginWfsCodeSpec ?? throw new ArgumentNullException(nameof(iBeginWfsCodeSpec));
        IWfsCodeSpec      = iWfsCodeSpec      ?? throw new ArgumentNullException(nameof(iWfsCodeSpec));
        WfsBaseCodeSpec   = wfsBaseCodeSpec   ?? throw new ArgumentNullException(nameof(wfsBaseCodeSpec));
        WfsCodeSpec       = wfsCodeSpec       ?? throw new ArgumentNullException(nameof(wfsCodeSpec));
        ToCodeSpecs       = (toCodeSpecs ?? Enumerable.Empty<CodeGenerationSpec>()).ToImmutableList();
    }

    public ITaskDefinitionSymbol             TaskDefinition    { get; }
    public CodeGenerationSpec                IBeginWfsCodeSpec { get; }
    public CodeGenerationSpec                IWfsCodeSpec      { get; }
    public CodeGenerationSpec                WfsBaseCodeSpec   { get; }
    public CodeGenerationSpec                WfsCodeSpec       { get; }
    public ImmutableList<CodeGenerationSpec> ToCodeSpecs       { get; }

}