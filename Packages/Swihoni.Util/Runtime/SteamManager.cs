using System;
using Steamworks;
using UnityEngine;

namespace Swihoni.Util
{
    public class SteamManager : MonoBehaviour
    {
        private void Awake()
        {
            try
            {
                SteamClient.Init(480);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to initialize steam client");
            }
        }

        private void OnApplicationQuit() => SteamClient.Shutdown();
    }
}