using System;
using UnityEngine;

namespace Liman.Game
{
    /// <summary>Ad network abstraction so gameplay code never depends on a specific SDK (AdMob, Unity Ads, ...).</summary>
    public interface IAdService
    {
        void ShowInterstitial();

        /// <summary>Shows an opt-in rewarded ad; the callback receives true if the reward was earned.</summary>
        void ShowRewarded(Action<bool> onCompleted);
    }

    /// <summary>Stand-in until a real SDK is integrated: logs and grants rewards immediately.</summary>
    public sealed class PlaceholderAdService : IAdService
    {
        public void ShowInterstitial() => Debug.Log("[Ads] Interstitial would show here.");

        public void ShowRewarded(Action<bool> onCompleted)
        {
            Debug.Log("[Ads] Rewarded ad would show here; granting reward.");
            onCompleted(true);
        }
    }
}
