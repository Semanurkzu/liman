using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Liman.Core;
using UnityEngine;

namespace Liman.Game
{
    public enum HintOutcome
    {
        /// <summary>The ad was watched; the move is the first step of an optimal solution.</summary>
        Found,

        /// <summary>The player skipped or closed the rewarded ad.</summary>
        AdDeclined,

        /// <summary>The board can no longer be won; no ad was shown.</summary>
        Unsolvable,

        /// <summary>The solver gave up or failed, so there is no hint to offer; no ad was shown.</summary>
        NotFound,
    }

    /// <summary>
    /// Owns the level flow (start, moves, undo, hints, completion) and talks to Liman.Core.
    /// Knows nothing about rendering; views subscribe to its events.
    /// Level generation and hint solving run on worker threads (Liman.Core is pure C# and keeps no shared
    /// state), and the next level is generated while the current one is played so "continue" is instant.
    /// Every board the player reaches is also solved in the background: that detects dead ends that still have
    /// legal moves, and lets a hint be checked before the player is asked to watch an ad for it.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        private readonly Stack<BoardState> _history = new Stack<BoardState>();
        private readonly Dictionary<int, Task<GeneratedLevel>> _generation = new Dictionary<int, Task<GeneratedLevel>>();
        private IAdService _ads;
        private SaveData _save;
        private int _stateVersion;

        // At most one background solve runs at a time; when it finishes on a stale board, the latest board is
        // solved next, so fast play never queues up solves.
        private Task<SolveResult> _analysis;

        /// <summary>Solution of the board at <see cref="_solutionVersion"/>; null if that solve failed.</summary>
        private SolveResult _solution;
        private int _solutionVersion = -1;
        private int _stuckVersion = -1;

        /// <summary>The level in play, or null while <see cref="IsLoading"/>.</summary>
        public BoardState State { get; private set; }
        public GeneratedLevel Level { get; private set; }
        public int LevelNumber { get; private set; }
        public bool IsLoading { get; private set; }
        public bool CanUndo => _history.Count > 0;

        /// <summary>
        /// Moves made since the level (re)started, used for stars. Unlike <see cref="BoardState.MoveCount"/> it
        /// is not rewound by undo, otherwise undoing every mistake would make three stars free.
        /// </summary>
        public int MoveCount { get; private set; }

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
            MoveCount = 0;
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
            MoveCount++;
            MoveApplied?.Invoke(before, move);
            ChangeState();

            if (State.IsWon) CompleteLevel();
            else if (State.IsStuck) ReportStuck();
            return true;
        }

        public void Undo()
        {
            if (!CanUndo) return;
            State = _history.Pop();
            ChangeState();
        }

        /// <summary>
        /// Hints cost a rewarded ad, which is only offered once the board is known to have a solution, so the
        /// player never watches an ad for nothing. The move is only set for <see cref="HintOutcome.Found"/>.
        /// The callback is skipped entirely if the board changes before it can be delivered, since the hint
        /// would be stale.
        /// </summary>
        public void RequestHint(Action<HintOutcome, Move?> onHint)
        {
            if (State == null || State.IsWon) return;
            StartCoroutine(OfferHint(_stateVersion, onHint));
        }

        private IEnumerator OfferHint(int version, Action<HintOutcome, Move?> onHint)
        {
            while (_solutionVersion != version)
            {
                if (version != _stateVersion) yield break;
                yield return null;
            }

            var solution = _solution;
            if (solution == null || solution.HitStateLimit)
            {
                onHint(HintOutcome.NotFound, null);
                yield break;
            }
            if (!solution.Solved)
            {
                onHint(HintOutcome.Unsolvable, null);
                yield break;
            }

            _ads.ShowRewarded(rewarded =>
            {
                if (version != _stateVersion) return;
                if (rewarded) onHint(HintOutcome.Found, solution.Moves[0]);
                else onHint(HintOutcome.AdDeclined, null);
            });
        }

        private void AnalyzeInBackground()
        {
            if (State == null || State.IsWon) return;
            if (_analysis != null && !_analysis.IsCompleted) return;

            var snapshot = State.Clone();
            var analysis = Task.Run(() => Solver.Solve(snapshot));
            _analysis = analysis;
            StartCoroutine(FinishAnalysis(_stateVersion, analysis));
        }

        private IEnumerator FinishAnalysis(int version, Task<SolveResult> analysis)
        {
            while (!analysis.IsCompleted) yield return null;
            if (version != _stateVersion)
            {
                AnalyzeInBackground();
                yield break;
            }

            if (analysis.IsFaulted) Debug.LogException(analysis.Exception);
            _solution = analysis.IsFaulted ? null : analysis.Result;
            _solutionVersion = version;
            // A solve that hit the state limit proves nothing, so only a complete search reports a dead end.
            if (_solution != null && !_solution.Solved && !_solution.HitStateLimit) ReportStuck();
        }

        /// <summary>Raises <see cref="LevelStuck"/> at most once per board.</summary>
        private void ReportStuck()
        {
            if (_stuckVersion == _stateVersion) return;
            _stuckVersion = _stateVersion;
            LevelStuck?.Invoke();
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
            MoveCount = 0;
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
            AnalyzeInBackground();
        }

        private void CompleteLevel()
        {
            int stars = Scoring.Stars(MoveCount, Level.ParMoves);
            _save.RecordCompletion(LevelNumber, stars);
            _save.Save();
            LevelCompleted?.Invoke(stars);
        }
    }
}
