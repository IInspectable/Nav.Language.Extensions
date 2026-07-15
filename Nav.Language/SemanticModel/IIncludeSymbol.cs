#region Using Directives

using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Symbol einer Include-Direktive <c>taskref "datei.nav";</c> — bindet eine andere <c>.nav</c>-Datei
/// ein und macht deren Task-Deklarationen (<see cref="TaskDeclarations"/>) in der einbindenden Datei
/// referenzierbar. <see cref="ISymbol.Location"/> ist das String-Literal der Direktive;
/// <see cref="ISymbol.Name"/> ist der kleingeschriebene vollständige Dateipfad und dient als
/// Dedup-Schlüssel (ein Doppel-Include derselben Datei meldet Nav1001). Entsteht im
/// <see cref="TaskDeclarationSymbolBuilder"/> aus der <see cref="IncludeDirectiveSyntax"/>;
/// publiziert über <see cref="CodeGenerationUnit.Includes"/>.
/// </summary>
public interface IIncludeSymbol: ISymbol {

    /// <summary>
    /// Der vollständig aufgelöste Pfad der eingebundenen Datei in originaler Schreibweise —
    /// relative Pfadangaben der Direktive sind bereits gegen das Verzeichnis der einbindenden
    /// Datei aufgelöst.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// Die Fundstelle „Anfang der eingebundenen Datei": eine <see cref="Location"/>, die nur den
    /// Dateipfad trägt (Position 0/0) — das Sprungziel von GoTo auf der Direktive. Die Fundstelle
    /// der Direktive selbst in der einbindenden Datei ist <see cref="ISymbol.Location"/>.
    /// </summary>
    Location FileLocation { get; }

    /// <summary>
    /// Die zugrunde liegende Include-Direktive im Syntaxbaum der einbindenden Datei.
    /// </summary>
    IncludeDirectiveSyntax Syntax { get; }

    /// <summary>
    /// Die Diagnostics der eingebundenen Datei: ihre Syntax-Fehler vereinigt mit den Diagnosen
    /// der Deklarations-Extraktion. Enthält die Liste Fehler, wird das an der Direktive
    /// zusammengefasst als Nav0005 gemeldet.
    /// </summary>
    IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// Die aus der eingebundenen Datei extrahierten Task-Deklarationen
    /// (<see cref="ITaskDeclarationSymbol.IsIncluded"/> ist <c>true</c>); sie werden zusätzlich in
    /// die Deklarationstabelle der einbindenden Datei übernommen
    /// (<see cref="CodeGenerationUnit.TaskDeclarations"/>) und sind dort für Task-Knoten auflösbar.
    /// </summary>
    IReadOnlySymbolCollection<ITaskDeclarationSymbol> TaskDeclarations { get; }

}
