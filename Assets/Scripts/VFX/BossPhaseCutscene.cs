using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TawanOS.CardEngine;
using TawanOS.Settings;
using TawanOS.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.VFX
{
    /// <summary>
    /// The boss's comeback, played when it enters its next phase (CombatManager.OnEnemyPhaseChanged) as a short
    /// cutscene from the player's own view. The clash it happened in stops at once (CombatManager), and the cutscene
    /// waits until the camera is back in the normal view (no clash, no C view, no held card), then the fight freezes
    /// (time scale 0, no input), the room darkens, the boss shakes and roars under a red flash while the scene's main light strobes red and the camera trembles, then it
    /// slams the table: the camera shakes and everything on the table jumps up and lands again. Meanwhile its khwan
    /// bar (CombatStatusHUD) empties and fills up again. The room stays lit red (the light and the ambient, breathing
    /// slowly) for the rest of the fight. The props include the board slots, so the cards in play jump with them;
    /// props standing together (a candle and its flame) jump as one. Everything runs in real time. Reduce Motion keeps
    /// the darkening and the red room only. Placed in CombatTestScene by Tools/TawanOS/Combat Scene/Add Boss Phase
    /// Cutscene, which also fills the boss, the light and the props.
    /// </summary>
    public class BossPhaseCutscene : MonoBehaviour
    {
        /// <summary>True while the cutscene plays: the fight takes no input.</summary>
        public static bool Playing { get; private set; }

        private const int SortingOrder = 40; // over the table, under the HUDs (50): the khwan bar shows over the shade

        [Header("Boss")]
        [Tooltip("ตัวบอสในฉาก สั่นตอนคำราม Add Boss Phase Cutscene เติมให้")]
        public Transform boss;
        [Min(0f)] public float bossShake = 0.25f;

        [Header("Timing (seconds, real time)")]
        [Tooltip("ฉากมืดลงตอนเริ่ม และสว่างกลับตอนจบ")]
        [UnityEngine.Serialization.FormerlySerializedAs("barsTime")]
        [Min(0.05f)] public float fadeTime = 0.35f;
        [Tooltip("บอสคำราม ไฟกะพริบ ก่อนทุบโต๊ะ")]
        [Min(0.05f)] public float roar = 1.4f;
        [Tooltip("ของบนโต๊ะลอยแล้วตกลง ก่อนฉากสว่างกลับ")]
        [Min(0.05f)] public float settle = 0.9f;

        [Header("Darken")]
        [Range(0f, 1f)] public float darken = 0.35f;

        [Header("Red light")]
        [Tooltip("ไฟหลักของฉาก (Directional) ที่กะพริบแดงตอนบอสคืนร่าง และค้างเป็นสีแดงหลังจากนั้น Add Boss Phase Cutscene เติมให้")]
        public Light sceneLight;
        public Color redColor = new Color(1f, 0.1f, 0.06f);
        [Tooltip("ความสว่างตอนไฟกะพริบ")]
        [Min(0f)] public float strobeIntensity = 2.5f;
        [Tooltip("ความสว่างไฟแดงที่ค้างไว้หลัง cutscene")]
        [Min(0f)] public float redIntensity = 0.7f;
        [Tooltip("แสงรอบฉาก (ambient) หลัง cutscene")]
        public Color redAmbient = new Color(0.2f, 0.03f, 0.03f);
        [Tooltip("ไฟแดงหลัง cutscene สว่างขึ้นลง ± สัดส่วนนี้")]
        [Range(0f, 1f)] public float breathe = 0.25f;

        [Header("Slam: props")]
        [Tooltip("ของบนโต๊ะที่เด้งตอนทุบ (เทียน ลูกปัด กองการ์ด ช่องวางการ์ด) Add Boss Phase Cutscene เติมให้")]
        public List<Transform> props = new List<Transform>();
        [Tooltip("ของแต่ละกลุ่มเริ่มเด้งช้ากว่ากันได้สุ่มไม่เกินนี้ (วินาที)")]
        [Min(0f)] public float stagger = 0.06f;
        [Tooltip("ความสูงที่ของเด้งขึ้น ต่ำสุด / สูงสุด (สุ่มต่อกลุ่ม)")]
        public Vector2 jumpHeight = new Vector2(0.6f, 1.2f);
        [Min(0.05f)] public float jumpTime = 0.55f;
        [Tooltip("ของเอียงได้ไม่เกินกี่องศาตอนลอย")]
        [Min(0f)] public float maxTilt = 20f;
        [Tooltip("ของที่วางห่างกันไม่เกินนี้ (แนวราบ) เด้งพร้อมกันและสูงเท่ากัน")]
        [Min(0f)] public float groupRadius = 0.2f;

        [Header("Slam: camera")]
        [Tooltip("กล้องสั่นไปมาไกลแค่ไหนตอนทุบ (หน่วยโลก)")]
        [Min(0f)] public float cameraShake = 1.6f;
        [Tooltip("กล้องสั่นเอียงไปมากี่องศาตอนทุบ")]
        [Min(0f)] public float slamTilt = 4f;
        [Min(0f)] public float shakeTime = 0.8f;
        [Tooltip("กล้องสั่นเอียงไปมากี่องศาตอนบอสคำราม")]
        [Min(0f)] public float roarShake = 3f;
        [Tooltip("กล้องซูมเข้ากี่องศา (field of view) ตอนทุบ แล้วคืน")]
        [Min(0f)] public float fovKick = 6f;

        private CombatManager combat;
        private Canvas canvas;
        private Image shade, flash;
        private bool redLit; // the room is lit red: Update keeps the light breathing
        private bool pending; // the boss changed phase: the cutscene starts once the camera is back home
        private float pendingSince;
        private const float MaxWait = 6f; // real seconds; plays anyway if the normal view never comes back

        /// <summary>Raised when the cutscene starts (after the camera is back in the normal view).</summary>
        public static event System.Action Started;

        private void Start()
        {
            combat = CombatManager.Instance;
            if (combat != null) combat.OnEnemyPhaseChanged += HandlePhase;
        }

        private void OnDestroy()
        {
            if (combat != null) combat.OnEnemyPhaseChanged -= HandlePhase;
            DOTween.Kill(this);
            if (Playing)
            {
                Playing = false;
                Time.timeScale = 1f;
            }
        }

        private void Update()
        {
            if (pending && !Playing && ReadyToPlay())
            {
                pending = false;
                Play();
            }
            if (!redLit || sceneLight == null || GameSettings.ReduceMotion) return;
            sceneLight.intensity = redIntensity * (1f + breathe * Mathf.Sin(Time.unscaledTime * 1.7f));
        }

        private void HandlePhase(int phase, EnemyProfileSO profile)
        {
            // The player fell in the same blow: the defeat screen comes instead
            if (combat != null && combat.State.playerKhwan <= 0) return;
            pending = true;
            pendingSince = Time.realtimeSinceStartup;
            var rig = CombatCameraRig3D.Instance;
            if (rig != null) rig.ReturnHome(); // out of the player's C view
        }

        // Not during the clash (its camera lock comes off when it ends) nor with the camera away from the normal view
        private bool ReadyToPlay()
        {
            if (combat != null && combat.IsCombatOver) { pending = false; return false; }
            if (Time.realtimeSinceStartup - pendingSince > MaxWait) return true;
            var turns = TurnPhaseController.Instance;
            if (turns != null && turns.CurrentPhase == TurnPhase.Clash) return false;
            var rig = CombatCameraRig3D.Instance;
            if (rig == null) return true;
            rig.ReturnHome();
            return rig.IsHome;
        }

        /// <summary>Plays the cutscene; ignored while one is already playing.</summary>
        public void Play()
        {
            if (Playing || !isActiveAndEnabled) return;
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            Playing = true;
            Time.timeScale = 0f;
            bool still = GameSettings.ReduceMotion;
            BuildUI();
            Started?.Invoke();

            var cam = Camera.main;
            Vector3 homePos = default;
            Quaternion homeRot = default;
            float homeFov = 60f;
            if (cam != null)
            {
                // A view change still gliding would fight the cutscene: finish it first
                cam.transform.DOComplete();
                cam.DOComplete();
                homePos = cam.transform.position;
                homeRot = cam.transform.rotation;
                homeFov = cam.fieldOfView;
            }

            // The room darkens
            Shade(true);
            yield return new WaitForSecondsRealtime(fadeTime);

            // The roar: the boss shakes, a red flash, the light strobes, the camera trembles
            Flash(0.55f);
            if (!still)
            {
                StartCoroutine(Strobe(roar + 0.35f));
                if (boss != null) boss.DOShakePosition(roar, bossShake, 25, 90f, false, true).SetUpdate(true).SetId(this);
                if (cam != null) cam.transform.DOShakeRotation(roar, roarShake, 18, 90f, true).SetUpdate(true).SetId(this);
            }
            yield return new WaitForSecondsRealtime(roar);

            // The slam
            if (cam != null)
            {
                cam.transform.DOKill(true);
                cam.transform.SetPositionAndRotation(homePos, homeRot);
            }
            if (!still)
            {
                Flash(0.3f);
                ShakeCamera(cam, homeFov);
                JumpProps();
            }
            yield return new WaitForSecondsRealtime(settle);

            // The shade lifts, the room stays red, the fight goes on
            Shade(false);
            LightRed();
            yield return new WaitForSecondsRealtime(fadeTime);
            if (cam != null)
            {
                cam.transform.DOKill(true);
                cam.DOKill(true);
                cam.transform.SetPositionAndRotation(homePos, homeRot);
                cam.fieldOfView = homeFov;
            }

            // The pause menu keeps its own time scale and puts it back when it closes
            while (PauseMenu.IsPaused) yield return null;
            Time.timeScale = 1f;
            Playing = false;
        }

        // The main light flickers between a hard red flash and near dark
        private IEnumerator Strobe(float seconds)
        {
            if (sceneLight == null) yield break;
            redLit = false;
            sceneLight.color = redColor;
            float end = Time.realtimeSinceStartup + seconds;
            bool on = true;
            while (Time.realtimeSinceStartup < end)
            {
                sceneLight.intensity = on ? strobeIntensity * Random.Range(0.7f, 1f) : redIntensity * 0.15f;
                yield return new WaitForSecondsRealtime(on ? Random.Range(0.05f, 0.12f) : Random.Range(0.04f, 0.1f));
                on = !on;
            }
            sceneLight.intensity = strobeIntensity;
        }

        // The light and the ambient settle on red; Update keeps the light breathing from then on
        private void LightRed()
        {
            if (sceneLight != null)
            {
                sceneLight.DOKill();
                sceneLight.DOColor(redColor, fadeTime).SetUpdate(true);
                sceneLight.DOIntensity(redIntensity, fadeTime * 2f).SetUpdate(true).OnComplete(() => redLit = true);
            }
            Color from = RenderSettings.ambientLight;
            DOTween.To(() => 0f, t => RenderSettings.ambientLight = Color.Lerp(from, redAmbient, t), 1f, fadeTime * 2f).SetUpdate(true).SetId(this);
        }

        // ---------------------------------------------------------------- slam

        private void ShakeCamera(Camera cam, float fov)
        {
            if (cam == null) return;
            if (cameraShake > 0f)
                cam.transform.DOShakePosition(shakeTime, cameraShake, 30, 90f, false, true).SetUpdate(true).SetId(this);
            if (slamTilt > 0f)
                cam.transform.DOShakeRotation(shakeTime, slamTilt, 25, 90f, true).SetUpdate(true).SetId(this);
            if (fovKick > 0f)
            {
                DOTween.Sequence().SetId(this).SetTarget(cam).SetUpdate(true)
                    .Append(cam.DOFieldOfView(fov - fovKick, 0.06f).SetEase(Ease.OutQuad))
                    .Append(cam.DOFieldOfView(fov, 0.4f).SetEase(Ease.OutQuad));
            }
        }

        private void JumpProps()
        {
            foreach (var group in Groups())
            {
                float wait = Random.Range(0f, stagger);
                float height = Random.Range(jumpHeight.x, jumpHeight.y);
                foreach (var t in group) Jump(t, wait, height);
            }
        }

        // Up, a tilt, down, and a small bounce on landing; it ends exactly where it started
        private void Jump(Transform t, float wait, float height)
        {
            Vector3 rest = t.position;
            var tilt = new Vector3(Random.Range(-maxTilt, maxTilt), Random.Range(-maxTilt, maxTilt) * 0.5f, Random.Range(-maxTilt, maxTilt));
            DOTween.Sequence().SetId(this).SetLink(t.gameObject).SetUpdate(true)
                .AppendInterval(wait)
                .Append(t.DOJump(rest, height, 1, jumpTime))
                .Join(t.DOPunchRotation(tilt, jumpTime, 3, 0.5f))
                .Append(t.DOJump(rest, height * 0.2f, 1, jumpTime * 0.35f))
                .OnKill(() => { if (t != null) t.position = rest; });
        }

        // Props close together (flat distance under groupRadius) in one group
        private List<List<Transform>> Groups()
        {
            var groups = new List<List<Transform>>();
            var anchors = new List<Vector3>();
            foreach (var t in props)
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                var p = t.position;
                int found = -1;
                for (int i = 0; i < anchors.Count && found < 0; i++)
                {
                    var d = anchors[i] - p;
                    d.y = 0f;
                    if (d.magnitude <= groupRadius) found = i;
                }
                if (found < 0)
                {
                    anchors.Add(p);
                    groups.Add(new List<Transform>());
                    found = groups.Count - 1;
                }
                groups[found].Add(t);
            }
            return groups;
        }

        // ---------------------------------------------------------------- screen

        private void BuildUI()
        {
            if (canvas != null) return;
            canvas = UiFactory.CreateOverlayCanvas("BossPhaseCutsceneCanvas", SortingOrder, transform);

            shade = UiFactory.CreateImage("Shade", canvas.transform, new Color(0f, 0f, 0f, 0f));
            UiFactory.Stretch(shade.rectTransform, 0f);
            flash = UiFactory.CreateImage("Flash", canvas.transform, new Color(0.45f, 0f, 0.02f, 0f));
            UiFactory.Stretch(flash.rectTransform, 0f);
        }

        private void Shade(bool show)
        {
            shade.DOKill();
            shade.DOFade(show ? darken : 0f, fadeTime).SetUpdate(true);
        }

        private void Flash(float peak)
        {
            flash.DOKill();
            DOTween.Sequence().SetUpdate(true).SetTarget(flash)
                .Append(flash.DOFade(peak, 0.06f))
                .Append(flash.DOFade(0f, 0.5f).SetEase(Ease.OutQuad));
        }
    }
}
