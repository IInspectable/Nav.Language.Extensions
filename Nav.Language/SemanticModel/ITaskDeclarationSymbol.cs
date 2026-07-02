#nullable enable

#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

public enum TaskDeclarationOrigin {

    TaskDeclaration,
    TaskDefinition

}

public interface ITaskDeclarationSymbol: ISymbol {

    /// <summary>
    /// Ist nur dann null, wenn IsIncluded true, da wir keine Syntaxbäume von anderen nav-Dateien
    /// im Speicher halten wollen.
    /// </summary>
    MemberDeclarationSyntax? Syntax { get; }

    /// <summary>
    /// Ist nur dann null, wenn IsIncluded true, da wir keine Syntaxbäume von anderen nav-Dateien
    /// im Speicher halten wollen.
    /// </summary>
    CodeGenerationUnit? CodeGenerationUnit { get; }

    IReadOnlySymbolCollection<IConnectionPointSymbol> ConnectionPoints { get; }

    IEnumerable<IInitConnectionPointSymbol> Inits();
    IEnumerable<IExitConnectionPointSymbol> Exits();
    IEnumerable<IEndConnectionPointSymbol> Ends();

    IReadOnlyList<ITaskNodeSymbol> References { get; }

    /// <summary>
    /// Gibt an, ob die Deklaration aus eine inkludierten nav-Datei stammt.
    /// </summary>
    bool IsIncluded { get; }

    /// <summary>
    /// Gibt an, ob die Deklaration aus einer reinen Deklaration (taskref ...) oder einer Definition (task ...) entstammt.
    /// </summary>
    TaskDeclarationOrigin Origin { get; }

    string CodeNamespace { get; }

    bool CodeNotImplemented { get; }

    ICodeParameter? CodeTaskResult { get; }

}
