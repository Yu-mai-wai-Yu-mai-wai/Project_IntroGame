using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TawanOS.VFX
{
    /// <summary>
    /// Eases the fight's camera vignette off while a hand card is raised, so a card at the edge of the screen is not
    /// darkened and its ability text reads (Game Designer, 24 Oct), and back once the hover ends. Works on the global
    /// Volume's runtime copy of its profile, never on the profile asset.
    /// </summary>
    public static class HoverVignetteEase
    {
        // Share of the scene's vignette left while a card is raised
        public static float RaisedShare = 0.25f;
        // Change per second, in shares of the scene's vignette
        public static float Speed = 6f;

        private static Volume volume;
        private static Vignette vignette;
        private static float sceneIntensity;
        private static float share = 1f;
        private static float nextSearch;

        public static void Tick(bool cardRaised)
        {
            if (vignette == null || volume == null)
            {
                // A scene with no vignette: look again now and then, not every frame (a time left from an earlier
                // play session in the editor is ignored)
                if (Time.unscaledTime < nextSearch && nextSearch - Time.unscaledTime <= 1f) return;
                nextSearch = Time.unscaledTime + 1f;
                if (!Find()) return;
            }
            float target = cardRaised ? RaisedShare : 1f;
            if (Mathf.Approximately(share, target)) return;
            share = Mathf.MoveTowards(share, target, Time.unscaledDeltaTime * Speed);
            vignette.intensity.value = sceneIntensity * share;
        }

        private static bool Find()
        {
            foreach (var v in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (!v.isGlobal || v.sharedProfile == null || !v.sharedProfile.Has<Vignette>()) continue;
                volume = v;
                // .profile makes this Volume its own copy of the profile, so the asset is never changed
                if (!v.profile.TryGet(out vignette)) return false;
                sceneIntensity = vignette.intensity.value;
                share = 1f;
                return true;
            }
            return false;
        }
    }
}
