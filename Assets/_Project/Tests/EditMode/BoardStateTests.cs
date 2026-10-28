using NUnit.Framework;

namespace Liman.Core.Tests
{
    public class BoardStateTests
    {
        // Stacks are listed bottom -> top.
        private static BoardState Board(int stackCapacity, int docks, int[][] stacks, params Ship[] ships) =>
            new BoardState(new LevelDefinition(stackCapacity, docks, stacks, ships));

        [Test]
        public void Load_MatchingTopContainer_IsLoadedOntoShip()
        {
            var state = Board(3, 1, new[] { new[] { 0, 0 } }, new Ship(0, 2));

            Assert.That(state.CanApply(Move.Load(0, 0)), Is.True);
            state.Apply(Move.Load(0, 0));

            Assert.That(state.GetDockLoad(0), Is.EqualTo(1));
            Assert.That(state.GetStack(0).Count, Is.EqualTo(1));
            Assert.That(state.MoveCount, Is.EqualTo(1));
        }

        [Test]
        public void Load_MismatchedColor_IsIllegal()
        {
            var state = Board(3, 1, new[] { new[] { 0, 1 } }, new Ship(0, 1), new Ship(1, 1));

            Assert.That(state.CanApply(Move.Load(0, 0)), Is.False);
        }

        [Test]
        public void FullShip_Departs_AndNextShipDocks()
        {
            var state = Board(3, 1, new[] { new[] { 1, 0, 0 } }, new Ship(0, 2), new Ship(1, 1));

            Assert.That(state.Apply(Move.Load(0, 0)), Is.False);
            Assert.That(state.Apply(Move.Load(0, 0)), Is.True);

            Assert.That(state.GetDockedShip(0).Color, Is.EqualTo(1));
            Assert.That(state.GetDockLoad(0), Is.EqualTo(0));
            Assert.That(state.ShipsWaiting, Is.EqualTo(0));
        }

        [Test]
        public void Shift_AllowedOnlyOntoEmptyOrSameColor()
        {
            var state = Board(3, 1,
                new[] { new[] { 0, 1 }, new[] { 1 }, new[] { 0 }, new int[0] },
                new Ship(0, 2), new Ship(1, 2));

            Assert.That(state.CanApply(Move.Shift(0, 1)), Is.True, "same color");
            Assert.That(state.CanApply(Move.Shift(0, 3)), Is.True, "empty stack");
            Assert.That(state.CanApply(Move.Shift(0, 2)), Is.False, "different color");
            Assert.That(state.CanApply(Move.Shift(0, 0)), Is.False, "same stack");
            Assert.That(state.CanApply(Move.Shift(3, 0)), Is.False, "from empty stack");
        }

        [Test]
        public void Shift_IntoFullStack_IsIllegal()
        {
            var state = Board(2, 1, new[] { new[] { 1 }, new[] { 1, 1 } }, new Ship(1, 3));

            Assert.That(state.CanApply(Move.Shift(0, 1)), Is.False);
        }

        [Test]
        public void ClearingEverything_WinsTheLevel()
        {
            var state = Board(3, 1, new[] { new[] { 0 } }, new Ship(0, 1));

            state.Apply(Move.Load(0, 0));

            Assert.That(state.IsWon, Is.True);
            Assert.That(state.IsStuck, Is.False);
        }

        [Test]
        public void NoLegalMoves_IsStuck()
        {
            var state = Board(2, 1, new[] { new[] { 0, 1 }, new[] { 0, 1 } }, new Ship(0, 2), new Ship(1, 2));

            Assert.That(state.IsStuck, Is.True);
        }

        [Test]
        public void Clone_IsIndependentOfOriginal()
        {
            var state = Board(3, 1, new[] { new[] { 0, 0 } }, new Ship(0, 2));
            var clone = state.Clone();

            clone.Apply(Move.Load(0, 0));

            Assert.That(state.GetStack(0).Count, Is.EqualTo(2));
            Assert.That(state.GetDockLoad(0), Is.EqualTo(0));
            Assert.That(state.MoveCount, Is.EqualTo(0));
        }

        [Test]
        public void StateKey_IgnoresStackOrder()
        {
            var a = Board(3, 1, new[] { new[] { 0 }, new[] { 1 } }, new Ship(0, 1), new Ship(1, 1));
            var b = Board(3, 1, new[] { new[] { 1 }, new[] { 0 } }, new Ship(0, 1), new Ship(1, 1));

            Assert.That(a.GetStateKey(), Is.EqualTo(b.GetStateKey()));
        }

        [Test]
        public void LevelDefinition_RejectsContainersThatDoNotFillShips()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new LevelDefinition(3, 1, new[] { new[] { 0, 0 } }, new[] { new Ship(0, 3) }));
        }
    }
}
