using NUnit.Framework;

namespace Liman.Core.Tests
{
    public class SolverTests
    {
        [Test]
        public void FindsShortestSolution_ThatReplaysToAWin()
        {
            // Color 1 sits on top of color 0, but the color-0 ship docks first: one shift is required.
            var level = new LevelDefinition(3, 1,
                new[] { new[] { 0, 1 }, new int[0] },
                new[] { new Ship(0, 1), new Ship(1, 1) });

            var result = Solver.Solve(level);

            Assert.That(result.Solved, Is.True);
            Assert.That(result.Moves.Count, Is.EqualTo(3));

            var state = new BoardState(level);
            foreach (var move in result.Moves)
                state.Apply(move);
            Assert.That(state.IsWon, Is.True);
        }

        [Test]
        public void ReportsUnsolvableLevel()
        {
            var level = new LevelDefinition(2, 1,
                new[] { new[] { 0, 1 }, new[] { 0, 1 } },
                new[] { new Ship(0, 2), new Ship(1, 2) });

            var result = Solver.Solve(level);

            Assert.That(result.Solved, Is.False);
            Assert.That(result.HitStateLimit, Is.False);
        }

        [Test]
        public void SolvesFromMidGameState_ForHints()
        {
            var level = new LevelDefinition(3, 1,
                new[] { new[] { 0, 1 }, new int[0] },
                new[] { new Ship(0, 1), new Ship(1, 1) });
            var state = new BoardState(level);
            state.Apply(Move.Shift(0, 1));

            var result = Solver.Solve(state);

            Assert.That(result.Solved, Is.True);
            Assert.That(result.Moves.Count, Is.EqualTo(2));
            Assert.That(state.CanApply(result.Moves[0]), Is.True);
        }
    }
}
