namespace Liman.Core
{
    /// <summary>
    /// "Fair ads" rules, kept in one testable place: no interstitials during onboarding,
    /// never mid-level, at most one every few levels, none at all after the remove-ads purchase.
    /// Rewarded ads (hints) are always opt-in and are not governed here.
    /// </summary>
    public static class AdPolicy
    {
        public const int FirstInterstitialLevel = 6;
        public const int LevelsBetweenInterstitials = 3;

        public static bool ShouldShowInterstitial(int completedLevel, bool removeAdsOwned)
        {
            if (removeAdsOwned || completedLevel < FirstInterstitialLevel) return false;
            return (completedLevel - FirstInterstitialLevel) % LevelsBetweenInterstitials == 0;
        }
    }
}
