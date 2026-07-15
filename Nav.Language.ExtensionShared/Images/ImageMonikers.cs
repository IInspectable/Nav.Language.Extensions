#region Using Directives

using System;

using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.CodeFixes;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Images; 

/// <summary>
/// Zentrale Zuordnung semantischer Namen zu Visual-Studio-Icons (<see cref="Microsoft.VisualStudio.Imaging.Interop.ImageMoniker"/>) für die
/// Editor-Integration der Nav Language — Icons für Symbole, Knoten, ConnectionPoints, Kanten, Trigger,
/// Code-Aktionen sowie Analyse- und GoTo-Zustände. Die meisten Icons stammen aus
/// <see cref="KnownMonikers"/>; einige Kanten-Icons sind eigene, über <see cref="CustomMonikerGuid"/>
/// adressierte Custom-Monikers.
/// </summary>
public static partial class ImageMonikers {

    /// <summary>
    /// Die <see cref="Guid"/> der projekteigenen Custom-Icon-Sammlung (Kanten- und Continuation-Icons,
    /// die es nicht als <see cref="KnownMonikers"/> gibt).
    /// </summary>
    static readonly Guid CustomMonikerGuid = new("{11e9628b-b9e6-45d6-ae8d-b4440be46fa6}");

    /// <summary>Icon für einen Projektknoten.</summary>
    public static ImageMoniker ProjectNode  => KnownMonikers.CSProjectNode;
    /// <summary>Icon für einen geschlossenen Ordner.</summary>
    public static ImageMoniker FolderClosed => KnownMonikers.FolderClosed;
    /// <summary>Icon für den übergeordneten Ordner (eine Ebene aufwärts).</summary>
    public static ImageMoniker ParentFolder => KnownMonikers.MoveUp;
    /// <summary>Icon für eine <c>.nav</c>-Datei.</summary>
    public static ImageMoniker NavFile      => KnownMonikers.ActivityDiagram; //KnownMonikers.ClassFile;
    /// <summary>Icon für eine generische Textdatei.</summary>
    public static ImageMoniker File         => KnownMonikers.TextFile;
    /// <summary>Icon für eine Informationsmeldung.</summary>
    public static ImageMoniker Information  => KnownMonikers.StatusInformation;

    // CodeFixImpact

    /// <summary>
    /// Liefert das Status-Icon zur Auswirkung (<see cref="CodeFixImpact"/>) eines Code-Fixes.
    /// </summary>
    /// <param name="impact">Die Auswirkung des Code-Fixes.</param>
    /// <returns>Das passende Icon; <c>default</c> bei <see cref="CodeFixImpact.None"/>.</returns>
    public static ImageMoniker FromCodeFixImpact(CodeFixImpact impact) {
        switch (impact) {
            case CodeFixImpact.None:
                return default;
            case CodeFixImpact.Medium:
                return KnownMonikers.StatusWarningOutline;
            case CodeFixImpact.High:
                return KnownMonikers.StatusInvalidOutline;
            default:
                return default;
        }
    }

    // Analysis

    /// <summary>Icon für eine noch laufende Analyse.</summary>
    public static ImageMoniker WaitingForAnalysis => KnownMonikers.Loading;
    /// <summary>Icon für eine fehlerfreie Analyse.</summary>
    public static ImageMoniker AnalysisOK         => KnownMonikers.StatusOK;
    /// <summary>Icon für eine Analyse mit Warnungen.</summary>
    public static ImageMoniker AnalysisWarning    => KnownMonikers.StatusWarning;
    /// <summary>Icon für eine Analyse mit Fehlern.</summary>
    public static ImageMoniker AnalysisError      => KnownMonikers.StatusError;

    // GoTo

    /// <summary>
    /// Icon für den Sprung von der <c>.nav</c>-Datei zum generierten C#-Code (Nav → C#).
    /// </summary>
    public static ImageMoniker GoToDeclaration => KnownMonikers.GoToDefinition;

    /// <summary>
    /// Icon für den Sprung vom generierten C#-Code zurück zur <c>.nav</c>-Datei (C# → Nav).
    /// </summary>
    public static ImageMoniker GoToDefinition => KnownMonikers.GoToDeclaration;

    /// <summary>Icon für eine Include-Direktive.</summary>
    public static ImageMoniker Include             => NavFile;
    /// <summary>Icon für den Sprung zur Deklaration eines Knotens.</summary>
    public static ImageMoniker GoToNodeDeclaration => KnownMonikers.GoToReference;
    /// <summary>Icon für eine öffentliche Methode.</summary>
    public static ImageMoniker GoToMethodPublic    => KnownMonikers.MethodPublic;
    /// <summary>Icon für eine öffentliche Klasse.</summary>
    public static ImageMoniker GoToClassPublic     => KnownMonikers.ClassPublic;
    /// <summary>Icon für eine öffentliche Schnittstelle.</summary>
    public static ImageMoniker GoToInterfacePublic => KnownMonikers.InterfacePublic;
    /// <summary>Icon für eine C#-Datei.</summary>
    public static ImageMoniker CSharpFile          => KnownMonikers.CSFileNode;

    // Keyword
    /// <summary>Icon für ein Schlüsselwort (IntelliSense).</summary>
    public static ImageMoniker Keyword => KnownMonikers.IntellisenseKeyword;

    // Actions

    /// <summary>Icon für die Aktion „Kante hinzufügen".</summary>
    public static ImageMoniker AddEdge              => KnownMonikers.AddAssociation;
    /// <summary>Icon für die Aktion „Knoten umbenennen".</summary>
    public static ImageMoniker RenameNode           => KnownMonikers.Rename;
    /// <summary>Icon für die Aktion „Knoten einfügen".</summary>
    public static ImageMoniker InsertNode           => KnownMonikers.InsertClause;
    /// <summary>Icon für die Aktion „Anführungszeichen entfernen".</summary>
    public static ImageMoniker DeleteQuotationMarks => KnownMonikers.PendingDeleteNode;
    /// <summary>Icon für die Aktion „ungenutztes Symbol entfernen".</summary>
    public static ImageMoniker RemoveUnusedSymbol   => KnownMonikers.PendingDeleteNode;
    /// <summary>Icon für die Aktion „Semikolon hinzufügen".</summary>
    public static ImageMoniker AddSemicolon         => KnownMonikers.PendingAddNode;
    /// <summary>Icon für die Aktion „Sprachversion setzen".</summary>
    public static ImageMoniker SetLanguageVersion   => KnownMonikers.IntellisenseKeyword;
    /// <summary>Icon für die Aktion „Direktive an den Anfang verschieben".</summary>
    public static ImageMoniker MoveDirectiveToTop   => KnownMonikers.MoveUp;

    // Symbols

    /// <summary>Icon für eine Task-Deklaration.</summary>
    public static ImageMoniker TaskDeclaration     => KnownMonikers.Interface; //KnownMonikers.WorkflowInterop;
    /// <summary>Icon für einen Init-ConnectionPoint.</summary>
    public static ImageMoniker InitConnectionPoint => KnownMonikers.InputPin;
    /// <summary>Icon für einen Exit-ConnectionPoint.</summary>
    public static ImageMoniker ExitConnectionPoint => KnownMonikers.OutputPin;
    /// <summary>Icon für einen End-ConnectionPoint.</summary>
    public static ImageMoniker EndConnectionPoint  => KnownMonikers.ActivityFinalNode;
    /// <summary>Icon für eine Task-Definition.</summary>
    public static ImageMoniker TaskDefinition      => KnownMonikers.Task; //KnownMonikers.CSWorkflow;
    /// <summary>Icon für einen Init-Knoten.</summary>
    public static ImageMoniker InitNode            => KnownMonikers.InputPin;
    /// <summary>Icon für einen Exit-Knoten.</summary>
    public static ImageMoniker ExitNode            => KnownMonikers.OutputPin;
    /// <summary>Icon für einen End-Knoten.</summary>
    public static ImageMoniker EndNode             => KnownMonikers.ActivityFinalNode;
    /// <summary>Icon für einen Task-Knoten.</summary>
    public static ImageMoniker TaskNode            => KnownMonikers.Task; //KnownMonikers.CSWorkflow;
    /// <summary>Icon für einen Choice-Knoten.</summary>
    public static ImageMoniker ChoiceNode          => KnownMonikers.DecisionNode;
    /// <summary>Icon für einen View-Knoten.</summary>
    public static ImageMoniker ViewNode            => KnownMonikers.WindowsForm;
    /// <summary>Icon für einen Dialog-Knoten.</summary>
    public static ImageMoniker DialogNode          => KnownMonikers.Dialog;
    /// <summary>Icon für einen Signal-Trigger.</summary>
    public static ImageMoniker SignalTrigger       => KnownMonikers.EventTrigger;
    /// <summary>Icon für eine Kante.</summary>
    public static ImageMoniker Edge                => KnownMonikers.AssociationRelationship;
    /// <summary>Icon für eine modale Kante (Custom-Moniker).</summary>
    public static ImageMoniker ModalEdge           => new() {Guid = CustomMonikerGuid, Id = 1};
    /// <summary>Icon für eine nicht-modale Kante (Custom-Moniker).</summary>
    public static ImageMoniker NonModalEdge        => new() {Guid = CustomMonikerGuid, Id = 2};
    /// <summary>Icon für eine GoTo-Kante (Custom-Moniker).</summary>
    public static ImageMoniker GoToEdge            => new() {Guid = CustomMonikerGuid, Id = 3};
    /// <summary>Icon für eine modale Continuation (Custom-Moniker).</summary>
    public static ImageMoniker ModalContinuation   => new() {Guid = CustomMonikerGuid, Id = 4};
    /// <summary>Icon für eine GoTo-Continuation (Custom-Moniker).</summary>
    public static ImageMoniker GoToContinuation    => new() {Guid = CustomMonikerGuid, Id = 5};

    /// <summary>
    /// Ermittelt das zu einem <see cref="ISymbol"/> passende Icon über den
    /// <see cref="SymbolImageVisitor"/>.
    /// </summary>
    /// <param name="symbol">Das Nav-Symbol, dessen Icon gesucht wird.</param>
    /// <returns>Das dem Symbol zugeordnete Icon.</returns>
    public static ImageMoniker FromSymbol(ISymbol symbol) {
        return SymbolImageVisitor.FindImageMoniker(symbol);
    }

}