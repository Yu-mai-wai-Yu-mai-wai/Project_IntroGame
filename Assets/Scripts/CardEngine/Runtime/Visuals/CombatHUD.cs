using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace TawanOS.CardEngine
{
    public class CombatHUD : MonoBehaviour
    {
        [Header("Player Khwan & Dual Energy")]
        public Slider playerKhwanSlider;
        public TMP_Text playerKhwanText;
        public TMP_Text meritValueText;
        public Slider corruptionSlider;
        public TMP_Text corruptionText;
        public GameObject playerShieldRoot;
        public TMP_Text playerShieldText;

        [Header("Piles & Turns")]
        public TMP_Text drawCountText;
        public TMP_Text discardCountText;
        public TMP_Text turnText;
        public TMP_Text phaseText;

        [Header("Controls & Overlays")]
        public Button endTurnButton;
        public GameObject victoryPanel;
        public GameObject defeatPanel;
        public GameObject curseBackfirePopup;

        private void Start()
        {
            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(OnEndTurnClicked);
            }

            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnPhaseChanged += HandlePhaseChanged;
                CombatManager.Instance.OnMeritChanged += HandleMeritChanged;
                CombatManager.Instance.OnCorruptionChanged += HandleCorruptionChanged;
                CombatManager.Instance.OnShieldChanged += HandleShieldChanged;
                CombatManager.Instance.OnCurseBackfireTriggered += HandleCurseBackfire;
                CombatManager.Instance.OnCombatEnded += HandleCombatEnded;
            }

            if (EffectResolver.Instance != null)
            {
                EffectResolver.Instance.OnDamageDealt += (dmg, toPlayer) => UpdateStats();
            }

            if (CardManager.Instance != null)
            {
                CardManager.Instance.OnCardDrawn += (c) => UpdatePileCounts();
                CardManager.Instance.OnCardDiscarded += (c) => UpdatePileCounts();
                CardManager.Instance.OnDeckReshuffled += UpdatePileCounts;
            }

            UpdateStats();
            UpdatePileCounts();

            if (CombatManager.Instance != null)
            {
                HandleMeritChanged(CombatManager.Instance.CurrentMerit, CombatManager.Instance.State.maxMerit);
            }
        }

        private void OnDestroy()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.OnPhaseChanged -= HandlePhaseChanged;
                CombatManager.Instance.OnMeritChanged -= HandleMeritChanged;
                CombatManager.Instance.OnCorruptionChanged -= HandleCorruptionChanged;
                CombatManager.Instance.OnShieldChanged -= HandleShieldChanged;
                CombatManager.Instance.OnCurseBackfireTriggered -= HandleCurseBackfire;
                CombatManager.Instance.OnCombatEnded -= HandleCombatEnded;
            }
        }

        private void OnEndTurnClicked()
        {
            if (CombatManager.Instance != null)
            {
                CombatManager.Instance.EndPlayerTurn();
            }
        }

        private void HandlePhaseChanged(CombatPhase phase)
        {
            if (phaseText != null)
            {
                phaseText.text = phase == CombatPhase.PlayerTurn ? "เทิร์นของคุณ" : "เทิร์นของศัตรู...";
            }

            if (endTurnButton != null)
            {
                endTurnButton.interactable = (phase == CombatPhase.PlayerTurn);
            }

            if (turnText != null && CombatManager.Instance != null)
            {
                turnText.text = $"เทิร์นที่ {CombatManager.Instance.State.turnNumber}";
            }

            UpdateStats();
        }

        private void HandleMeritChanged(int current, int max)
        {
            if (meritValueText != null)
            {
                meritValueText.text = $"กุศล: {current} / {max}";
            }
        }

        private void HandleShieldChanged(int current, bool toPlayer)
        {
            if (!toPlayer) return;

            if (playerShieldRoot != null) playerShieldRoot.SetActive(current > 0);
            if (playerShieldText != null) playerShieldText.text = $"{current}";
        }

        private void HandleCorruptionChanged(int current, int max)
        {
            if (corruptionSlider != null)
            {
                corruptionSlider.maxValue = max;
                corruptionSlider.value = current;
            }
            if (corruptionText != null)
            {
                corruptionText.text = $"มลทิน: {current} / {max}";
            }
        }

        private void HandleCurseBackfire()
        {
            if (curseBackfirePopup != null)
            {
                curseBackfirePopup.SetActive(true);
                curseBackfirePopup.transform.DOKill();
                curseBackfirePopup.transform.localScale = Vector3.zero;
                curseBackfirePopup.transform.DOScale(1.2f, 0.2f).OnComplete(() =>
                {
                    curseBackfirePopup.transform.DOScale(1f, 0.1f);
                    DOVirtual.DelayedCall(1.2f, () => curseBackfirePopup.SetActive(false));
                });
            }
        }

        private void HandleCombatEnded(bool isVictory)
        {
            if (isVictory && victoryPanel != null) victoryPanel.SetActive(true);
            if (!isVictory && defeatPanel != null) defeatPanel.SetActive(true);
            if (endTurnButton != null) endTurnButton.interactable = false;
        }

        public void UpdateStats()
        {
            if (CombatManager.Instance == null) return;
            var state = CombatManager.Instance.State;

            if (playerKhwanSlider != null)
            {
                playerKhwanSlider.maxValue = state.maxPlayerKhwan;
                playerKhwanSlider.value = state.playerKhwan;
            }

            if (playerKhwanText != null)
            {
                playerKhwanText.text = $"ขวัญ: {state.playerKhwan} / {state.maxPlayerKhwan}";
            }
        }

        public void UpdatePileCounts()
        {
            if (CardManager.Instance == null) return;

            if (drawCountText != null)
            {
                drawCountText.text = $"สำรับ: {CardManager.Instance.DrawPile.Count}";
            }
            if (discardCountText != null)
            {
                discardCountText.text = $"ทิ้ง: {CardManager.Instance.DiscardPile.Count}";
            }
        }
    }
}
