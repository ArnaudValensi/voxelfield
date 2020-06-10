using System;
using Steamworks;
using UnityEngine;

namespace Swihoni.Util
{
    [DisallowMultipleComponent]
    public class SteamManager : MonoBehaviour
    {
#if !UNITY_EDITOR
        private void Awake()
        {
            try
            {
                SteamClient.Init(480);
            }
            catch (Exception)
            {
                Debug.LogWarning("Failed to initialize steam client");
            }
        }

        private void OnApplicationQuit() => SteamClient.Shutdown();
#endif
    }
}