using System.Threading.Tasks;
using Swihoni.Sessions;
using Swihoni.Sessions.Config;
using Swihoni.Sessions.Interfaces;
using UnityEngine;
using UnityEngine.UI;
using Voxelfield.Integration;
using Voxelfield.Session;

namespace Voxelfield.Interface
{
    public class MainMenuInterface : SessionInterfaceBehavior
    {
        private const string InMainMenu = "In Main Menu", Searching = "Searching for a Game";

        public override void Render(in SessionContext context) { }

        protected override void OnSetInterfaceActive(bool isActive)
        {
            if (isActive) DiscordManager.SetActivity(InMainMenu);
        }

        public async void OnPlayButton(Button button)
        {
            GameObject progress = button.GetComponentInChildren<Animator>(true).gameObject;
            progress.SetActive(true);
            button.interactable = false;
#if VOXELFIELD_RELEASE_CLIENT
            try
            {
                DiscordManager.SetActivity(Searching);
                await GameLiftClientManager.QuickPlayAsync();
            }
            finally
            {
                if (button) button.interactable = true;
                if (progress) progress.SetActive(false);
                DiscordManager.SetActivity(InMainMenu);
            }
#else
            await Task.Delay(500);
            if (progress) progress.SetActive(false);
            if (button) button.interactable = true;
            SessionManager.StartHost();
#endif
        }

        public void OnSettingsButton() => DefaultConfig.OpenSettings();

        public void OnQuitButton() => Application.Quit();
    }
}