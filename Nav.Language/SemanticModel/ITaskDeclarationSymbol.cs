#region Using Directives

using System.Collections.Generic;

using JetBrains.Annotations;

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
    [CanBeNull]
    MemberDeclarationSyntax Syntax { get; }

    /// <summary>
    /// Ist nur dann null, wenn IsIncluded true, da wir keine Syntaxbäume von anderen nav-Dateien 
    /// im Speicher halten wollen.
    /// </summary>
    [CanBeNull]
    CodeGenerationUnit CodeGenerationUnit { get; }

    [NotNull]
    IReadOnlySymbolCollection<IConnectionPointSymbol> ConnectionPoints { get; }

    IReadOnlySymbolCollection<IInitConnectionPointSymbol> Inits();
    IReadOnlySymbolCollection<IExitConnectionPointSymbol> Exits();
    IReadOnlySymbolCollection<IEndConnectionPointSymbol> Ends();

    [NotNull]
    IReadOnlyList<ITaskNodeSymbol> References { get; }

    /// <summary>
    /// Gibt an, ob die Deklaration aus eine inkludierten nav-Datei stammt.
    /// </summary>
    bool IsIncluded { get; }

    /// <summary>
    /// Gibt an, ob die Deklaration aus einer reinen Deklaration (taskref ...) oder einer Definition (task ...) entstammt.
    /// </summary>
    TaskDeclarationOrigin Origin { get; }

    [NotNull]
    string CodeNamespace { get; }

    bool CodeNotImplemented { get; }

    [CanBeNull]
    ICodeParameter CodeTaskResult { get; }

}