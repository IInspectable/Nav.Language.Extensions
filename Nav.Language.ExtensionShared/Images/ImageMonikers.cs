#region Using Directives

using System;

using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;

using Pharmatechnik.Nav.Language.CodeFixes;

#endregion

namespace Pharmatechnik.Nav.Language.Extension.Images; 

public static partial class ImageMonikers {

    static readonly Guid CustomMonikerGuid = new("{11e9628b-b9e6-45d6-ae8d-b4440be46fa6}");

    public static ImageMoniker ProjectNode  => KnownMonikers.CSProjectNode;
    public static ImageMoniker FolderClosed => KnownMonikers.FolderClosed;
    public static ImageMoniker ParentFolder => KnownMonikers.MoveUp;
    public static ImageMoniker NavFile      => KnownMonikers.ActivityDiagram; //KnownMonikers.ClassFile;
    public static ImageMoniker File         => KnownMonikers.TextFile;
    public static ImageMoniker Information  => KnownMonikers.StatusInformation;

    // CodeFixImpact

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

    public static ImageMoniker WaitingForAnalysis => KnownMonikers.Loading;
    public static ImageMoniker AnalysisOK         => KnownMonikers.StatusOK;
    public static ImageMoniker AnalysisWarning    => KnownMonikers.StatusWarning;
    public static ImageMoniker AnalysisError      => KnownMonikers.StatusError;

    // GoTo

    /// <summary>
    /// Nav file --> C# file
    /// </summary>
    public static ImageMoniker GoToDeclaration => KnownMonikers.GoToDefinition;

    /// <summary>
    /// C# file --> Nav file
    /// </summary>
    public static ImageMoniker GoToDefinition => KnownMonikers.GoToDeclaration;

    public static ImageMoniker Include             => NavFile;
    public static ImageMoniker GoToNodeDeclaration => KnownMonikers.GoToReference;
    public static ImageMoniker GoToMethodPublic    => KnownMonikers.MethodPublic;
    public static ImageMoniker GoToClassPublic     => KnownMonikers.ClassPublic;
    public static ImageMoniker GoToInterfacePublic => KnownMonikers.InterfacePublic;
    public static ImageMoniker CSharpFile          => KnownMonikers.CSFileNode;

    // Keyword
    public static ImageMoniker Keyword => KnownMonikers.IntellisenseKeyword;

    // Actions

    public static ImageMoniker AddEdge              => KnownMonikers.AddAssociation;
    public static ImageMoniker RenameNode           => KnownMonikers.Rename;
    public static ImageMoniker InsertNode           => KnownMonikers.InsertClause;
    public static ImageMoniker DeleteQuotationMarks => KnownMonikers.PendingDeleteNode;
    public static ImageMoniker RemoveUnusedSymbol   => KnownMonikers.PendingDeleteNode;
    public static ImageMoniker AddSemicolon         => KnownMonikers.PendingAddNode;
    public static ImageMoniker SetLanguageVersion   => KnownMonikers.IntellisenseKeyword;
    public static ImageMoniker MoveDirectiveToTop   => KnownMonikers.MoveUp;

    // Symbols

    public static ImageMoniker TaskDeclaration     => KnownMonikers.Interface; //KnownMonikers.WorkflowInterop;
    public static ImageMoniker InitConnectionPoint => KnownMonikers.InputPin;
    public static ImageMoniker ExitConnectionPoint => KnownMonikers.OutputPin;
    public static ImageMoniker EndConnectionPoint  => KnownMonikers.ActivityFinalNode;
    public static ImageMoniker TaskDefinition      => KnownMonikers.Task; //KnownMonikers.CSWorkflow;
    public static ImageMoniker InitNode            => KnownMonikers.InputPin;
    public static ImageMoniker ExitNode            => KnownMonikers.OutputPin;
    public static ImageMoniker EndNode             => KnownMonikers.ActivityFinalNode;
    public static ImageMoniker TaskNode            => KnownMonikers.Task; //KnownMonikers.CSWorkflow;
    public static ImageMoniker ChoiceNode          => KnownMonikers.DecisionNode;
    public static ImageMoniker ViewNode            => KnownMonikers.WindowsForm;
    public static ImageMoniker DialogNode          => KnownMonikers.Dialog;
    public static ImageMoniker SignalTrigger       => KnownMonikers.EventTrigger;
    public static ImageMoniker Edge                => KnownMonikers.AssociationRelationship;
    public static ImageMoniker ModalEdge           => new() {Guid = CustomMonikerGuid, Id = 1};
    public static ImageMoniker NonModalEdge        => new() {Guid = CustomMonikerGuid, Id = 2};
    public static ImageMoniker GoToEdge            => new() {Guid = CustomMonikerGuid, Id = 3};

    public static ImageMoniker FromSymbol(ISymbol symbol) {
        return SymbolImageVisitor.FindImageMoniker(symbol);
    }

}