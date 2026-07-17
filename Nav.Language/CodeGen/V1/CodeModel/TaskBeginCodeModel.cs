#region Using Directives

using System;
using System.Collections.Immutable;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Modelliert eine generierte <c>Begin{Node}</c>-Wrapper-Methode der <c>{Task}WFSBase</c> — eine Überladung je
/// <c>init</c>-Knoten des aufgerufenen Sub-Tasks. Der <see cref="WfsBaseEmitter"/> schreibt daraus
/// <c>protected INavCommandBody Begin{Node}(IBegin{Task}WFS wfs, {TaskParameter…}) => new TaskCall({Node}NodeName, () =&gt; wfs.Begin(…))</c>;
/// bei <see cref="NotImplemented"/> entfällt der Begin-Thunk (<c>new TaskCall(…, null)</c>). Gebündelt je Task-Knoten
/// im <see cref="BeginWrapperCodeModel"/>.
/// </summary>
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

    /// <summary>Der Name des aufgerufenen Task-Knotens (Basis der <c>{Node}NodeName</c>-Konstante und der <c>&lt;NavInitCall&gt;</c>-Zuordnung).</summary>
    public string TaskNodeName           { get; }
    /// <summary>Der Knotenname in Pascalcase — bildet den Methodennamen <c>Begin{Node}</c>.</summary>
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