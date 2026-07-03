// ReSharper disable InconsistentNaming

namespace Pharmatechnik.Nav.Language;

public interface IPathProvider {

    string SyntaxFileName    { get; }
    string WfsBaseFileName   { get; }
    string IWfsFileName      { get; }
    string IBeginWfsFileName { get; }
    string WfsFileName       { get; }

    string GetToFileName(string toClassName);
    string GetRelativePath(string fromPath, string toPath);

}