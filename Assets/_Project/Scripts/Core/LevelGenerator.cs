using System;
using System.Collections.Generic;

namespace Liman.Core
{
    public sealed class GeneratorSettings
    {
        public int ColorCount { get; set; } = 3;
        public int ShipCapacity { get; set; } = 3;
        public int ShipsPerColor { get; set; } = 1;
        public int StackCapacity { get; set; } = 4;
        public int EmptyStacks { get; set; } = 2;
        public int DockCount { get; set; } = 2;
        public int HeavyContainers { get; set; }
        public int LockedContainers { get; set; }

        /// <summary>Minimum number of Shift moves the optimal solution must need; the main difficulty knob.</summary>
        public int MinShifts { get; set; }

        public int MaxAttempts { get; set; } = 40;
        public int MaxSolverStates { get; set; } = Solver.DefaultMaxStates;

        /// <summary>First level with heavy containers; the game introduces the mechanic there.</summary>
        public const int FirstHeavyLevel = 8;

        /// <summary>First level with locked containers; the game introduces the mechanic there.</summary>
        public const int FirstLockedLevel = 15;

        /// <summary>Difficulty curve. Tune these numbers with playtesting.</summary>
        public static GeneratorSettings ForLevel(int level)
        {
            level = Math.Max(1, level);
            int shipCapacity = level < 10 ? 3 : 4;
            return new GeneratorSettings
            {
                ColorCount = Math.Min(3 + (level - 1) / 5, 6),
                ShipCapacity = shipCapacity,
                ShipsPerColor = 1,
                StackCapacity = shipCapacity + 1,
                EmptyStacks = level < 6 ? 2 : 1,
                DockCount = 2,
                HeavyContainers = level < FirstHeavyLevel ? 0 : Math.Min(1 + (level - FirstHeavyLevel) / 6, 3),
                LockedContainers = level < FirstLockedLevel ? 0 : Math.Min(1 + (level - FirstLockedLevel) / 8, 3),
                MinShifts = Math.Min(1 + level / 4, 8),
            };
        }
    }

    public sealed class GeneratedLevel
    {
        public LevelDefinition Level { get; }
        public int Seed { get; }

        /// <summary>Length of the optimal solution; matching it earns three stars.</summary>
        public int ParMoves { get; }

        public GeneratedLevel(LevelDefinition level, int seed, int parMoves)
        {
            Level = level;
            Seed = seed;
            ParMoves = parMoves;
        }
    }

    /// <summary>
    /// Builds random layouts from a seed and keeps only ones the solver proves solvable,
    /// so the game can ship "infinite" levels without level data files.
    /// </summary>
    public static class LevelGenerator
    {
        public static int SeedForLevel(int level) => unchecked(level * 7919 + 17);

        public static GeneratedLevel ForLevel(int level) =>
            Generate(SeedForLevel(level), GeneratorSettings.ForLevel(level));

        public static GeneratedLevel Generate(int seed, GeneratorSettings settings)
        {
            var rng = new DeterministicRandom(seed);
            GeneratedLevel best = null;
            int bestShifts = -1;

            for (int attempt = 0; attempt < settings.MaxAttempts; attempt++)
            {
                var level = BuildCandidate(rng, settings);
                var result = Solver.Solve(level, settings.MaxSolverStates);
                if (!result.Solved) continue;

                int shifts = result.Moves.Count - level.TotalContainers;
                if (shifts >= settings.MinShifts) return new GeneratedLevel(level, seed, result.Moves.Count);

                // Fall back to the hardest solvable candidate rather than failing the level.
                if (shifts > bestShifts)
                {
                    bestShifts = shifts;
                    best = new GeneratedLevel(level, seed, result.Moves.Count);
                }
            }

            return best ?? throw new InvalidOperationException(
                $"No solvable level found for seed {seed}; loosen the generator settings.");
        }

        private static LevelDefinition BuildCandidate(DeterministicRandom rng, GeneratorSettings settings)
        {
            var ships = new List<Ship>();
            for (int color = 0; color < settings.ColorCount; color++)
                for (int i = 0; i < settings.ShipsPerColor; i++)
                    ships.Add(new Ship(color, settings.ShipCapacity));
            rng.Shuffle(ships);

            var containers = new List<int>();
            foreach (var ship in ships)
                for (int i = 0; i < ship.Capacity; i++)
                    containers.Add(ship.Color);
            rng.Shuffle(containers);
            // Only draw from the RNG when a level has special containers, so earlier levels stay unchanged.
            MarkRandom(rng, containers, settings.HeavyContainers, Container.Heavy);
            MarkRandom(rng, containers, settings.LockedContainers, Container.Locked);

            var stacks = new List<List<int>>();
            for (int i = 0; i < containers.Count; i += settings.StackCapacity)
                stacks.Add(containers.GetRange(i, Math.Min(settings.StackCapacity, containers.Count - i)));
            for (int i = 0; i < settings.EmptyStacks; i++)
                stacks.Add(new List<int>());

            return new LevelDefinition(settings.StackCapacity, settings.DockCount, stacks, ships);
        }

        /// <summary>Adds <paramref name="flag"/> to <paramref name="count"/> distinct containers that have no flags yet.</summary>
        private static void MarkRandom(DeterministicRandom rng, List<int> containers, int count, int flag)
        {
            var plain = new List<int>();
            for (int i = 0; i < containers.Count; i++)
                if (containers[i] == Container.ColorOf(containers[i])) plain.Add(i);

            for (int i = 0; i < count && plain.Count > 0; i++)
            {
                int pick = rng.Next(plain.Count);
                containers[plain[pick]] |= flag;
                plain.RemoveAt(pick);
            }
        }
    }
}
