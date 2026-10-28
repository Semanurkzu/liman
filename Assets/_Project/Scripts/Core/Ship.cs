using System;

namespace Liman.Core
{
    /// <summary>A cargo ship that accepts containers of a single colour and leaves port when full.</summary>
    public sealed class Ship
    {
        public int Color { get; }
        public int Capacity { get; }

        public Ship(int color, int capacity)
        {
            if (color < 0) throw new ArgumentOutOfRangeException(nameof(color));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Color = color;
            Capacity = capacity;
        }

        public override string ToString() => $"Ship(color {Color}, capacity {Capacity})";
    }
}
