#nullable enable

namespace Pharmatechnik.Nav.Language.Text;

public sealed class ClassifiedText {

    public ClassifiedText(string? text, TextClassification classification) {
        Text           = text ?? "";
        Classification = classification;

    }

    public string             Text           { get; }
    public TextClassification Classification { get; }

    public override string ToString() => Text;

}