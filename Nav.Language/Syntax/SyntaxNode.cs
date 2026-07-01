#region Using Directives

using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

using JetBrains.Annotations;

using Pharmatechnik.Nav.Language.Text;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Der abstrakte Basistyp aller Knoten des Nav-Syntaxbaums. Jeder Knoten deckt einen zusammenhängenden
/// <see cref="Extent"/> im Quelltext ab und ist Teil eines unveränderlichen Baums: Er kennt seinen
/// <see cref="Parent"/>, seine <see cref="ChildNodes"/> und die ihm direkt zugeordneten
/// <see cref="ChildTokens"/>. Der Baum wird in zwei Phasen aufgebaut — erst die Knoten samt Kindern, dann
/// das einmalige <see cref="FinalConstruct"/>, das <see cref="SyntaxTree"/> und <see cref="Parent"/>
/// nachträgt und den Knoten einfriert. Danach sind alle Zugriffe lesend.
/// </summary>
[Serializable]
[DebuggerDisplay("{" + nameof(ToDebuggerDisplayString) + "(), nq}")]
public abstract partial class SyntaxNode: IExtent {

    List<SyntaxNode> _childNodes;
    SyntaxTree       _syntaxTree;
    SyntaxNode       _parent;

    /// <summary>
    /// Erzeugt einen Knoten mit seinem Quelltext-<paramref name="extent"/>. Der Knoten ist danach noch im
    /// Aufbau-Modus (kein <see cref="SyntaxTree"/>, kein <see cref="Parent"/>) — beides setzt erst
    /// <see cref="FinalConstruct"/>.
    /// </summary>
    internal SyntaxNode(TextExtent extent) {
        Extent = extent;
    }

    /// <summary>
    /// Schließt den Aufbau des Knotens ab: trägt <paramref name="syntaxTree"/> und <paramref name="parent"/>
    /// nach und ruft sich rekursiv für alle Kindknoten auf. Danach ist der Knoten eingefroren und nur noch
    /// lesend benutzbar. Darf je Knoten genau einmal aufgerufen werden.
    /// </summary>
    internal void FinalConstruct(SyntaxTree syntaxTree, SyntaxNode parent) {

        EnsureConstructionMode();

        _syntaxTree = syntaxTree;
        _parent     = parent;

        if (_childNodes == null) {
            return;
        }

        foreach (var child in _childNodes) {
            child.FinalConstruct(syntaxTree, this);
        }
    }

    /// <summary>Die Startposition dieses Knotens im Quelltext (inklusiv).</summary>
    public int Start  => Extent.Start;

    /// <summary>Die Endposition dieses Knotens im Quelltext (exklusiv).</summary>
    public int End    => Extent.End;

    /// <summary>Die Länge dieses Knotens in Zeichen.</summary>
    public int Length => Extent.Length;

    /// <summary>Der Quelltext-Ausschnitt, den dieser Knoten abdeckt (ohne umgebende Trivia; ≙ Roslyn <c>Span</c>).</summary>
    public TextExtent Extent { get; }

    /// <summary>
    /// Der Quelltext-Ausschnitt dieses Knotens samt umgebender Trivia (≙ Roslyn <c>FullSpan</c>) — das
    /// Gegenstück zum trivia-freien <see cref="Extent"/> (≙ Roslyn <c>Span</c>). Reicht von der Leading-
    /// bis zur Trailing-Trivia, wobei auch Kommentare als Trivia zählen. Wer Kommentare stattdessen als
    /// Grenze behandeln will, nutzt <see cref="GetFullExtent"/> mit <c>onlyWhiteSpace: true</c>.
    /// </summary>
    public TextExtent FullExtent => GetFullExtent();

    /// <summary>
    /// Der übergeordnete Knoten, oder <c>null</c> für die Wurzel des Baums. Erst nach
    /// <see cref="FinalConstruct"/> verfügbar.
    /// </summary>
    [CanBeNull]
    public SyntaxNode Parent {
        get {
            EnsureConstructed();
            return _parent;
        }
    }

    /// <summary>Die <see cref="Location"/> (Datei + Zeilen-/Spaltenbereich) dieses Knotens.</summary>
    public Location GetLocation() {
        EnsureConstructed();
        return SyntaxTree.SourceText.GetLocation(Extent);
    }

    /// <summary>
    /// Die diesem Knoten <b>direkt</b> zugeordneten Token (die der Parser hier konsumiert und angehängt hat) —
    /// in Quelltext-Reihenfolge. Token tiefer liegender Kindknoten gehören nicht dazu; Trivia hängt am
    /// Wurzelknoten und erscheint hier nur für die Wurzel. Knoten, deren Token nicht im flachen
    /// <see cref="SyntaxTree.Tokens"/>-Strom stehen (etwa strukturierte Trivia), überschreiben diese Methode.
    /// </summary>
    [NotNull]
    public virtual IEnumerable<SyntaxToken> ChildTokens() {
        return SyntaxTree.Tokens[Extent].Where(token => token.Parent == this);
    }

    static readonly IReadOnlyList<SyntaxNode> EmptyNodeList = new List<SyntaxNode>();

    /// <summary>Die unmittelbaren Kindknoten dieses Knotens (eine Ebene tief), in Quelltext-Reihenfolge.</summary>
    [NotNull]
    public IReadOnlyList<SyntaxNode> ChildNodes() {
        EnsureConstructed();
        return _childNodes ?? EmptyNodeList;
    }

    /// <summary>
    /// Alle Nachfahren dieses Knotens (rekursiv, ohne den Knoten selbst) in Tiefendurchlauf-Reihenfolge.
    /// </summary>
    [NotNull]
    public IEnumerable<SyntaxNode> DescendantNodes() {
        return DescendantNodes<SyntaxNode>();
    }

    /// <summary>
    /// Dieser Knoten und alle seine Nachfahren (rekursiv) in Tiefendurchlauf-Reihenfolge.
    /// </summary>
    [NotNull]
    public IEnumerable<SyntaxNode> DescendantNodesAndSelf() {
        return DescendantNodesAndSelf<SyntaxNode>();
    }

    /// <summary>
    /// Alle Nachfahren vom Typ <typeparamref name="T"/> (rekursiv, ohne den Knoten selbst) in
    /// Tiefendurchlauf-Reihenfolge.
    /// </summary>
    [NotNull]
    public IEnumerable<T> DescendantNodes<T>() where T : SyntaxNode {
        return DescendantNodesAndSelfImpl<T>(includeSelf: false);
    }

    /// <summary>
    /// Dieser Knoten (sofern vom Typ <typeparamref name="T"/>) und alle seine Nachfahren vom Typ
    /// <typeparamref name="T"/> (rekursiv) in Tiefendurchlauf-Reihenfolge.
    /// </summary>
    [NotNull]
    public IEnumerable<T> DescendantNodesAndSelf<T>() where T : SyntaxNode {
        return DescendantNodesAndSelfImpl<T>(includeSelf: true);
    }

    /// <summary>
    /// Gemeinsame Implementierung der Nachfahren-Durchläufe: liefert optional zuerst den Knoten selbst
    /// (sofern vom Typ <typeparamref name="T"/>) und steigt dann in die Kindknoten ab. Kann über
    /// <see cref="PromiseNoDescendantNodeOfSameType"/> den Abstieg vorzeitig abbrechen.
    /// </summary>
    [NotNull]
    IEnumerable<T> DescendantNodesAndSelfImpl<T>(bool includeSelf) where T : SyntaxNode {
        EnsureConstructed();
        if (includeSelf && this is T) {
            yield return (T) this;

            if (typeof(T) == GetType() && PromiseNoDescendantNodeOfSameType) {
                yield break;
            }
        }

        foreach (var node in ChildNodes().SelectMany(child => child.DescendantNodesAndSelf<T>())) {
            yield return node;
        }
    }

    /// <summary>
    /// Für Knoten, die sehr weit "oben" liegen, kann die Implementierung von DescendantNodes&lt;T&gt;
    /// massiv beschleunigt werden, wenn sichergestellt werden kann, dass ein Knoten keine untergeordneten
    /// Knoten vom selben Typ haben kann, und deshalb die Suche in den Kindknoten vorzeitig abgebrochen
    /// werden kann.
    /// Eigentlich müsste diese Eigenschaft systematisch überschrieben werden. Bisweilen werden hiermit nur
    /// die Hotspots optimiert.
    /// </summary>
    private protected virtual bool PromiseNoDescendantNodeOfSameType => false;

    /// <summary>
    /// Die Vorfahren dieses Knotens — vom <see cref="Parent"/> aufwärts bis zur Wurzel.
    /// </summary>
    public IEnumerable<SyntaxNode> Ancestors() {
        return Parent?.AncestorsAndSelf() ?? Enumerable.Empty<SyntaxNode>();
    }

    /// <summary>
    /// Dieser Knoten und seine Vorfahren — von ihm selbst aufwärts bis zur Wurzel.
    /// </summary>
    public IEnumerable<SyntaxNode> AncestorsAndSelf() {
        for (var node = this; node != null; node = node.Parent) {
            yield return node;
        }
    }

    /// <summary>
    /// Das Token, zu dem die angegebene <paramref name="position"/> gehört — nach Roslyn-Vorbild: liegt die
    /// Position auf dem Extent eines Tokens, ist es dieses; liegt sie in angehängter Trivia
    /// (Whitespace/Zeilenende/Kommentar), ist es das <b>signifikante Token, an dem die Trivia hängt</b>. Im
    /// gültigen Bereich wird also nie eine Trivia-Position als „leer" zurückgegeben; außerhalb (oder ohne
    /// tragendes Token) ist das Ergebnis <see cref="SyntaxToken.Missing"/>.
    /// </summary>
    /// <remarks>
    /// Wer das Token <b>exakt</b> an der Position braucht (und an einer Trivia-Position bewusst nichts
    /// erhalten will), nutzt <see cref="SyntaxTokenList.FindAtPosition"/>. Ein <c>findInsideTrivia</c>-Pendant
    /// (Abstieg in strukturierte <see cref="SyntaxTokenType.DirectiveTrivia"/> und deren lokale Token) gibt es
    /// nicht — die Token einer Direktive erreicht man über <see cref="SyntaxTrivia.GetStructure"/>.
    /// </remarks>
    public SyntaxToken FindToken(int position) {
        return SyntaxTree.Tokens.FindOwningToken(position);
    }

    /// <summary>
    /// Der Knoten, dem das Token an der angegebenen <paramref name="position"/> zugeordnet ist — sofern
    /// dieses Token innerhalb des <see cref="Extent"/> dieses Knotens liegt; sonst <c>null</c>.
    /// </summary>
    public SyntaxNode FindNode(int position) {
        var token = SyntaxTree.Tokens.FindAtPosition(position);
        if (token.IsMissing) {
            return null;
        }

        if (Extent.Contains(token.Extent)) {
            return token.Parent;
        }

        return null;
    }

    /// <summary>
    /// Der <see cref="Extent"/> dieses Knotens samt umgebender Trivia — von der Leading- bis zur
    /// Trailing-Trivia (siehe <see cref="GetLeadingTriviaExtent"/> / <see cref="GetTrailingTriviaExtent"/>).
    /// Das parameterlose Standard-Gegenstück (Kommentare zählen als Trivia) ist die Property
    /// <see cref="FullExtent"/> (≙ Roslyn <c>FullSpan</c>).
    /// </summary>
    /// <param name="onlyWhiteSpace">
    /// Steuert, was als zugehörige Trivia gilt — und damit, wie weit der Ausschnitt über den
    /// <see cref="Extent"/> hinausreicht:
    /// <list type="bullet">
    /// <item><description><c>false</c> (Standard): Kommentare zählen als Trivia und werden mit
    /// eingeschlossen. Der Ausschnitt umfasst also auch unmittelbar vor- bzw. nachstehende
    /// Kommentar(zeilen) — gedacht, um einen Knoten <i>samt seiner zugehörigen Kommentare</i> zu erfassen
    /// (etwa beim Ausschneiden/Verschieben eines Knotens).</description></item>
    /// <item><description><c>true</c>: nur Whitespace (Leerzeichen, Tabs, Zeilenenden) zählt als Trivia;
    /// Kommentare <i>begrenzen</i> den Ausschnitt. Leading reicht dann nur bis hinter den letzten
    /// Kommentar zurück (vorangehende Kommentare bleiben außen vor), und ein Kommentar in der
    /// Trailing-Zeile beendet den Ausschnitt. Gedacht, wenn nur die reine Leerraum-/Einrückungs-Umgebung
    /// des Knotens interessiert, nicht seine Kommentare.</description></item>
    /// </list>
    /// </param>
    public TextExtent GetFullExtent(bool onlyWhiteSpace = false) {
        return TextExtent.FromBounds(GetLeadingTriviaExtent(onlyWhiteSpace).Start, GetTrailingTriviaExtent(onlyWhiteSpace).End);
    }

    /// <summary>
    /// Die Leading-Trivia dieses Knotens — abgeleitet aus dem <b>ersten</b> signifikanten Token (echtes
    /// Roslyn-Modell). Das ist das Token an <see cref="Start"/>; bei zusammengesetzten Knoten gehört es
    /// einem Nachfahren, liegt aber über die Position eindeutig fest. Hat der Knoten keinen echten Extent,
    /// ist sie leer.
    /// </summary>
    public SyntaxTriviaList GetLeadingTrivia() {
        if (Extent.IsMissing) {
            return SyntaxTriviaList.Empty;
        }

        var first = SyntaxTree.Tokens.FindAtPosition(Start);
        return first.IsMissing ? SyntaxTriviaList.Empty : first.LeadingTrivia;
    }

    /// <summary>
    /// Die Trailing-Trivia dieses Knotens — abgeleitet aus dem <b>letzten</b> signifikanten Token (echtes
    /// Roslyn-Modell). Das ist das Token, das an <see cref="End"/> endet; bei zusammengesetzten Knoten
    /// gehört es einem Nachfahren, liegt aber über die Position eindeutig fest. Hat der Knoten keinen echten
    /// Extent, ist sie leer.
    /// </summary>
    public SyntaxTriviaList GetTrailingTrivia() {
        if (Extent.IsMissing) {
            return SyntaxTriviaList.Empty;
        }

        var last = SyntaxTree.Tokens.FindAtPosition(End - 1);
        return last.IsMissing ? SyntaxTriviaList.Empty : last.TrailingTrivia;
    }

    /// <summary>
    /// Der Ausschnitt der Leading-Trivia vor diesem Knoten: die Einrückung seiner eigenen Zeile sowie alle
    /// unmittelbar darüber liegenden Zeilen, die ausschließlich aus Trivia bestehen. Steht vor dem Knoten in
    /// seiner Zeile bereits etwas Nicht-Trivia, ist der Ausschnitt leer (nullbreit am Knotenanfang).
    /// </summary>
    /// <param name="onlyWhiteSpace">Wenn <c>true</c>, zählt nur Whitespace als Trivia; Kommentare begrenzen
    /// den Ausschnitt dann.</param>
    public TextExtent GetLeadingTriviaExtent(bool onlyWhiteSpace = false) {

        // Abgeleitet aus der am ersten signifikanten Token angehängten Leading-Trivia (Roslyn-Modell).
        var leadingTrivia = GetLeadingTrivia();
        if (leadingTrivia.IsEmpty) {
            return TextExtent.FromBounds(Start, Start);
        }

        int start;
        if (!onlyWhiteSpace) {
            // Kommentare zählen als Trivia: der gesamte angehängte Vorlauf gehört dazu.
            start = leadingTrivia[0].Start;
        } else {
            // Nur Whitespace zählt: der letzte Kommentar begrenzt den Ausschnitt. Alles bis
            // einschließlich des Zeilenendes nach dem letzten Kommentar gehört zu einer
            // kommentar-behafteten Zeile und zählt nicht mehr; erst die anschließende reine
            // Whitespace-Strecke (Leerzeilen + Einrückung der Knotenzeile) ist Trivia.
            var lastComment = -1;
            for (var i = 0; i < leadingTrivia.Length; i++) {
                if (IsCommentTrivia(leadingTrivia[i].Type)) {
                    lastComment = i;
                }
            }

            if (lastComment < 0) {
                start = leadingTrivia[0].Start;
            } else {
                var newLineAfterComment = -1;
                for (var i = lastComment + 1; i < leadingTrivia.Length; i++) {
                    if (leadingTrivia[i].Type == SyntaxTokenType.NewLine) {
                        newLineAfterComment = i;
                        break;
                    }
                }

                if (newLineAfterComment < 0) {
                    // Der letzte Kommentar steht in der Knotenzeile selbst → kein reiner Whitespace-Vorlauf.
                    return TextExtent.FromBounds(Start, Start);
                }

                start = leadingTrivia[newLineAfterComment].End;
            }
        }

        // Zeilen-Guard (Parität zur zeilenbasierten Vorgänger-Heuristik): der Ausschnitt darf nur dann vor
        // den Anfang der Knotenzeile zurückreichen, wenn er an einer Zeilengrenze beginnt. Beginnt er mitten
        // in einer Zeile — etwa weil ein übersprungenes Zeichen oder eine Direktive vorausgeht (solche Trenner
        // tragen keine angehängte Trivia) —, besteht der Zeilen-Präfix nicht ausschließlich aus Trivia und der
        // Ausschnitt ist leer.
        var lineStart = SyntaxTree.SourceText.GetTextLineAtPosition(Start).Extent.Start;
        if (start > lineStart) {
            return TextExtent.FromBounds(Start, Start);
        }

        return TextExtent.FromBounds(start, Start);
    }

    /// <summary>
    /// Der Ausschnitt der Trailing-Trivia nach diesem Knoten: vom Knotenende bis zum nächsten Token,
    /// respektive bis zum Ende der Zeile (das Zeilenende eingeschlossen), falls in dieser Zeile kein
    /// weiteres Token mehr folgt.
    /// </summary>
    /// <param name="onlyWhiteSpace">Wenn <c>true</c>, zählt nur Whitespace als Trivia; ein Kommentar in der
    /// Zeile begrenzt den Ausschnitt dann.</param>
    public TextExtent GetTrailingTriviaExtent(bool onlyWhiteSpace = false) {

        // Abgeleitet aus der am letzten signifikanten Token angehängten Trailing-Trivia (Roslyn-Modell):
        // diese reicht ohnehin höchstens bis einschließlich des ersten Zeilenendes bzw. bis zum nächsten
        // Token. Im onlyWhiteSpace-Modus begrenzt der erste Kommentar den Ausschnitt.
        var trailingTrivia = GetTrailingTrivia();

        var end = End;
        foreach (var trivia in trailingTrivia) {
            if (onlyWhiteSpace && IsCommentTrivia(trivia.Type)) {
                break;
            }

            end = trivia.End;
        }

        return TextExtent.FromBounds(End, end);
    }

    /// <summary>Ob der Trivia-Typ ein Kommentar ist (ein- oder mehrzeilig) — für die <c>onlyWhiteSpace</c>-Grenze.</summary>
    static bool IsCommentTrivia(SyntaxTokenType type) {
        return type == SyntaxTokenType.SingleLineComment || type == SyntaxTokenType.MultiLineComment;
    }

    /// <summary>Der <see cref="SyntaxTree"/>, zu dem dieser Knoten gehört. Erst nach <see cref="FinalConstruct"/> verfügbar.</summary>
    [NotNull]
    public SyntaxTree SyntaxTree {
        get {
            EnsureConstructed();
            return _syntaxTree;
        }
    }

    /// <summary>
    /// Fügt — nur während des Aufbaus — einen Kindknoten an. <c>null</c> wird ignoriert. Nach
    /// <see cref="FinalConstruct"/> nicht mehr erlaubt.
    /// </summary>
    protected void AddChildNode(SyntaxNode syntaxNode) {
        EnsureConstructionMode();
        EnsureChildNodes();
        if (syntaxNode != null) {
            _childNodes.Add(syntaxNode);
        }
    }

    /// <summary>
    /// Fügt — nur während des Aufbaus — mehrere Kindknoten an (siehe <see cref="AddChildNode"/>).
    /// </summary>
    protected void AddChildNodes(IEnumerable<SyntaxNode> syntaxNodes) {
        EnsureConstructionMode();
        EnsureChildNodes();
        foreach (var node in syntaxNodes) {
            AddChildNode(node);
        }
    }

    /// <summary>Wirft, wenn der Knoten noch im Aufbau ist (kein <see cref="SyntaxTree"/> gesetzt).</summary>
    void EnsureConstructed() {
        if (_syntaxTree == null) {
            throw new InvalidOperationException();
        }
    }

    /// <summary>Legt die Kindknoten-Liste bei Bedarf an (nur im Aufbau-Modus).</summary>
    void EnsureChildNodes() {
        if (_childNodes == null) {
            EnsureConstructionMode();
            _childNodes = new List<SyntaxNode>();
        }
    }

    /// <summary>Wirft, wenn der Knoten bereits eingefroren ist (<see cref="FinalConstruct"/> gelaufen).</summary>
    void EnsureConstructionMode() {
        if (_syntaxTree != null) {
            throw new InvalidOperationException();
        }
    }

    /// <summary>Der Quelltext dieses Knotens (sein <see cref="Extent"/>, ohne umgebende Trivia).</summary>
    public override string ToString() {
        return SyntaxTree.SourceText.Substring(Start, Length);
    }

    /// <summary>Kompakte Debug-Darstellung: Ausschnitt und konkreter Knotentyp.</summary>
    public string ToDebuggerDisplayString() {
        return $"{Extent} {GetType().Name}";
    }

}
