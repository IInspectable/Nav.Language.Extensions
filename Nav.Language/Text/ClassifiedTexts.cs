namespace Pharmatechnik.Nav.Language.Text;

public static class ClassifiedTexts {

    public static readonly ClassifiedText Space = new(" ", TextClassification.Whitespace);
    public static readonly ClassifiedText Colon = Punctuation(SyntaxFacts.Colon.ToString());

    public static ClassifiedText Text(char c) => Text(c.ToString());
    public static ClassifiedText Text(string text) => new(text,                            TextClassification.Text);
    public static ClassifiedText Keyword(string keyword) => new(keyword,                   TextClassification.Keyword);
    public static ClassifiedText TaskName(string taskName) => new(taskName,                TextClassification.TaskName);
    public static ClassifiedText GuiNode(string formName) => new(formName,                 TextClassification.GuiNode);
    public static ClassifiedText ChoiceNode(string formName) => new(formName,              TextClassification.ChoiceNode);
    public static ClassifiedText MethodName(string formName) => new(formName,              TextClassification.ChoiceNode);
    public static ClassifiedText Identifier(string identifier) => new(identifier,          TextClassification.Identifier);
    public static ClassifiedText ConnectionPoint(string identifier) => new(identifier,     TextClassification.ConnectionPoint);
    public static ClassifiedText Whitespace(string whitespace) => new(whitespace,          TextClassification.Whitespace);
    public static ClassifiedText Punctuation(string punctuation) => new(punctuation,       TextClassification.Punctuation);
    public static ClassifiedText StringLiteral(string stringLiteral) => new(stringLiteral, TextClassification.StringLiteral);

}