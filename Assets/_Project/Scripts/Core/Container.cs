namespace Liman.Core
{
    /// <summary>
    /// A container is stored as a single int: the low byte is its colour, higher bits are special-type flags.
    /// Keeping it a plain int keeps board states cheap to clone, which matters for the solver.
    /// </summary>
    public static class Container
    {
        /// <summary>Can't be stacked on another container: only onto an empty stack or a ship.</summary>
        public const int Heavy = 1 << 8;

        /// <summary>Can't be moved at all until a ship of its colour is docked.</summary>
        public const int Locked = 1 << 9;

        private const int ColorMask = 0xFF;

        public static int Make(int color, int flags = 0) => (color & ColorMask) | flags;

        public static int ColorOf(int container) => container & ColorMask;

        public static bool IsHeavy(int container) => (container & Heavy) != 0;

        public static bool IsLocked(int container) => (container & Locked) != 0;
    }
}
