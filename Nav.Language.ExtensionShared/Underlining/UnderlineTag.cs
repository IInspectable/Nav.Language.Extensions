using Microsoft.VisualStudio.Text.Tagging;

namespace Pharmatechnik.Nav.Language.Extension.Underlining; 

/// <summary>
/// Leerer Marker-Tag, der eine zu unterstreichende Textstelle kennzeichnet. Der
/// <see cref="UnderlineTagger"/> verwaltet diese logischen Tags; der
/// <see cref="Pharmatechnik.Nav.Language.Extension.Classification.UnderlineClassifier"/> übersetzt sie
/// in die sichtbare Underline-Klassifizierung.
/// </summary>
public class UnderlineTag: ITag {

}