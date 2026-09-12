using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    public class EnemyCombatView : MonoBehaviour
    {
        [Header("UI References")]
        public Image portraitImage;
        public TMP_Text nameText;
        public Slider khwanSlider;
        public TMP_Text khwanText;

        [Header("Intent Telegraphing (+6 Bonus Criteria)")]
        public GameObject intentRoot;
        public Image intentIcon;
        public TMP_Text intentValueText;
        public TMP_Text intentDescText;

        [Header("Animation Settings")]
        public Transform visualRoot;
        public float shakeDuration = 0.25f;
        public float shakeStrength = 15f;

        private void Start()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnPhaseChanged += HandlePhaseChanged;
            }
            if (EffectResolver.Instance != null)
            {
                EffectResolver.Instance.OnDamageDealt += HandleDamageDealt;
            }

            UpdateDisplay();
        }

        private void OnDestroy()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
            }
            if (EffectResolver.Instance != null)
            {
                EffectResolver.Instance.OnDamageDealt -= HandleDamageDealt;
            }
        }

        private void HandlePhaseChanged(CombatPhase phase)
        {
            UpdateDisplay();
        }

        private void HandleDamageDealt(int damage, bool toPlayer)
        {
            if (!toPlayer)
            {
                // Enemy took damage: visual feedback
                UpdateDisplay();
                if (visualRoot != null)
                {
                    visualRoot.DOComplete();
                    visualRoot.DOShakePosition(shakeDuration, shakeStrength, 10, 90, false, true);
                }
            }
        }

        public void UpdateDisplay()
        {
            if (CombatManager.Instance == null) return;

            var profile = CombatManager.Instance.currentEnemyProfile;
            var state = CombatManager.Instance.State;

            if (profile != null)
            {
                if (nameText != null) nameText.text = profile.enemyName;
                if (portraitImage != null && profile.portrait != null)
                {
                    portraitImage.sprite = profile.portrait;
                    portraitImage.gameObject.SetActive(true);
                }
            }

            if (khwanSlider != null)
            {
                khwanSlider.maxValue = state.maxEnemyKhwan;
                khwanSlider.value = state.enemyKhwan;
            }

            if (khwanText != null)
            {
                khwanText.text = $"{state.enemyKhwan} / {state.maxEnemyKhwan}";
            }

            // Intent Telegraphing
            var nextMove = CombatManager.Instance.nextEnemyMove;
            if (nextMove != null && intentRoot != null)
            {
                intentRoot.SetActive(state.enemyKhwan > 0 && state.currentPhase != CombatPhase.Victory);
                if (intentValueText != null)
                {
                    intentValueText.text = nextMove.intent == EnemyIntent.Attack || nextMove.intent == EnemyIntent.HeavyAttack
                        ? $"{nextMove.baseValue}"
                        : "";
                }
                if (intentDescText != null)
                {
                    intentDescText.text = nextMove.moveDescription;
                }
            }
        }
    }
}
