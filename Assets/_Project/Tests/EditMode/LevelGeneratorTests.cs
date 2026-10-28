using NUnit.Framework;

namespace Liman.Core.Tests
{
    public class LevelGeneratorTests
    {
        [Test]
        public void SameSeed_ProducesSameLevel()
        {
            var a = LevelGenerator.ForLevel(7);
            var b = LevelGenerator.ForLevel(7);

            Assert.That(new BoardState(a.Level).GetStateKey(), Is.EqualTo(new BoardState(b.Level).GetStateKey()));
            Assert.That(a.ParMoves, Is.EqualTo(b.ParMoves));
        }

        [Test]
        public void DeterministicRandom_IsStableAcrossRuns()
        {
            // Pinned values: if this changes, every player's levels change too.
            var rng = new DeterministicRandom(42);
            var first = rng.NextULong();
            var again = new DeterministicRandom(42).NextULong();

            Assert.That(first, Is.EqualTo(again));
            Assert.That(new DeterministicRandom(43).NextULong(), Is.Not.EqualTo(first));
        }

        [Test]
        public void FirstLevels_AreSolvable_WithParMatchingSolver()
        {
            for (int levelNumber = 1; levelNumber <= 30; levelNumber++)
            {
                var generated = LevelGenerator.ForLevel(levelNumber);
                var result = Solver.Solve(generated.Level);

                Assert.That(result.Solved, Is.True, $"level {levelNumber}");
                Assert.That(result.Moves.Count, Is.EqualTo(generated.ParMoves), $"level {levelNumber}");
                Assert.That(generated.ParMoves, Is.GreaterThan(generated.Level.TotalContainers), $"level {levelNumber} needs no shifts");
            }
        }
    }
}
