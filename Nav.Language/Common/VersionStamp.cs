using System;

namespace Pharmatechnik.Nav.Language;

[Serializable]
public struct VersionStamp: IEquatable<VersionStamp> {

    readonly DateTime _utcTimeStamp;
    readonly int      _increment;

    public VersionStamp(DateTime utcTimeStamp): this(utcTimeStamp, 0) {

    }

    VersionStamp(DateTime utcTimeStamp, int increment) {
        _utcTimeStamp = utcTimeStamp;
        _increment    = increment;

    }

    public static VersionStamp Create() {
        return new VersionStamp(DateTime.UtcNow);
    }

    public static VersionStamp Create(DateTime utcTimeStamp) {
        return new VersionStamp(utcTimeStamp);
    }

    public VersionStamp CreateNewer() {

        var utcTimeStamp = DateTime.UtcNow;
        var increment    = _utcTimeStamp == utcTimeStamp ? _increment + 1 : 0;
        return new VersionStamp(utcTimeStamp, increment);
    }

    public VersionStamp GetNewer(VersionStamp other) {

        return GetNewer(this, other);
    }

    public static VersionStamp GetNewer(VersionStamp a, VersionStamp b) {
        if (b._utcTimeStamp > a._utcTimeStamp) {
            return b;
        }

        if (b._utcTimeStamp == a._utcTimeStamp) {
            return b._increment > a._increment ? b : a;
        }

        return a;
    }

    public bool Equals(VersionStamp other) {
        return _utcTimeStamp.Equals(other._utcTimeStamp) &&
               _increment == other._increment;
    }

    public override bool Equals(object? obj) {
        if (ReferenceEquals(null, obj)) {
            return false;
        }

        return obj is VersionStamp stamp && Equals(stamp);
    }

    public override int GetHashCode() {
        unchecked {
            return (_utcTimeStamp.GetHashCode() * 397) ^ _increment;
        }
    }

    public static bool operator ==(VersionStamp left, VersionStamp right) {
        return left.Equals(right);
    }

    public static bool operator !=(VersionStamp left, VersionStamp right) {
        return !left.Equals(right);
    }

    public static bool operator <(VersionStamp left, VersionStamp right) {
        if (left == right) {
            return false;
        }

        return GetNewer(left, right) == right;

    }

    public static bool operator >(VersionStamp left, VersionStamp right) {
        if (left == right) {
            return false;
        }

        return GetNewer(left, right) == left;
    }

    public static bool operator <=(VersionStamp left, VersionStamp right) {
        if (left == right) {
            return true;
        }

        return left < right;
    }

    public static bool operator >=(VersionStamp left, VersionStamp right) {
        if (left == right) {
            return true;
        }

        return left > right;
    }

}