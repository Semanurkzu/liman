using NUnit.Framework;

namespace Liman.Core.Tests
{
    public class SpecialContainerTests
    {
        private const int Red = 0;
        private const int Blue = 1;

        private static readonly int HeavyRed = Container.Make(Red, Container.Heavy);
        private static readonly int LockedBlue = Container.Make(Blue, Container.Locked);

        // Stacks are listed bottom -> top.
        private static BoardState Board(int stackCapacity, int docks, int[][] stacks, params Ship[] ships) =>
            new BoardState(new LevelDefinition(stackCapacity, docks, stacks, ships));

        [Test]
        public void Heavy_CannotBeStackedOnAnotherContainer()
        {
            var state = Board(3, 1, new[] { new[] { HeavyRed }, new[] { Red }, new int[0] }, new Ship(Red, 2));

            Assert.That(state.CanApply(Move.Shift(0, 1)), Is.False, "onto same colour");
            Assert.That(state.CanApply(Move.Shift(0, 2)), Is.True, "onto empty stack");
            Assert.That(state.CanApply(Move.Shift(1, 0)), Is.True, "plain container onto a heavy one");
        }

        [Test]
        public void Heavy_LoadsLikeAnyOtherContainer()
        {
            var state = Board(3, 1, new[] { new[] { Red, HeavyRed } }, new Ship(Red, 2));

            state.Apply(Move.Load(0, 0));
            state.Apply(Move.Load(0, 0));

            Assert.That(state.IsWon, Is.True);
        }

        [Test]
        public void Locked_CannotMove_UntilShipOfItsColourDocks()
        {
            var state = Board(3, 1, new[] { new[] { Red, LockedBlue }, new int[0] }, new Ship(Red, 1), new Ship(Blue, 1));

            Assert.That(state.IsTopMovable(0), Is.False);
            Assert.That(state.CanApply(Move.Shift(0, 1)), Is.False);
            Assert.That(state.IsStuck, Is.True, "red is buried under a container that can't move yet");
        }

        [Test]
        public void Locked_Unlocks_WhenShipOfItsColourDocks()
        {
            var state = Board(3, 2, new[] { new[] { Red, LockedBlue }, new int[0] }, new Ship(Red, 1), new Ship(Blue, 1));

            Assert.That(state.IsTopMovable(0), Is.True);
            Assert.That(state.CanApply(Move.Shift(0, 1)), Is.True);
            Assert.That(state.CanApply(Move.Load(0, 1)), Is.True);
        }

        [Test]
        public void ColourQueries_IgnoreFlags()
        {
            var state = Board(3, 1, new[] { new[] { LockedBlue, HeavyRed } }, new Ship(Red, 1), new Ship(Blue, 1));

            Assert.That(state.TopColor(0), Is.EqualTo(Red));
            Assert.That(state.TopContainer(0), Is.EqualTo(HeavyRed));
        }

        [Test]
        public void StateKey_DistinguishesSpecialContainers()
        {
            var plain = Board(3, 1, new[] { new[] { Red } }, new Ship(Red, 1));
            var heavy = Board(3, 1, new[] { new[] { HeavyRed } }, new Ship(Red, 1));

            Assert.That(plain.GetStateKey(), Is.Not.EqualTo(heavy.GetStateKey()));
        }

        [Test]
        public void Solver_RoutesAroundHeavyContainer()
        {
            // Red must reach the ship first, but the heavy blue on top of it can only go to the empty stack.
            var state = Board(2, 1,
                new[] { new[] { Red, Container.Make(Blue, Container.Heavy) }, new[] { Blue }, new int[0] },
                new Ship(Red, 1), new Ship(Blue, 2));

            var result = Solver.Solve(state);

            Assert.That(result.Solved, Is.True);
            Assert.That(result.Moves[0], Is.EqualTo(Move.Shift(0, 2)));
        }

        [Test]
        public void Generator_IntroducesMechanics_AtTheirFirstLevel()
        {
            Assert.That(CountFlag(GeneratorSettings.FirstHeavyLevel - 1, Container.Heavy), Is.Zero);
            Assert.That(CountFlag(GeneratorSettings.FirstHeavyLevel, Container.Heavy), Is.EqualTo(1));
            Assert.That(CountFlag(GeneratorSettings.FirstLockedLevel - 1, Container.Locked), Is.Zero);
            Assert.That(CountFlag(GeneratorSettings.FirstLockedLevel, Container.Locked), Is.EqualTo(1));
        }

        private static int CountFlag(int levelNumber, int flag)
        {
            int count = 0;
            foreach (var stack in LevelGenerator.ForLevel(levelNumber).Level.Stacks)
                foreach (int container in stack)
                    if ((container & flag) != 0) count++;
            return count;
        }
    }
}
