using System.Collections;
using TawanOS.CardEngine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TawanOS.Audio
{
    /// <summary>
    /// Decides what plays when: music and ambience per scene, and sound effects from events the card/combat
    /// engines already raise. Only subscribes; it never changes game logic.
    /// </summary>
    public class AudioDirector : MonoBehaviour
    {
        private const string CombatScene = "CombatTestScene";
        private int lastMerit = -1;

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void Start()
        {
            // The first scene is already loaded when this object is created, so route it once by hand.
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            StopAllCoroutines();

            switch (scene.name)
            {
                case "MainMenu": audio.PlayBgm("bgm_title"); audio.PlayAmbience("amb_map"); break;
                case "MapTestScene":
                case "ShopScene":
                case "RewardScene":
                case "MeruScene": audio.PlayBgm("bgm_map"); audio.PlayAmbience("amb_map"); break;
                case "EventScene": audio.PlayBgm("bgm_event"); audio.PlayAmbience("amb_event"); break;
                case "VictoryScene": audio.PlayBgm("bgm_title"); audio.PlayAmbience(null); break;
                case CombatScene: StartCoroutine(RouteCombat(audio)); break;
            }

            // Card sounds also exist in the combat test scene only; the managers are scene objects.
            BindCardEvents(audio);
        }

        private void BindCardEvents(AudioManager audio)
        {
            var cards = CardManager.Instance;
            if (cards != null)
            {
                cards.OnCardDrawn += _ => audio.PlaySfx("sfx_card_draw");
                cards.OnCardPlayed += _ => audio.PlaySfx("sfx_card_play");
                cards.OnDeckReshuffled += () => audio.PlaySfx("sfx_card_shuffle");
            }
        }

        // The enemy is handed to CombatManager after the scene loads, so wait for it to know boss from minor.
        private IEnumerator RouteCombat(AudioManager audio)
        {
            float waited = 0f;
            while ((CombatManager.Instance == null || CombatManager.Instance.currentEnemyProfile == null) && waited < 5f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            var combat = CombatManager.Instance;
            bool boss = combat != null && combat.currentEnemyProfile != null && combat.currentEnemyProfile.isBoss;
            audio.PlayBgm(boss ? "bgm_boss" : "bgm_combat");
            audio.PlayAmbience(boss ? "amb_boss" : "amb_combat");
            audio.PlaySfx("sfx_fire_on");
            if (combat == null) yield break;

            lastMerit = -1;
            combat.OnMeritChanged += (current, _) =>
            {
                if (lastMerit >= 0 && current > lastMerit) audio.PlaySfx("sfx_merit_gain");
                lastMerit = current;
            };
            combat.OnCurseBackfireTriggered += () =>
            {
                audio.PlaySfx("sfx_corruption_break");
                audio.PlaySfx("sfx_corruption_thunder");
            };
            combat.OnCombatEnded += victory => audio.PlaySfx(victory ? "sfx_scream" : "sfx_fire_off");

            if (EffectResolver.Instance != null)
            {
                // second argument true = the player took the damage
                EffectResolver.Instance.OnDamageDealt += (_, playerHit) =>
                {
                    if (playerHit) { audio.PlaySfx("sfx_hit_taken"); audio.PlaySfx("sfx_khwan_shake"); }
                    else audio.PlaySfx("sfx_attack");
                };
            }
            if (TurnPhaseController.Instance != null)
            {
                TurnPhaseController.Instance.OnPhaseChanged += phase =>
                {
                    if (phase == TurnPhase.End) audio.PlaySfx("sfx_turn_end");
                };
            }
        }
    }
}
