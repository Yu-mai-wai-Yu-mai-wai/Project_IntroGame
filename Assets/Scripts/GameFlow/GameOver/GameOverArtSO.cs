using UnityEngine;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// The pictures the Game Over screen shows, loaded from Resources/GameOverArt (built in code, so it cannot hold
    /// a scene reference). Set up by Tools/TawanOS/Game Over/Set Up Defeat Art.
    /// </summary>
    [CreateAssetMenu(fileName = "GameOverArt", menuName = "TawanOS/Game Over Art")]
    public class GameOverArtSO : ScriptableObject
    {
        public const string ResourceName = "GameOverArt";

        [Tooltip("Full-screen picture behind the ขวัญหลุด screen (Art/CutScenes/Lost).")]
        public Sprite defeatArt;

        public static GameOverArtSO Load() => Resources.Load<GameOverArtSO>(ResourceName);
    }
}
