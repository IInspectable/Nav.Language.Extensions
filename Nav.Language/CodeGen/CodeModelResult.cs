#nullable enable

#region Using Directives

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

// ReSharper disable InconsistentNaming

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

sealed class CodeModelResult {

    public CodeModelResult(
        ITaskDefinitionSymbol taskDefinition,
        IBeginWfsCodeModel? beginWfsCodeModel,
        IWfsCodeModel? iwfsCodeModel,
        WfsBaseCodeModel? wfsBaseCodeModel,
        WfsCodeModel? wfsCodeModel,
        IEnumerable<TOCodeModel>? toCodeModels) {

        TaskDefinition     = taskDefinition ?? throw new ArgumentNullException(nameof(taskDefinition));
        IBeginWfsCodeModel = beginWfsCodeModel;
        IWfsCodeModel      = iwfsCodeModel;
        WfsBaseCodeModel   = wfsBaseCodeModel;
        WfsCodeModel       = wfsCodeModel;
        TOCodeModels       = (toCodeModels ?? Enumerable.Empty<TOCodeModel>()).ToImmutableList();
    }

    public ITaskDefinitionSymbol TaskDefinition { get; }

    public IBeginWfsCodeModel? IBeginWfsCodeModel { get; }

    public IWfsCodeModel? IWfsCodeModel { get; }

    public WfsBaseCodeModel? WfsBaseCodeModel { get; }

    public WfsCodeModel? WfsCodeModel { get; }

    public ImmutableList<TOCodeModel> TOCodeModels { get; }

}