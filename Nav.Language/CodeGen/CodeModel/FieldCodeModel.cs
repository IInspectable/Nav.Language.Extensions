#nullable enable

namespace Pharmatechnik.Nav.Language.CodeGen;

class FieldCodeModel : ParameterCodeModel {

    public FieldCodeModel(string parameterType, string name): base(parameterType, name) {
    }

    public override string ParameterName => $"{CodeGenFacts.FieldPrefix}{base.ParameterName}";
}