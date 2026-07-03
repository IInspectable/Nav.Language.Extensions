#nullable enable

#region Using Directives

using System;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

class TaskBeginCodeModel : CodeModel {

    public TaskBeginCodeModel(string? taskNodeName,
                              ParameterCodeModel taskBeginParameter,
                              ImmutableList<ParameterCodeModel> taskParameter,
                              bool notImplemented = false) {

        TaskNodeName       = taskNodeName       ?? String.Empty;
        TaskBeginParameter = taskBeginParameter ?? throw new ArgumentNullException(nameof(taskBeginParameter));
        TaskParameter      = taskParameter      ?? throw new ArgumentNullException(nameof(taskParameter));
        NotImplemented     = notImplemented;
    }

    public string TaskNodeName           { get; }
    public string TaskNodeNamePascalcase => TaskNodeName.ToPascalcase();
    /// <summary>
    /// Parameter, der das IBegin...WFS interface des Tasks darstellt.
    /// </summary>
    public ParameterCodeModel TaskBeginParameter { get; }
    /// <summary>
    /// Die Parameter, die zum Aufrufen des Tasks nötig sind.
    /// </summary>
    public ImmutableList<ParameterCodeModel> TaskParameter { get; }
    /// <summary>
    /// Gibt an, ob der Task nicht implementiert ist.
    /// </summary>
    public bool NotImplemented { get; }

}