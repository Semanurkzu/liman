using System;
using System.Collections.Generic;

namespace Liman.Core
{
    /// <summary>
    /// Immutable starting layout of a level. Stacks are listed bottom to top as <see cref="Container"/> values;
    /// ships in arrival order.
    /// </summary>
    public sealed class LevelDefinition
    {
        public int StackCapacity { get; }
        public int DockCount { get; }
        public IReadOnlyList<IReadOnlyList<int>> Stacks { get; }
        public IReadOnlyList<Ship> Ships { get; }
        public int TotalContainers { get; }

        public LevelDefinition(int stackCapacity, int dockCount, IEnumerable<IEnumerable<int>> stacks, IEnumerable<Ship> ships)
        {
            if (stackCapacity < 1) throw new ArgumentOutOfRangeException(nameof(stackCapacity));
            if (dockCount < 1) throw new ArgumentOutOfRangeException(nameof(dockCount));

            var demand = new Dictionary<int, int>();
            var stackList = new List<IReadOnlyList<int>>();
            foreach (var stack in stacks ?? throw new ArgumentNullException(nameof(stacks)))
            {
                var copy = new List<int>(stack);
                if (copy.Count > stackCapacity) throw new ArgumentException("A stack exceeds the stack capacity.", nameof(stacks));
                foreach (int container in copy)
                {
                    int color = Container.ColorOf(container);
                    demand.TryGetValue(color, out int count);
                    demand[color] = count + 1;
                }
                TotalContainers += copy.Count;
                stackList.Add(copy);
            }

            var shipList = new List<Ship>(ships ?? throw new ArgumentNullException(nameof(ships)));
            foreach (var ship in shipList)
            {
                demand.TryGetValue(ship.Color, out int count);
                demand[ship.Color] = count - ship.Capacity;
            }

            // Every container must end up on a ship and every ship must leave full, otherwise the level can't be won.
            foreach (var pair in demand)
            {
                if (pair.Value != 0)
                    throw new ArgumentException($"Containers of color {pair.Key} do not exactly fill the ships of that color.");
            }

            StackCapacity = stackCapacity;
            DockCount = dockCount;
            Stacks = stackList;
            Ships = shipList;
        }
    }
}
