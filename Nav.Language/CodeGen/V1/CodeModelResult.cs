#region Using Directives

using System;

// ReSharper disable InconsistentNaming

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

sealed class CodeModelResult {

    public CodeModelResult(
        ITaskDefinitionSymbol taskDefinition,
        IBeginWfsCodeModel? beginWfsCodeModel,
        IWfsCodeModel? iwfsCodeModel,
        WfsBaseCodeModel? wfsBaseCodeModel,
        WfsCodeModel? wfsCodeModel) {

        TaskDefinition     = taskDefinition ?? throw new ArgumentNullException(nameof(taskDefinition));
        IBeginWfsCodeModel = beginWfsCodeModel;
        IWfsCodeModel      = iwfsCodeModel;
        WfsBaseCodeModel   = wfsBaseCodeModel;
        WfsCodeModel       = wfsCodeModel;
    }

    public ITaskDefinitionSymbol TaskDefinition { get; }

    public IBeginWfsCodeModel? IBeginWfsCodeModel { get; }

    public IWfsCodeModel? IWfsCodeModel { get; }

    public WfsBaseCodeModel? WfsBaseCodeModel { get; }

    public WfsCodeModel? WfsCodeModel { get; }

}