#region Using Directives

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

#endregion

namespace Pharmatechnik.Nav.Language.Extension; 

/// <summary>
/// Definiert den ContentType, den Sprachnamen und die Dateiendung der Nav Language und exportiert die
/// zugehörigen MEF-Definitionen (<see cref="ContentTypeDefinition"/>,
/// <see cref="FileExtensionToContentTypeDefinition"/>), mit denen Visual Studio <c>.nav</c>-Dateien dem
/// Nav-ContentType (abgeleitet vom Basis-ContentType <c>code</c>) zuordnet.
/// </summary>
sealed class NavLanguageContentDefinitions
{
    /// <summary>Der Name des Nav-ContentTypes.</summary>
    public const string ContentType   = "Nav";
    /// <summary>Der Name der Nav-Sprache (Language Service).</summary>
    public const string LanguageName  = "Nav";
    /// <summary>Die Dateiendung der Nav Language.</summary>
    public const string FileExtension = ".nav";

    /// <summary>
    /// MEF-exportierte <see cref="ContentTypeDefinition"/> des Nav-ContentTypes (abgeleitet von <c>code</c>).
    /// </summary>
    [Export]
    [Name(ContentType)]
    [BaseDefinition("code")]
    internal ContentTypeDefinition GuiModelContentTypeDefinition = null;

    /// <summary>
    /// MEF-exportierte Zuordnung der Dateiendung <see cref="FileExtension"/> zum Nav-ContentType.
    /// </summary>
    [Export]
    [ContentType(ContentType)]
    [FileExtension(FileExtension)]
    internal FileExtensionToContentTypeDefinition GuiModelFileExtensionDefinition = null;
}