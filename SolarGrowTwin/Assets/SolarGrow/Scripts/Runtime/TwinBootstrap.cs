using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Pressing Play in a scene that has no TwinController spawns the default twin (houses A and B,
    /// simulated data). Delete this file to switch the behaviour off.
    /// </summary>
    public static class TwinBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SpawnIfMissing()
        {
            if (UnityEngine.Object.FindFirstObjectByType<TwinController>() != null) return;
            TwinSceneFactory.CreateTwin(TwinSceneFactory.LoadDefaultConfig(), new string[] { "A", "B" });
            Debug.Log("SolarGrow: the scene had no TwinController, so TwinBootstrap spawned the default twin.");
        }
    }
}
