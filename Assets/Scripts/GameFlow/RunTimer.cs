using TawanOS.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TawanOS.GameFlow
{
    /// <summary>
    /// Counts the time played in a run (RunState.PlaySeconds). It starts at 0 with New Game and counts while a run is
    /// going (RunState.IsPersistent), so it stops by itself when the run ends in defeat or victory (RunState.EndRun).
    /// The pause menu, the main menu and a game window in the background do not count. The time is in the save, so
    /// Continue carries on from it. The Game Over and Victory screens show it.
    /// </summary>
    public class RunTimer : MonoBehaviour
    {
        private const float SaveEverySeconds = 30f;

        private static RunTimer instance;
        private float sinceSave;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null) return;
            var go = new GameObject("RunTimer");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RunTimer>();
        }

        private void Update()
        {
            var run = RunState.Current;
            if (!run.IsPersistent || PauseMenu.IsPaused || !Application.isFocused) return;
            if (SceneManager.GetActiveScene().name == GameFlowManager.MainMenuSceneName) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f); // a long hitch (a scene loading) counts as one second at most
            run.AddPlayTime(dt);
            sinceSave += dt;
            if (sinceSave >= SaveEverySeconds) Flush();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        private void OnApplicationQuit() => Flush();

        private void Flush()
        {
            sinceSave = 0f;
            RunState.Current.SavePlayTime();
        }
    }
}
