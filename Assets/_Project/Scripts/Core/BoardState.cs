using System;
using System.Collections.Generic;
using System.Text;

namespace Liman.Core
{
    /// <summary>
    /// Mutable state of a level in progress: yard stacks, docked ships and the arrival queue.
    /// Pure C# (no UnityEngine) so the solver and unit tests can use it directly.
    /// </summary>
    public sealed class BoardState
    {
        public const int Empty = -1;

        private readonly List<int>[] _stacks;
        private readonly IReadOnlyList<Ship> _ships;
        private readonly int[] _dockShip;
        private readonly int[] _dockLoad;
        private int _nextShip;

        public int StackCapacity { get; }
        public int MoveCount { get; private set; }
        public int StackCount => _stacks.Length;
        public int DockCount => _dockShip.Length;
        public int ShipsWaiting => _ships.Count - _nextShip;

        public BoardState(LevelDefinition level)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            StackCapacity = level.StackCapacity;
            _ships = level.Ships;
            _stacks = new List<int>[level.Stacks.Count];
            for (int i = 0; i < _stacks.Length; i++)
            {
                _stacks[i] = new List<int>(StackCapacity);
                _stacks[i].AddRange(level.Stacks[i]);
            }

            _dockShip = new int[level.DockCount];
            _dockLoad = new int[level.DockCount];
            for (int dock = 0; dock < _dockShip.Length; dock++)
                DockNextShip(dock);
        }

        private BoardState(BoardState other)
        {
            StackCapacity = other.StackCapacity;
            MoveCount = other.MoveCount;
            _ships = other._ships;
            _nextShip = other._nextShip;
            _dockShip = (int[])other._dockShip.Clone();
            _dockLoad = (int[])other._dockLoad.Clone();
            _stacks = new List<int>[other._stacks.Length];
            for (int i = 0; i < _stacks.Length; i++)
            {
                _stacks[i] = new List<int>(StackCapacity);
                _stacks[i].AddRange(other._stacks[i]);
            }
        }

        public BoardState Clone() => new BoardState(this);

        public IReadOnlyList<int> GetStack(int stack) => _stacks[stack];

        public int TopColor(int stack)
        {
            var s = _stacks[stack];
            return s.Count == 0 ? Empty : s[s.Count - 1];
        }

        /// <summary>The ship currently at the dock, or null if the dock is empty.</summary>
        public Ship GetDockedShip(int dock) => _dockShip[dock] == Empty ? null : _ships[_dockShip[dock]];

        public int GetDockLoad(int dock) => _dockLoad[dock];

        public IEnumerable<Ship> GetWaitingShips()
        {
            for (int i = _nextShip; i < _ships.Count; i++)
                yield return _ships[i];
        }

        public bool IsWon
        {
            get
            {
                if (_nextShip < _ships.Count) return false;
                foreach (int ship in _dockShip)
                    if (ship != Empty) return false;
                foreach (var stack in _stacks)
                    if (stack.Count > 0) return false;
                return true;
            }
        }

        public bool IsStuck => !IsWon && !HasLegalMoves();

        public bool CanApply(Move move)
        {
            switch (move.Type)
            {
                case MoveType.Load:
                {
                    if (!IsValidStack(move.From) || move.To < 0 || move.To >= _dockShip.Length) return false;
                    var ship = GetDockedShip(move.To);
                    int color = TopColor(move.From);
                    return ship != null && color != Empty && color == ship.Color;
                }
                case MoveType.Shift:
                {
                    if (!IsValidStack(move.From) || !IsValidStack(move.To) || move.From == move.To) return false;
                    int color = TopColor(move.From);
                    if (color == Empty || _stacks[move.To].Count >= StackCapacity) return false;
                    int targetTop = TopColor(move.To);
                    return targetTop == Empty || targetTop == color;
                }
                default:
                    return false;
            }
        }

        /// <summary>Applies a legal move. Returns true when the move filled a ship and it left port.</summary>
        public bool Apply(Move move)
        {
            if (!CanApply(move)) throw new InvalidOperationException($"Illegal move: {move}");

            var from = _stacks[move.From];
            int color = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            MoveCount++;

            if (move.Type == MoveType.Shift)
            {
                _stacks[move.To].Add(color);
                return false;
            }

            int dock = move.To;
            _dockLoad[dock]++;
            if (_dockLoad[dock] < _ships[_dockShip[dock]].Capacity) return false;

            DockNextShip(dock);
            return true;
        }

        public void GetLegalMoves(List<Move> results)
        {
            results.Clear();
            for (int s = 0; s < _stacks.Length; s++)
            {
                if (_stacks[s].Count == 0) continue;
                for (int d = 0; d < _dockShip.Length; d++)
                {
                    var load = Move.Load(s, d);
                    if (CanApply(load)) results.Add(load);
                }
                for (int t = 0; t < _stacks.Length; t++)
                {
                    var shift = Move.Shift(s, t);
                    if (CanApply(shift)) results.Add(shift);
                }
            }
        }

        public bool HasLegalMoves()
        {
            for (int s = 0; s < _stacks.Length; s++)
            {
                if (_stacks[s].Count == 0) continue;
                for (int d = 0; d < _dockShip.Length; d++)
                    if (CanApply(Move.Load(s, d))) return true;
                for (int t = 0; t < _stacks.Length; t++)
                    if (CanApply(Move.Shift(s, t))) return true;
            }
            return false;
        }

        /// <summary>
        /// Canonical key for search: stack and dock order are ignored because they don't affect solvability,
        /// which shrinks the solver's state space considerably.
        /// </summary>
        public string GetStateKey()
        {
            var sb = new StringBuilder();
            var stacks = new string[_stacks.Length];
            for (int i = 0; i < _stacks.Length; i++)
            {
                sb.Clear();
                foreach (int color in _stacks[i])
                    sb.Append((char)('a' + color));
                stacks[i] = sb.ToString();
            }
            Array.Sort(stacks, StringComparer.Ordinal);

            var docks = new string[_dockShip.Length];
            for (int d = 0; d < docks.Length; d++)
                docks[d] = _dockShip[d] + ":" + _dockLoad[d];
            Array.Sort(docks, StringComparer.Ordinal);

            return string.Join("/", stacks) + "|" + _nextShip + "|" + string.Join(",", docks);
        }

        private bool IsValidStack(int stack) => stack >= 0 && stack < _stacks.Length;

        private void DockNextShip(int dock)
        {
            _dockLoad[dock] = 0;
            _dockShip[dock] = _nextShip < _ships.Count ? _nextShip++ : Empty;
        }
    }
}
