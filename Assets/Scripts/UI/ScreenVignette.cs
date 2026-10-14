using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TawanOS.UI
{
    /// <summary>
    /// A dark vignette over the whole screen, UI included, in every scene (Game Designer, 12 Oct). It is an overlay
    /// image, not a camera effect, so it also darkens the edges of the screens that are all UI (หมอกดำ, rewards, เมรุ,
    /// วัด, the story). Its falloff is the same formula URP's Vignette uses: black, smoothness 0.45 like the combat
    /// scene, intensity 0.3 (lighter than the fight's 0.45, which was too dark over UI). MainMenu and CombatTestScene keep the
    /// camera vignette they already have and get no overlay. A scene can have its own, heavier one (SceneIntensity: the
    /// เมรุ, Game Designer 14 Oct). Created once before the first scene; lives across scenes.
    /// </summary>
    public class ScreenVignette : MonoBehaviour
    {
        // Above every game canvas and the card book (500), under the scene fade (SceneTransition, 1000)
        private const int SortingOrder = 950;

        // Scenes with their own camera vignette (Volume profiles), left as they are
        private static readonly string[] OwnVignette = { "MainMenu", "CombatTestScene" };

        // 0.45 (the fight's value) read too dark over the UI: lighter (Game Designer, 12 Oct)
        public static float Intensity = 0.3f;
        public static float Smoothness = 0.45f;
        public static Color Tint = Color.black;

        // Scenes darker at the edges than the rest. The เมรุ's open stonework is lit and pale, so the usual 0.3 barely
        // shows there; this makes it read like the dark forest round the กองของเซ่น (Game Designer, 14 Oct). 0.5 hid
        // the buttons at the screen's corners
        private static readonly System.Collections.Generic.Dictionary<string, float> SceneIntensity =
            new System.Collections.Generic.Dictionary<string, float> { { "MeruScene", 0.38f } };

        private static ScreenVignette instance;
        private GameObject overlay;
        private RawImage image;
        private float builtIntensity = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("ScreenVignette");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ScreenVignette>();
        }

        private void Awake()
        {
            var canvas = UiFactory.CreateOverlayCanvas("VignetteCanvas", SortingOrder, transform);
            var group = canvas.GetComponent<CanvasGroup>();
            if (group != null) { group.blocksRaycasts = false; group.interactable = false; }

            image = new GameObject("Vignette", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(canvas.transform, false);
            UiFactory.Stretch(image.rectTransform, 0f);
            image.color = Tint;
            image.raycastTarget = false;
            overlay = canvas.gameObject;

            SceneManager.sceneLoaded += HandleSceneLoaded;
            Apply(SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (instance == this) instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Apply(scene);

        private void Apply(Scene scene)
        {
            if (overlay != null) overlay.SetActive(System.Array.IndexOf(OwnVignette, scene.name) < 0);
            float intensity = SceneIntensity.TryGetValue(scene.name, out var own) ? own : Intensity;
            if (image != null && !Mathf.Approximately(intensity, builtIntensity))
            {
                if (image.texture != null) Destroy(image.texture);
                image.texture = BuildTexture(intensity, Smoothness);
                builtIntensity = intensity;
            }
        }

        // URP Vignette (not rounded): d = |uv - 0.5| * intensity * 3, factor = (1 - d.d) ^ (smoothness * 5),
        // and the screen is blended toward the tint by 1 - factor. The texture holds that 1 - factor as alpha and
        // is stretched over the screen, so the shape follows the screen's proportions as URP's does.
        private static Texture2D BuildTexture(float intensity, float smoothness)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "ScreenVignette"
            };
            var pixels = new Color32[size * size];
            float k = intensity * 3f;
            float power = Mathf.Max(0.01f, smoothness * 5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs((x + 0.5f) / size - 0.5f) * k;
                    float dy = Mathf.Abs((y + 0.5f) / size - 0.5f) * k;
                    float factor = Mathf.Pow(Mathf.Clamp01(1f - (dx * dx + dy * dy)), power);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt((1f - factor) * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
