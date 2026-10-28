using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Liman.Core;
using UnityEngine;

namespace Liman.Game
{
    /// <summary>
    /// Owns the level flow (start, moves, undo, hints, completion) and talks to Liman.Core.
    /// Knows nothing about rendering; views subscribe to its events.
    /// Level generation and hint solving run on worker threads (Liman.Core is pure C# and keeps no shared
    /// state), and the next level is generated while the current one is played so "continue" is instant.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private readonly Stack<BoardState> _history = new Stack<BoardState>();
        private readonly Dictionary<int, Task<GeneratedLevel>> _generation = new Dictionary<int, Task<GeneratedLevel>>();
        private IAdService _ads;
        private SaveData _save;
        private int _stateVersion;

        /// <summary>The level in play, or null while <see cref="IsLoading"/>.</summary>
        public BoardState State { get; private set; }
        public GeneratedLevel Level { get; private set; }
        public int LevelNumber { get; private set; }
        public bool IsLoading { get; private set; }
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
            var generation = GenerateInBackground(LevelNumber);
            if (generation.IsCompleted)
            {
                Begin(generation);
                return;
            }

            IsLoading = true;
            State = null;
            Level = null;
            _history.Clear();
            ChangeState();
            StartCoroutine(BeginWhenGenerated(LevelNumber, generation));
        }

        /// <summary>Replays the current level from its start without generating it again.</summary>
        public void Restart()
        {
            if (Level == null) return;
            State = new BoardState(Level.Level);
            _history.Clear();
            ChangeState();
        }

        public void NextLevel()
        {
            if (AdPolicy.ShouldShowInterstitial(LevelNumber, _save.RemoveAdsOwned))
                _ads.ShowInterstitial();
            StartLevel(LevelNumber + 1);
        }

        public bool TryMove(Move move)
        {
            if (State == null || State.IsWon || !State.CanApply(move)) return false;

            var before = State.Clone();
            _history.Push(before);
            State.Apply(move);
            MoveApplied?.Invoke(before, move);
            ChangeState();

            if (State.IsWon) CompleteLevel();
            else if (State.IsStuck) LevelStuck?.Invoke();
            return true;
        }

        public void Undo()
        {
            if (!CanUndo) return;
            State = _history.Pop();
            ChangeState();
        }

        /// <summary>
        /// Hints cost a rewarded ad. The callback gets null if no ad was watched or no solution exists, and is
        /// skipped entirely if the board changed while the solver was running, since the hint would be stale.
        /// </summary>
        public void RequestHint(Action<Move?> onHint)
        {
            if (State == null) return;
            _ads.ShowRewarded(rewarded =>
            {
                if (!rewarded)
                {
                    onHint(null);
                    return;
                }
                var snapshot = State.Clone();
                var solving = Task.Run(() => Solver.Solve(snapshot));
                StartCoroutine(DeliverHint(_stateVersion, solving, onHint));
            });
        }

        private IEnumerator DeliverHint(int version, Task<SolveResult> solving, Action<Move?> onHint)
        {
            while (!solving.IsCompleted) yield return null;
            if (version != _stateVersion) yield break;
            if (solving.IsFaulted)
            {
                Debug.LogException(solving.Exception);
                onHint(null);
                yield break;
            }
            var result = solving.Result;
            onHint(result.Solved && result.Moves.Count > 0 ? result.Moves[0] : (Move?)null);
        }

        private Task<GeneratedLevel> GenerateInBackground(int levelNumber)
        {
            if (!_generation.TryGetValue(levelNumber, out var task))
            {
                task = Task.Run(() => LevelGenerator.ForLevel(levelNumber));
                _generation[levelNumber] = task;
            }
            return task;
        }

        private IEnumerator BeginWhenGenerated(int levelNumber, Task<GeneratedLevel> generation)
        {
            while (!generation.IsCompleted) yield return null;
            // Another StartLevel may have replaced this request while it was generating.
            if (levelNumber == LevelNumber && IsLoading) Begin(generation);
        }

        private void Begin(Task<GeneratedLevel> generation)
        {
            IsLoading = false;
            if (generation.IsFaulted)
            {
                // Should not happen (the generator falls back to its best candidate); retry on the next start.
                Debug.LogException(generation.Exception);
                _generation.Remove(LevelNumber);
                return;
            }

            Level = generation.Result;
            State = new BoardState(Level.Level);
            _history.Clear();
            ForgetGenerationsBefore(LevelNumber);
            GenerateInBackground(LevelNumber + 1);
            ChangeState();
        }

        private void ForgetGenerationsBefore(int levelNumber)
        {
            var stale = new List<int>();
            foreach (int key in _generation.Keys)
                if (key < levelNumber) stale.Add(key);
            foreach (int key in stale) _generation.Remove(key);
        }

        private void ChangeState()
        {
            _stateVersion++;
            StateChanged?.Invoke();
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
