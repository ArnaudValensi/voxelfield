using Swihoni.Sessions;
using Swihoni.Sessions.Config;
using Swihoni.Sessions.Interfaces;
using UnityEngine;
using UnityEngine.UI;
using Voxelfield.Integration;

namespace Voxelfield.Interface
{
    public class MainMenuInterface : SessionInterfaceBehavior
    {
        private const string InMainMenu = "In Main Menu", Searching = "Searching for a Game";

        public override void Render(in SessionContext context) { }

        protected override void OnSetInterfaceActive(bool isActive)
        {
            if (isActive) VoxelfieldActivityManager.TrySetActivity(InMainMenu);
        }

        public async void OnPlayButton(Button button)
        {
            GameObject progress = button.GetComponentInChildren<Animator>(true).gameObject;
            progress.SetActive(true);
            button.interactable = false;
            try
            {
                VoxelfieldActivityManager.TrySetActivity(Searching);
                await GameLiftClientManager.QuickPlayAsync();
            }
            finally
            {
                if (button) button.interactable = true;
                if (progress) progress.SetActive(false);
                VoxelfieldActivityManager.TrySetActivity(InMainMenu);
            }
        }

        public void OnSettingsButton() => DefaultConfig.OpenSettings();

        public void OnQuitButton() => Application.Quit();
    }
}