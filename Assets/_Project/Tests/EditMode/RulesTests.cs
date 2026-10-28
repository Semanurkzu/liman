using NUnit.Framework;

namespace Liman.Core.Tests
{
    public class RulesTests
    {
        [TestCase(10, 10, 3)]
        [TestCase(12, 10, 2)]
        [TestCase(13, 10, 1)]
        [TestCase(30, 25, 2)]
        public void Stars_DependOnDistanceFromPar(int moves, int par, int expected)
        {
            Assert.That(Scoring.Stars(moves, par), Is.EqualTo(expected));
        }

        [Test]
        public void NoInterstitials_DuringOnboarding()
        {
            for (int level = 1; level < AdPolicy.FirstInterstitialLevel; level++)
                Assert.That(AdPolicy.ShouldShowInterstitial(level, false), Is.False, $"level {level}");
        }

        [Test]
        public void Interstitials_FollowCadence()
        {
            Assert.That(AdPolicy.ShouldShowInterstitial(6, false), Is.True);
            Assert.That(AdPolicy.ShouldShowInterstitial(7, false), Is.False);
            Assert.That(AdPolicy.ShouldShowInterstitial(8, false), Is.False);
            Assert.That(AdPolicy.ShouldShowInterstitial(9, false), Is.True);
        }

        [Test]
        public void RemoveAdsPurchase_DisablesInterstitials()
        {
            for (int level = 1; level <= 50; level++)
                Assert.That(AdPolicy.ShouldShowInterstitial(level, true), Is.False);
        }
    }
}
