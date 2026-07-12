#region Using Directives

using System;
using System.Collections;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Text;

/// <summary>
/// Die Liste der Zeilen eines <see cref="SourceText"/>. Die Zeilen sind aufsteigend geordnet, liegen
/// lückenlos aneinander und decken den gesamten Text ab; es gibt immer mindestens eine Zeile.
/// </summary>
public abstract class SourceTextLineList: IReadOnlyList<SourceTextLine> {

    /// <summary>
    /// Liefert einen (struct-basierten, allokationsfreien) Enumerator über die Zeilen.
    /// </summary>
    public Enumerator GetEnumerator() {
        return new Enumerator(this);
    }

    IEnumerator<SourceTextLine> IEnumerable<SourceTextLine>.GetEnumerator() {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    /// <summary>
    /// Die Anzahl der Zeilen (immer mindestens 1).
    /// </summary>
    public abstract int Count { get; }

    /// <summary>
    /// Liefert die Zeile mit der angegebenen (nullbasierten) Zeilennummer.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> liegt außerhalb von <c>[0, Count)</c>.</exception>
    public abstract SourceTextLine this[int index] { get; }

    /// <summary>
    /// Struct-Enumerator über die Zeilen einer <see cref="SourceTextLineList"/>. <see cref="Current"/>
    /// ist erst nach einem erfolgreichen <see cref="MoveNext"/> und vor dem Ende der Enumeration gültig.
    /// </summary>
    public struct Enumerator: IEnumerator<SourceTextLine>, IEnumerator {

        private readonly SourceTextLineList _lines;
        private          int                _index;

        internal Enumerator(SourceTextLineList lines) {
            _lines = lines;
            _index = -1;
        }

        /// <summary>
        /// Die Zeile an der aktuellen Enumerator-Position.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Die Enumeration hat noch nicht begonnen (vor dem ersten <see cref="MoveNext"/>) oder ist
        /// bereits beendet (nachdem <see cref="MoveNext"/> <c>false</c> geliefert hat).
        /// </exception>
        public SourceTextLine Current {
            get {
                var ndx = _index;
                if (ndx < 0 || ndx >= _lines.Count) {
                    throw new InvalidOperationException("Die Enumeration hat noch nicht begonnen oder ist bereits beendet.");
                }

                return _lines[ndx];
            }
        }

        /// <summary>
        /// Rückt zur nächsten Zeile vor. Liefert <c>false</c>, wenn keine weitere Zeile vorhanden ist.
        /// </summary>
        public bool MoveNext() {
            if (_index < _lines.Count - 1) {
                _index = _index + 1;
                return true;
            }

            // Am Ende den Index bewusst hinter das letzte Element setzen, damit Current danach
            // klar mit InvalidOperationException scheitert, statt still die letzte Zeile zu liefern.
            _index = _lines.Count;
            return false;
        }

        object IEnumerator.Current => Current;

        bool IEnumerator.MoveNext() {
            return MoveNext();
        }

        void IEnumerator.Reset() {
            _index = -1;
        }

        void IDisposable.Dispose() {
        }

        public override bool Equals(object? obj) {
            throw new NotSupportedException();
        }

        public override int GetHashCode() {
            throw new NotSupportedException();
        }

    }

}
