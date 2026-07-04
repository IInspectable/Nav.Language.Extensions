#region Using Directives

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Der „Wirt" eines Code-Blocks (<c>[ … ]</c>) — er bestimmt, welche Code-Schlüsselwörter dort grammatisch
/// zulässig sind. Die zugehörigen Grammatikregeln stehen im <c>NavParser</c> (jeweils die optionalen
/// <c>code*</c>-Deklarationen des Wirts); die erlaubten Schlüsselwörter je Wirt liefert
/// <see cref="CodeBlockFacts"/>.
/// </summary>
enum CodeBlockHost {

    /// <summary>Datei-Ebene (Kopf der CodeGenerationUnit): <c>namespaceprefix</c>, <c>using</c>.</summary>
    CompilationUnit,

    /// <summary><c>taskref</c>-Deklaration: <c>namespaceprefix</c>, <c>result</c> (<c>notimplemented</c> ist versteckt).</summary>
    TaskRef,

    /// <summary><c>task</c>-Definitions-Kopf: <c>code</c>, <c>base</c>, <c>generateto</c>, <c>params</c>, <c>result</c>.</summary>
    TaskDefinition,

    /// <summary><c>init</c>-Knoten: <c>abstractmethod</c>, <c>params</c>.</summary>
    InitNode,

    /// <summary><c>task</c>-Knoten: <c>donotinject</c>, <c>abstractmethod</c>.</summary>
    TaskNode

}

/// <summary>
/// Einzige Autorität dafür, welche <c>[keyword …]</c>-Code-Deklarationen die Grammatik im jeweiligen
/// <see cref="CodeBlockHost"/> erlaubt. Genutzt vom <c>NavParser</c> (Fehler-Recovery für leere bzw.
/// deplatzierte Klammern samt <c>expected …</c>-Diagnose) <b>und</b> von der Completion (Vorschläge im
/// Schlüsselwort-Slot direkt hinter <c>[</c>) — so teilen beide Seiten dieselbe Grammatik-Wahrheit, statt
/// die Listen getrennt zu pflegen. Die Literale stammen ausschließlich aus <see cref="SyntaxFacts"/>.
/// </summary>
static class CodeBlockFacts {

    /// <summary>
    /// Die im Wirt zulässigen Code-Deklarations-Schlüsselwörter in <b>Grammatik-Reihenfolge</b> — spiegelt
    /// die optionalen <c>code*</c>-Deklarationen der jeweiligen Parse-Regel im <c>NavParser</c>. Enthält auch
    /// versteckte Schlüsselwörter (z.B. <c>notimplemented</c>); für nutzerseitige Ausgaben stattdessen
    /// <see cref="VisibleDeclarationKeywords"/> verwenden.
    /// </summary>
    public static ImmutableArray<string> DeclarationKeywords(CodeBlockHost host) => host switch {
        CodeBlockHost.CompilationUnit => ImmutableArray.Create(SyntaxFacts.NamespaceprefixKeyword, SyntaxFacts.UsingKeyword),
        CodeBlockHost.TaskRef         => ImmutableArray.Create(SyntaxFacts.NamespaceprefixKeyword, SyntaxFacts.NotimplementedKeyword, SyntaxFacts.ResultKeyword),
        CodeBlockHost.TaskDefinition  => ImmutableArray.Create(SyntaxFacts.CodeKeyword,            SyntaxFacts.BaseKeyword,           SyntaxFacts.GeneratetoKeyword, SyntaxFacts.ParamsKeyword, SyntaxFacts.ResultKeyword),
        CodeBlockHost.InitNode        => ImmutableArray.Create(SyntaxFacts.AbstractmethodKeyword,  SyntaxFacts.ParamsKeyword),
        CodeBlockHost.TaskNode        => ImmutableArray.Create(SyntaxFacts.DonotinjectKeyword,     SyntaxFacts.AbstractmethodKeyword),
        _                             => ImmutableArray<string>.Empty
    };

    /// <summary>
    /// Die im Wirt zulässigen, <b>sichtbaren</b> Code-Deklarations-Schlüsselwörter — die versteckten
    /// (<see cref="SyntaxFacts.IsHiddenKeyword"/>, z.B. <c>notimplemented</c>) sind entfernt. Grundlage
    /// aller nutzerseitigen Ausgaben: der Completion-Vorschläge wie der <c>expected …</c>-Diagnose des Parsers.
    /// </summary>
    public static ImmutableArray<string> VisibleDeclarationKeywords(CodeBlockHost host) =>
        DeclarationKeywords(host).Where(keyword => !SyntaxFacts.IsHiddenKeyword(keyword)).ToImmutableArray();

    /// <summary>
    /// Ob eine Code-Deklaration im selben Wirt <b>mehrfach</b> zulässig ist. Die Grammatik erlaubt jede
    /// <c>code*</c>-Deklaration genau einmal (<c>?</c>) — einzige Ausnahme ist <c>using</c> im Datei-Kopf
    /// (<c>codeUsingDeclaration*</c>, wiederholbar). Alle übrigen sind Singletons.
    /// </summary>
    public static bool IsRepeatable(string keyword) => keyword == SyntaxFacts.UsingKeyword;

    /// <summary>
    /// Die im Wirt noch <b>anbietbaren</b>, sichtbaren Code-Deklarations-Schlüsselwörter:
    /// <see cref="VisibleDeclarationKeywords"/> ohne die am selben Wirt bereits vorhandenen Singletons.
    /// Wiederholbare Deklarationen (<c>using</c>, siehe <see cref="IsRepeatable"/>) bleiben stets erhalten.
    /// <paramref name="presentKeywords"/> sind die am Wirt schon deklarierten Code-Schlüsselwörter.
    /// </summary>
    public static ImmutableArray<string> AvailableDeclarationKeywords(CodeBlockHost host, ISet<string> presentKeywords) =>
        VisibleDeclarationKeywords(host)
            .Where(keyword => IsRepeatable(keyword) || !presentKeywords.Contains(keyword))
            .ToImmutableArray();

}