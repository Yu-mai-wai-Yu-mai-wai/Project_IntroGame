using System;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// วัด (a map node): the player bows to the Buddha and the run's Khwan goes back to full, for free, once per
    /// visit; then leaves. The only place in the game that heals besides a few หมอกดำ events (Game Designer, 11 Oct).
    /// Raises <see cref="OnTempleFinished"/> when the player leaves.
    /// </summary>
    public class TempleManager : MonoBehaviour
    {
        public static TempleManager Instance { get; private set; }

        public TempleViewUI view;
        [Tooltip("The 3D temple (TempleStageView); when set it runs the visit instead of the flat screen.")]
        public TempleStageView stage;

        public event Action OnTempleFinished;

        // One blessing per visit
        public bool Used { get; private set; }

        public bool CanPray => !Used && RunState.Current.CurrentHp < RunState.Current.MaxHp;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            Begin();
        }

        private void Begin()
        {
            Used = false;
            if (stage != null) stage.Show(this);
            else if (view != null) view.Show(this);
        }

        /// <summary>Khwan back to full. Returns how much was healed (0 if nothing happened).</summary>
        public int Pray()
        {
            if (!CanPray) return 0;
            var run = RunState.Current;
            int healed = run.MaxHp - run.CurrentHp;
            run.Heal(healed);
            Used = true;
            if (stage == null && view != null) view.ShowResult(healed);
            return healed;
        }

        public void Leave()
        {
            if (OnTempleFinished != null)
            {
                OnTempleFinished.Invoke();
                return;
            }
            // Scene opened on its own: start over so designers can keep testing
            Debug.Log($"[TempleManager] Left the temple (no GameFlow listener). Khwan {RunState.Current.CurrentHp}/{RunState.Current.MaxHp}.");
            Begin();
        }
    }
}
