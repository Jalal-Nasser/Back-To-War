using System;

namespace Back2War.SimCore.Core
{
    public readonly struct SimTick : IEquatable<SimTick>, IComparable<SimTick>
    {
        public readonly uint Value;

        public SimTick(uint value)
        {
            Value = value;
        }

        public int CompareTo(SimTick other)
        {
            return Value.CompareTo(other.Value);
        }

        public bool Equals(SimTick other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is SimTick other && Equals(other);
        }

        public override int GetHashCode()
        {
            return unchecked((int)Value);
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static SimTick operator +(SimTick left, uint right)
        {
            return new SimTick(left.Value + right);
        }

        public static SimTick operator -(SimTick left, uint right)
        {
            return new SimTick(left.Value - right);
        }

        public static long operator -(SimTick left, SimTick right)
        {
            return (long)left.Value - right.Value;
        }

        public static bool operator ==(SimTick left, SimTick right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SimTick left, SimTick right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(SimTick left, SimTick right)
        {
            return left.Value < right.Value;
        }

        public static bool operator <=(SimTick left, SimTick right)
        {
            return left.Value <= right.Value;
        }

        public static bool operator >(SimTick left, SimTick right)
        {
            return left.Value > right.Value;
        }

        public static bool operator >=(SimTick left, SimTick right)
        {
            return left.Value >= right.Value;
        }
    }
}
