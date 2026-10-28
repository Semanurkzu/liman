using System;
using System.Collections.Generic;
using Liman.Core;
using UnityEngine;

namespace Liman.Game
{
    /// <summary>
    /// Owns the level flow (start, moves, undo, hints, completion) and talks to Liman.Core.
    /// Knows nothing about rendering; views subscribe to its events.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private readonly Stack<BoardState> _history = new Stack<BoardState>();
        private IAdService _ads;
        private SaveData _save;

        public BoardState State { get; private set; }
        public GeneratedLevel Level { get; private set; }
        public int LevelNumber { get; private set; }
        public bool CanUndo => _history.Count > 0;

        public event Action StateChanged;

        /// <summary>Raised after a move is applied and before <see cref="StateChanged"/>, with the state before the move.</summary>
        public event Action<BoardState, Move> MoveApplied;
        public event Action<int> LevelCompleted;
        public event Action LevelStuck;

        public void Initialize(IAdService ads)
        {
            _ads = ads;
            _save = SaveData.Load();
            StartLevel(_save.CurrentLevel);
        }

        public void StartLevel(int levelNumber)
        {
            LevelNumber = Mathf.Max(1, levelNumber);
            Level = LevelGenerator.ForLevel(LevelNumber);
            State = new BoardState(Level.Level);
            _history.Clear();
            StateChanged?.Invoke();
        }

        public void Restart() => StartLevel(LevelNumber);

        public void NextLevel()
        {
            if (AdPolicy.ShouldShowInterstitial(LevelNumber, _save.RemoveAdsOwned))
                _ads.ShowInterstitial();
            StartLevel(LevelNumber + 1);
        }

        public bool TryMove(Move move)
        {
            if (State.IsWon || !State.CanApply(move)) return false;

            var before = State.Clone();
            _history.Push(before);
            State.Apply(move);
            MoveApplied?.Invoke(before, move);
            StateChanged?.Invoke();

            if (State.IsWon) CompleteLevel();
            else if (State.IsStuck) LevelStuck?.Invoke();
            return true;
        }

        public void Undo()
        {
            if (!CanUndo) return;
            State = _history.Pop();
            StateChanged?.Invoke();
        }

        /// <summary>Hints cost a rewarded ad. The callback gets null if no ad was watched or no solution exists.</summary>
        public void RequestHint(Action<Move?> onHint)
        {
            _ads.ShowRewarded(rewarded =>
            {
                if (!rewarded)
                {
                    onHint(null);
                    return;
                }
                var result = Solver.Solve(State);
                onHint(result.Solved && result.Moves.Count > 0 ? result.Moves[0] : (Move?)null);
            });
        }

        private void CompleteLevel()
        {
            int stars = Scoring.Stars(State.MoveCount, Level.ParMoves);
            _save.RecordCompletion(LevelNumber, stars);
            _save.Save();
            LevelCompleted?.Invoke(stars);
        }
    }
}
