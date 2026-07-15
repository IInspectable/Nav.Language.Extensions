namespace Pharmatechnik.Nav.Language.Extension.FindReferences; 

/// <summary>
/// Eine reine Text-Ersatzzeile im „Find All References"-Fenster ohne eigene Location — etwa
/// „Search found no results". Trägt nur den Anzeigetext.
/// </summary>
class SimpleTextEntry: Entry {

    SimpleTextEntry(FindReferencesPresenter presenter, DefinitionEntry definition, string text)
        : base(presenter, definition) {
        Text = text;
    }

    /// <summary>Factory für eine Text-Ersatzzeile unter dem gegebenen Definitionsknoten.</summary>
    public static SimpleTextEntry Create(FindReferencesPresenter presenter, DefinitionEntry definition, string text) {
        return new SimpleTextEntry(presenter, definition, text);
    }

    /// <inheritdoc/>
    public override string Text { get; }

}