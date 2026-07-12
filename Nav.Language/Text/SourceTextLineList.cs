#region Using Directives

using System;
using System.Collections;
using System.Collections.Generic;

#endregion

namespace Pharmatechnik.Nav.Language.Text;

public abstract class SourceTextLineList: IReadOnlyList<SourceTextLine> {

    public Enumerator GetEnumerator() {
        return new Enumerator(this);
    }

    IEnumerator<SourceTextLine> IEnumerable<SourceTextLine>.GetEnumerator() {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    public abstract int Count { get; }
    public abstract SourceTextLine this[int index] { get; }

    public struct Enumerator: IEnumerator<SourceTextLine>, IEnumerator {

        private readonly SourceTextLineList _lines;
        private          int                _index;

        internal Enumerator(SourceTextLineList lines) {
            _lines = lines;
            _index = -1;
        }

        public SourceTextLine Current {
            get {
                var ndx = _index;
                if (ndx < 0 || ndx >= _lines.Count) {
                    throw new InvalidOperationException("Die Enumeration hat noch nicht begonnen oder ist bereits beendet.");
                }

                return _lines[ndx];
            }
        }

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