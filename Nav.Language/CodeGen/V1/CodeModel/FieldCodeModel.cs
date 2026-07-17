namespace Pharmatechnik.Nav.Language.CodeGen;

/// <summary>
/// Ein <see cref="ParameterCodeModel"/>, dessen <see cref="ParameterName"/> das Feld-Präfix
/// (<see cref="CodeGenFacts.FieldPrefix"/>, <c>_</c>) trägt — bildet einen Parameter auf sein
/// gleichnamiges Backing-Feld ab (z.B. Parameter <c>messageboxOk</c> → Feld <c>_messageboxOk</c>). Der
/// <see cref="WfsBaseEmitter"/> nutzt dies, um in den Logic-Aufrufen der Transitionen die injizierten
/// Sub-Task-Wrapper als Felder statt als Parameter einzusetzen (siehe
/// <c>TransitionCodeModel.TaskBeginFields</c>).
/// </summary>
class FieldCodeModel : ParameterCodeModel {

    public FieldCodeModel(string parameterType, string name): base(parameterType, name) {
    }

    /// <summary>Der um das Feld-Präfix (<see cref="CodeGenFacts.FieldPrefix"/>) ergänzte Basis-Parametername.</summary>
    public override string ParameterName => $"{CodeGenFacts.FieldPrefix}{base.ParameterName}";
}