using System;

namespace Liman.Core
{
    public enum MoveType
    {
        /// <summary>Top container of a yard stack goes onto the ship at a dock.</summary>
        Load,

        /// <summary>Top container of a yard stack goes onto another yard stack.</summary>
        Shift,
    }

    /// <summary>
    /// A single crane action. For <see cref="MoveType.Load"/>, <see cref="To"/> is a dock index;
    /// for <see cref="MoveType.Shift"/>, it is a stack index.
    /// </summary>
    public readonly struct Move : IEquatable<Move>
    {
        public MoveType Type { get; }
        public int From { get; }
        public int To { get; }

        private Move(MoveType type, int from, int to)
        {
            Type = type;
            From = from;
            To = to;
        }

        public static Move Load(int stack, int dock) => new Move(MoveType.Load, stack, dock);

        public static Move Shift(int fromStack, int toStack) => new Move(MoveType.Shift, fromStack, toStack);

        public bool Equals(Move other) => Type == other.Type && From == other.From && To == other.To;

        public override bool Equals(object obj) => obj is Move other && Equals(other);

        public override int GetHashCode() => (((int)Type * 397) ^ From) * 397 ^ To;

        public override string ToString() =>
            Type == MoveType.Load ? $"Load(stack {From} -> dock {To})" : $"Shift(stack {From} -> stack {To})";
    }
}
