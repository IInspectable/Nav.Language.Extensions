#region Using Directives

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

#endregion

namespace Pharmatechnik.Nav.Language;

/// <summary>
/// Eine leichtgewichtige Sicht auf einen zusammenhängenden Bereich der Trivia eines <see cref="SyntaxTree"/>.
/// Statt je Token ein eigenes <see cref="ImmutableArray{T}"/> zu materialisieren, teilen sich alle Token
/// <b>ein</b> Trivia-Array; ein Token merkt sich nur Start und Länge seines Leading- bzw. Trailing-Bereichs
/// darin. Die Oberfläche (<see cref="Length"/>, <see cref="IsEmpty"/>, Indexer, <c>foreach</c>) bildet die
/// eines <see cref="ImmutableArray{T}"/> nach, damit Aufrufstellen unverändert bleiben; <c>foreach</c> läuft
/// über den Struct-<see cref="Enumerator"/> allokationsfrei.
/// </summary>
public readonly struct SyntaxTriviaList: IReadOnlyList<SyntaxTrivia> {

    readonly ImmutableArray<SyntaxTrivia> _all;
    readonly int                          _start;
    readonly int                          _length;

    /// <summary>
    /// Erzeugt eine Sicht auf <paramref name="length"/> Trivia-Stücke ab Index <paramref name="start"/> im
    /// geteilten Array <paramref name="all"/> (das gesamte Trivia der Datei in Quelltext-Reihenfolge hält).
    /// </summary>
    public SyntaxTriviaList(ImmutableArray<SyntaxTrivia> all, int start, int length) {
        _all    = all;
        _start  = start;
        _length = length;
    }

    /// <summary>Die leere Trivia-Sicht (≙ <c>default</c>).</summary>
    public static readonly SyntaxTriviaList Empty = default;

    /// <summary>Anzahl der Trivia-Stücke (Member-Name wie bei <see cref="ImmutableArray{T}"/>).</summary>
    public int Length => _length;

    /// <summary><c>true</c>, wenn die Sicht kein Trivia-Stück enthält.</summary>
    public bool IsEmpty => _length == 0;

    /// <summary>Das Trivia-Stück am Index <paramref name="index"/> innerhalb dieser Sicht.</summary>
    public SyntaxTrivia this[int index] => _all[_start + index];

    /// <summary>Der allokationsfreie Struct-<see cref="Enumerator"/> — von <c>foreach</c> bevorzugt gebunden.</summary>
    public Enumerator GetEnumerator() => new(this);

    // IReadOnlyList/IEnumerable: nur für LINQ & Co. (boxt den Enumerator) — die heißen Pfade nutzen den
    // Struct-Enumerator oben.
    int IReadOnlyCollection<SyntaxTrivia>.Count => _length;

    IEnumerator<SyntaxTrivia> IEnumerable<SyntaxTrivia>.GetEnumerator() {
        for (var i = 0; i < _length; i++) {
            yield return _all[_start + i];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<SyntaxTrivia>) this).GetEnumerator();

    /// <summary>Allokationsfreier Struct-Enumerator für <c>foreach</c>.</summary>
    public struct Enumerator {

        readonly SyntaxTriviaList _list;
        int                       _index;

        internal Enumerator(SyntaxTriviaList list) {
            _list  = list;
            _index = -1;
        }

        /// <summary>Das Trivia-Stück an der aktuellen Position.</summary>
        public SyntaxTrivia Current => _list[_index];

        /// <summary>Rückt zur nächsten Position vor; <c>false</c> am Ende der Sicht.</summary>
        public bool MoveNext() => ++_index < _list._length;
    }

}
