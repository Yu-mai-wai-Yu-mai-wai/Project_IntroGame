using System;
using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// วัด (a map node): the player bows to the Buddha once per visit, for free, then leaves. Khwan below max goes
    /// back to full; with full Khwan the bow gives ขวัญชั่วคราว +5 instead, stacking on any already there
    /// (Game Designer, 15 Oct; replaces the heal / max Khwan +3 choice).
    /// Raises <see cref="OnTempleFinished"/> when the player leaves.
    /// </summary>
    public class TempleManager : MonoBehaviour
    {
        public static TempleManager Instance { get; private set; }

        [Tooltip("The 3D temple that runs the visit.")]
        public TempleStageView stage;

        public event Action OnTempleFinished;

        // One blessing per visit
        public bool Used { get; private set; }

        [Tooltip("ขวัญชั่วคราว from bowing with full Khwan.")]
        [Min(0)] public int tempKhwanOnFull = 5;

        public bool CanPray => !Used;

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
        }

        /// <summary>
        /// Khwan back to full, or ขวัญชั่วคราว +<see cref="tempKhwanOnFull"/> when already full.
        /// Returns the Khwan gained (0 if already used this visit).
        /// </summary>
        public int Pray()
        {
            if (!CanPray) return 0;
            var run = RunState.Current;
            int before = run.CurrentHp;
            if (run.CurrentHp < run.MaxHp) run.Heal(run.MaxHp - run.CurrentHp);
            else run.AddTempKhwan(tempKhwanOnFull);
            Used = true;
            return run.CurrentHp - before;
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
