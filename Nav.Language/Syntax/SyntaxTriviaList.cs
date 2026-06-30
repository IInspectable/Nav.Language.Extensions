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

    public SyntaxTrivia this[int index] => _all[_start + index];

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

        public SyntaxTrivia Current => _list[_index];

        public bool MoveNext() => ++_index < _list._length;
    }

}
