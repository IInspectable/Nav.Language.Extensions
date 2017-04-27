﻿#region Using Directives

using System;

#endregion

namespace Pharmatechnik.Nav.Language.Text {

    public struct TextChange : IEquatable<TextChange> {

        public TextChange(TextExtent extent, string newText) {
            Extent  = extent;
            NewText = newText ?? throw new ArgumentNullException(nameof(newText));
        }

        public static TextChange NewInsert(int position, string newText) {
            return new TextChange(TextExtent.FromBounds(position, position), newText);
        }

        public TextExtent Extent { get; }

        public string NewText { get; }

        public override string ToString() {
            return $"{GetType().Name}: {{ {Extent}, \"{NewText}\" }}";
        }

        #region Equality members

        public bool Equals(TextChange other) {
            return Extent.Equals(other.Extent) && string.Equals(NewText, other.NewText);
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            return obj is TextChange && Equals((TextChange) obj);
        }

        public override int GetHashCode() {
            unchecked {
                return (Extent.GetHashCode() * 397) ^ NewText.GetHashCode();
            }
        }

        public static bool operator ==(TextChange left, TextChange right) {
            return left.Equals(right);
        }

        public static bool operator !=(TextChange left, TextChange right) {
            return !left.Equals(right);
        }

        #endregion        
    }
}