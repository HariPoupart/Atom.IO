using UnityEngine;

namespace AtomIO.Core
{
    /// <summary>
    /// Frame-rate settings applied once at startup. Android defaults to 30 fps, which makes fast
    /// orbiting electrons look choppy, so ask for 60 (the device's vsync still caps it).
    /// </summary>
    public static class PerformanceSettings
    {
        public const int TargetFps = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            Application.targetFrameRate = TargetFps;
            if (Application.isMobilePlatform)
            {
                QualitySettings.vSyncCount = 0; // on mobile, targetFrameRate controls the rate
            }
        }
    }
}
