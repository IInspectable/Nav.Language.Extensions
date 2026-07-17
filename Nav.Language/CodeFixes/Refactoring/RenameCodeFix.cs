#region Using Directives

using System;
using System.Collections.Generic;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language.CodeFixes.Refactoring; 

/// <summary>
/// Abstrakte Basis der Umbenennungs-Fixes: benennt ein Nav-Symbol (Knoten, Task-Deklaration, Alias)
/// samt aller seiner Referenzen um. Der Fix mutiert nichts selbst, sondern liefert über
/// <see cref="GetTextChanges"/> das Edit-Set als
/// <see cref="Pharmatechnik.Nav.Language.Text.TextChange"/>-Folge; ein neuer Name kann zuvor mit
/// <see cref="ValidateSymbolName"/> geprüft und mit <see cref="ProvideDefaultName"/> vorbelegt werden.
/// </summary>
public abstract class RenameCodeFix: RefactoringCodeFix {

    /// <summary>
    /// Initialisiert die Basis mit dem <paramref name="context"/> (Position/Auswahl,
    /// <see cref="CodeGenerationUnit"/>, Editor-Einstellungen).
    /// </summary>
    protected RenameCodeFix(CodeFixContext context)
        : base(context) {
    }

    /// <summary>Liefert den vorzuschlagenden Ausgangsnamen für das Umbenennen (i.d.R. der aktuelle Name).</summary>
    public abstract string                  ProvideDefaultName();
    /// <summary>
    /// Prüft den vorgeschlagenen <paramref name="symbolName"/> und liefert eine Fehlermeldung, wenn er
    /// unzulässig ist (z.B. kein gültiger Bezeichner oder bereits vergeben), sonst <c>null</c>.
    /// </summary>
    public abstract string?                 ValidateSymbolName(string? symbolName);
    /// <summary>
    /// Berechnet die Textänderungen, die das Symbol und alle seine Referenzen auf
    /// <paramref name="newName"/> umbenennen. Wirft eine <see cref="ArgumentException"/>, wenn
    /// <paramref name="newName"/> laut <see cref="ValidateSymbolName"/> unzulässig ist.
    /// </summary>
    public abstract IEnumerable<TextChange> GetTextChanges(string? newName);

}

/// <summary>
/// Generische Basis der konkreten Umbenennungs-Fixes für ein Symbol vom Typ <typeparamref name="T"/>.
/// Trennt das umzubenennende <see cref="Symbol"/> vom <see cref="OriginatingSymbol"/> (dem an der
/// Cursor-Position/Auswahl gefundenen Symbol, das die Umbenennung auslöst — etwa eine Referenz auf das
/// Symbol) und leitet daraus <see cref="ApplicableTo"/> ab.
/// </summary>
/// <typeparam name="T">Der Symboltyp, der umbenannt wird.</typeparam>
abstract class RenameCodeFix<T>: RenameCodeFix where T : class, ISymbol {

    protected RenameCodeFix(T symbol, ISymbol originatingSymbol, CodeFixContext context)
        : base(context) {

        Symbol            = symbol            ?? throw new ArgumentNullException(nameof(symbol));
        OriginatingSymbol = originatingSymbol ?? throw new ArgumentNullException(nameof(originatingSymbol));
    }

    /// <summary>
    /// Das an der Position/Auswahl gefundene Symbol, das die Umbenennung ausgelöst hat. Kann vom
    /// tatsächlich umbenannten <see cref="Symbol"/> abweichen (z.B. eine Referenz auf dessen Deklaration);
    /// bestimmt über seine Location den <see cref="ApplicableTo"/>-Bereich.
    /// </summary>
    protected ISymbol OriginatingSymbol { get; }

    /// <summary>Der Bereich, an dem der Fix angeboten wird — die Location des <see cref="OriginatingSymbol"/>.</summary>
    public override TextExtent? ApplicableTo => OriginatingSymbol.Location.Extent;
    /// <summary>Immer <see cref="CodeFixPrio.Low"/> — Umbenennungen sind eine niedrigpriore Umgestaltung.</summary>
    public override CodeFixPrio Prio         => CodeFixPrio.Low;

    /// <summary>Das umzubenennende Symbol.</summary>
    public T Symbol { get; }

    /// <summary>Der aktuelle Name des <see cref="Symbol"/> als Ausgangsvorschlag.</summary>
    public override string ProvideDefaultName() {
        return Symbol.Name;
    }

}