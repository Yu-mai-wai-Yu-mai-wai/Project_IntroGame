using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TawanOS.CardEngine;
using TawanOS.Settings;
using UnityEngine;

namespace TawanOS.VFX
{
    /// <summary>
    /// The boss slams the table when it enters its next phase (CombatManager.OnEnemyPhaseChanged): the camera shakes
    /// and everything on the table jumps up and lands again, tilting as it goes. The board slots are among the props,
    /// so the cards in play jump with them. Props that stand close together (a candle and its flame and light, the
    /// cards of a deck) jump as one. Placed in CombatTestScene by Tools/TawanOS/Combat Scene/Add Table Slam Fx, which
    /// also fills the props; Reduce Motion turns it off.
    /// </summary>
    public class TableSlamFx : MonoBehaviour
    {
        [Tooltip("ของบนโต๊ะที่เด้งตอนบอสเปลี่ยนเฟส (เทียน ลูกปัด กองการ์ด ช่องวางการ์ด) Add Table Slam Fx เติมให้")]
        public List<Transform> props = new List<Transform>();

        [Header("Timing")]
        [Tooltip("วินาทีหลังบอสคืนร่างก่อนทุบโต๊ะ")]
        [Min(0f)] public float delay = 0.15f;
        [Tooltip("ของแต่ละกลุ่มเริ่มเด้งช้ากว่ากันได้สุ่มไม่เกินนี้ (วินาที)")]
        [Min(0f)] public float stagger = 0.06f;

        [Header("Props")]
        [Tooltip("ความสูงที่ของเด้งขึ้น ต่ำสุด / สูงสุด (สุ่มต่อกลุ่ม)")]
        public Vector2 jumpHeight = new Vector2(0.6f, 1.2f);
        [Min(0.05f)] public float jumpTime = 0.55f;
        [Tooltip("ของเอียงได้ไม่เกินกี่องศาตอนลอย")]
        [Min(0f)] public float maxTilt = 20f;
        [Tooltip("ของที่วางห่างกันไม่เกินนี้ (แนวราบ) เด้งพร้อมกันและสูงเท่ากัน")]
        [Min(0f)] public float groupRadius = 0.2f;

        [Header("Camera")]
        [Min(0f)] public float cameraShake = 0.8f;
        [Min(0f)] public float shakeTime = 0.6f;
        [Tooltip("กล้องซูมเข้ากี่องศา (field of view) ตอนทุบ แล้วคืน")]
        [Min(0f)] public float fovKick = 6f;

        private CombatManager combat;
        private bool slamming;

        private void Start()
        {
            combat = CombatManager.Instance;
            if (combat != null) combat.OnEnemyPhaseChanged += HandlePhase;
        }

        private void OnDestroy()
        {
            if (combat != null) combat.OnEnemyPhaseChanged -= HandlePhase;
            DOTween.Kill(this);
        }

        private void HandlePhase(int phase, EnemyProfileSO profile) => Slam();

        /// <summary>The slam: camera shake and the props jump. One at a time; a second call while one plays is ignored.</summary>
        public void Slam()
        {
            if (slamming || GameSettings.ReduceMotion || !isActiveAndEnabled) return;
            StartCoroutine(SlamRoutine());
        }

        private IEnumerator SlamRoutine()
        {
            slamming = true;
            if (delay > 0f) yield return new WaitForSeconds(delay);

            ShakeCamera();

            float longest = 0f;
            foreach (var group in Groups())
            {
                float wait = Random.Range(0f, stagger);
                float height = Random.Range(jumpHeight.x, jumpHeight.y);
                foreach (var t in group)
                {
                    Jump(t, wait, height);
                }
                longest = Mathf.Max(longest, wait);
            }

            yield return new WaitForSeconds(longest + jumpTime + 0.25f);
            slamming = false;
        }

        // Up, a tilt, down, and a small bounce on landing; it ends exactly where it started
        private void Jump(Transform t, float wait, float height)
        {
            Vector3 rest = t.position;
            var tilt = new Vector3(Random.Range(-maxTilt, maxTilt), Random.Range(-maxTilt, maxTilt) * 0.5f, Random.Range(-maxTilt, maxTilt));
            DOTween.Sequence().SetId(this).SetLink(t.gameObject)
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

        private void ShakeCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // Not while the camera is gliding to another view: the shake would end where it started
            if (cameraShake > 0f && !DOTween.IsTweening(cam.transform))
                cam.transform.DOShakePosition(shakeTime, cameraShake, 30, 90f, false, true);
            if (fovKick > 0f && !DOTween.IsTweening(cam))
            {
                float fov = cam.fieldOfView;
                DOTween.Sequence().SetTarget(cam)
                    .Append(cam.DOFieldOfView(fov - fovKick, 0.06f).SetEase(Ease.OutQuad))
                    .Append(cam.DOFieldOfView(fov, 0.4f).SetEase(Ease.OutQuad));
            }
        }
    }
}
