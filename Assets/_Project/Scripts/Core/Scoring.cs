using System;

namespace Liman.Core
{
    public static class Scoring
    {
        /// <summary>Three stars for matching the optimal solution, two for being close, one otherwise.</summary>
        public static int Stars(int moves, int parMoves)
        {
            if (moves <= parMoves) return 3;
            if (moves <= parMoves + Math.Max(2, parMoves / 5)) return 2;
            return 1;
        }
    }
}
