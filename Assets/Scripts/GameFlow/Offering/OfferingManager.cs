using System;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// กองของเซ่น (the map's Treasure node) as its own 3D place (Game Designer, 12 Oct): a clearing in the forest
    /// with a pile of face-down cards. Clicking the pile takes the top card; <see cref="OnOfferingTaken"/> then sends
    /// the player to the reward screen, which offers that one card (CardRewardConfigSO.offeringChoices = 1).
    /// No story, no choices, nothing else to do here. Built by Tools/TawanOS/Offering/Setup Offering Scene.
    /// </summary>
    public class OfferingManager : MonoBehaviour
    {
        public static OfferingManager Instance { get; private set; }

        public event Action OnOfferingTaken;

        public bool Taken { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Called by the pile once the top card has lifted off. Works once per visit.</summary>
        public void Take()
        {
            if (Taken) return;
            Taken = true;
            if (OnOfferingTaken != null)
            {
                OnOfferingTaken.Invoke();
                return;
            }
            Debug.Log("[OfferingManager] Took the top card (no GameFlow listener: scene opened on its own).");
        }
    }
}
