using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TawanOS.CardEngine;
using TawanOS.Settings;
using TawanOS.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TawanOS.VFX
{
    /// <summary>
    /// The boss's comeback, played when it enters its next phase (CombatManager.OnEnemyPhaseChanged) as a short
    /// cutscene: the fight freezes (time scale 0, no input), black bars close in, the camera pushes in on the boss,
    /// which shakes and roars under its new name, then the camera pulls back and the boss slams the table: the camera
    /// shakes and everything on the table jumps up and lands again. Meanwhile its khwan bar (CombatStatusHUD) empties
    /// and fills up again. The scene's main light strobes red through the roar and the slam, and the room stays lit red
    /// (the light and the ambient, breathing slowly) for the rest of the fight. The props include the board slots, so the cards in play jump with them; props standing
    /// together (a candle and its flame) jump as one. Everything runs in real time. Reduce Motion keeps the bars and
    /// the title only. Placed in CombatTestScene by Tools/TawanOS/Combat Scene/Add Boss Phase Cutscene, which also
    /// fills the boss and the props.
    /// </summary>
    public class BossPhaseCutscene : MonoBehaviour
    {
        /// <summary>True while the cutscene plays: the fight takes no input.</summary>
        public static bool Playing { get; private set; }

        private const int SortingOrder = 40; // over the table, under the HUDs (50): the khwan bar shows over the black bars

        [Header("Boss")]
        [Tooltip("ตัวบอสในฉาก กล้องดันเข้าไปหาตัวนี้ และตัวนี้สั่นตอนคำราม Add Boss Phase Cutscene เติมให้")]
        public Transform boss;
        [Tooltip("กล้องเข้าไปใกล้บอสแค่ไหน (หน่วยโลก จากกลางตัวบอส)")]
        [Min(0.5f)] public float closeDistance = 7f;
        [Tooltip("ข้อความใหญ่กลางจอ {0} = ชื่อร่างใหม่ของบอส")]
        [TextArea] public string title = "{0}\nคืนร่าง!";
        public Color titleColor = new Color(0.95f, 0.22f, 0.16f);
        [Min(0f)] public float bossShake = 0.25f;

        [Header("Timing (seconds, real time)")]
        [Min(0.05f)] public float barsTime = 0.35f;
        [Tooltip("กล้องดันเข้าหาบอส")]
        [Min(0.05f)] public float pushIn = 0.9f;
        [Tooltip("บอสคำราม (ข้อความขึ้น)")]
        [Min(0.05f)] public float roar = 1.4f;
        [Tooltip("กล้องดึงกลับมาที่โต๊ะ แล้วทุบ")]
        [Min(0.05f)] public float pullBack = 0.22f;
        [Tooltip("ของบนโต๊ะลอยแล้วตกลง ก่อนแถบดำออก")]
        [Min(0.05f)] public float settle = 0.9f;

        [Header("Black bars")]
        [Min(0f)] public float barHeight = 120f;
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
        [Min(0f)] public float cameraShake = 0.8f;
        [Min(0f)] public float shakeTime = 0.6f;
        [Tooltip("กล้องซูมเข้ากี่องศา (field of view) ตอนทุบ แล้วคืน")]
        [Min(0f)] public float fovKick = 6f;

        private CombatManager combat;
        private Canvas canvas;
        private RectTransform topBar, bottomBar;
        private Image shade, flash;
        private TextMeshProUGUI titleText;
        private bool redLit; // the room is lit red: Update keeps the light breathing

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
            if (!redLit || sceneLight == null || GameSettings.ReduceMotion) return;
            sceneLight.intensity = redIntensity * (1f + breathe * Mathf.Sin(Time.unscaledTime * 1.7f));
        }

        private void HandlePhase(int phase, EnemyProfileSO profile)
        {
            // The player fell in the same blow: the defeat screen comes instead
            if (combat != null && combat.State.playerKhwan <= 0) return;
            Play(profile != null ? profile.enemyName : "");
        }

        /// <summary>Plays the cutscene; ignored while one is already playing.</summary>
        public void Play(string bossName)
        {
            if (Playing || !isActiveAndEnabled) return;
            StartCoroutine(PlayRoutine(bossName));
        }

        private IEnumerator PlayRoutine(string bossName)
        {
            Playing = true;
            Time.timeScale = 0f;
            bool still = GameSettings.ReduceMotion;
            BuildUI();

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
            bool moveCamera = !still && cam != null && boss != null;

            // Bars in, the room darkens, the camera pushes in on the boss
            Bars(true);
            if (moveCamera)
            {
                Vector3 look = BossLookPoint();
                Vector3 away = homePos - look;
                Vector3 target = look + away.normalized * Mathf.Min(closeDistance, away.magnitude);
                cam.transform.DOMove(target, pushIn).SetEase(Ease.InOutCubic).SetUpdate(true).SetId(this);
                cam.transform.DORotateQuaternion(Quaternion.LookRotation(look - target), pushIn).SetEase(Ease.InOutCubic).SetUpdate(true).SetId(this);
            }
            yield return new WaitForSecondsRealtime(pushIn);

            // The roar: the boss shakes, a red flash, its new name
            ShowTitle(string.Format(title, bossName));
            Flash(0.55f);
            if (!still) StartCoroutine(Strobe(roar + pullBack + 0.35f));
            if (!still && boss != null)
                boss.DOShakePosition(roar, bossShake, 25, 90f, false, true).SetUpdate(true).SetId(this);
            if (moveCamera)
                cam.transform.DOShakeRotation(roar, 1.2f, 18, 90f, true).SetUpdate(true).SetId(this);
            yield return new WaitForSecondsRealtime(roar);

            // Back to the table, fast, and the slam
            if (moveCamera)
            {
                cam.transform.DOKill(true);
                cam.transform.DOMove(homePos, pullBack).SetEase(Ease.InQuad).SetUpdate(true).SetId(this);
                cam.transform.DORotateQuaternion(homeRot, pullBack).SetEase(Ease.InQuad).SetUpdate(true).SetId(this);
            }
            yield return new WaitForSecondsRealtime(pullBack);
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

            // Bars out, the room stays red, the fight goes on
            Bars(false);
            LightRed();
            titleText.DOKill();
            titleText.DOFade(0f, barsTime).SetUpdate(true);
            yield return new WaitForSecondsRealtime(barsTime);
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
                sceneLight.DOColor(redColor, barsTime).SetUpdate(true);
                sceneLight.DOIntensity(redIntensity, barsTime * 2f).SetUpdate(true).OnComplete(() => redLit = true);
            }
            Color from = RenderSettings.ambientLight;
            DOTween.To(() => 0f, t => RenderSettings.ambientLight = Color.Lerp(from, redAmbient, t), 1f, barsTime * 2f).SetUpdate(true).SetId(this);
        }

        // A little above the middle of the boss's body: its face and chest
        private Vector3 BossLookPoint()
        {
            var renderers = boss.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return boss.position;
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.center + Vector3.up * bounds.extents.y * 0.35f;
        }

        // ---------------------------------------------------------------- slam

        private void ShakeCamera(Camera cam, float fov)
        {
            if (cam == null) return;
            if (cameraShake > 0f)
                cam.transform.DOShakePosition(shakeTime, cameraShake, 30, 90f, false, true).SetUpdate(true).SetId(this);
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
            var theme = UIThemeSO.Current;
            canvas = UiFactory.CreateOverlayCanvas("BossPhaseCutsceneCanvas", SortingOrder, transform);

            shade = UiFactory.CreateImage("Shade", canvas.transform, new Color(0f, 0f, 0f, 0f));
            UiFactory.Stretch(shade.rectTransform, 0f);
            flash = UiFactory.CreateImage("Flash", canvas.transform, new Color(0.45f, 0f, 0.02f, 0f));
            UiFactory.Stretch(flash.rectTransform, 0f);

            topBar = Bar("TopBar", 1f);
            bottomBar = Bar("BottomBar", 0f);

            var font = theme != null ? (theme.titleFont != null ? theme.titleFont : theme.bodyFont) : null;
            titleText = UiFactory.CreateText(canvas.transform, "Title", "", 96f, titleColor, TextAlignmentOptions.Center, font);
            titleText.fontStyle = FontStyles.Bold;
            titleText.outlineWidth = 0.2f;
            titleText.outlineColor = new Color32(10, 0, 0, 255);
            var rt = titleText.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(0f, 320f);
            rt.anchoredPosition = new Vector2(0f, -60f);
            titleText.alpha = 0f;
        }

        private RectTransform Bar(string name, float y)
        {
            var bar = UiFactory.CreateImage(name, canvas.transform, Color.black).rectTransform;
            bar.anchorMin = new Vector2(0f, y);
            bar.anchorMax = new Vector2(1f, y);
            bar.pivot = new Vector2(0.5f, y);
            bar.sizeDelta = new Vector2(0f, 0f);
            return bar;
        }

        private void Bars(bool show)
        {
            float h = show ? barHeight : 0f;
            topBar.DOKill();
            bottomBar.DOKill();
            shade.DOKill();
            topBar.DOSizeDelta(new Vector2(0f, h), barsTime).SetEase(Ease.OutCubic).SetUpdate(true);
            bottomBar.DOSizeDelta(new Vector2(0f, h), barsTime).SetEase(Ease.OutCubic).SetUpdate(true);
            shade.DOFade(show ? darken : 0f, barsTime).SetUpdate(true);
        }

        private void ShowTitle(string text)
        {
            titleText.text = text;
            titleText.DOKill();
            titleText.transform.DOKill(true);
            titleText.alpha = 0f;
            titleText.transform.localScale = Vector3.one * 1.4f;
            titleText.DOFade(1f, 0.25f).SetUpdate(true);
            titleText.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
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
