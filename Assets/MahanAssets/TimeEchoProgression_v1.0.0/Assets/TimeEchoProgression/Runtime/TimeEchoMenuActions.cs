using UnityEngine;

namespace TimeEcho.Flow
{
    public sealed class TimeEchoMenuActions : MonoBehaviour
    {
        public void NewGame() => TimeEchoFlowManager.Instance?.StartNewGame();
        public void ContinueGame() => TimeEchoFlowManager.Instance?.ContinueGame();
        public void RestartLevel() => TimeEchoFlowManager.Instance?.RestartCurrentLevel();
        public void MainMenu() => TimeEchoFlowManager.Instance?.RequestReturnToMenu();
        public void QuitGame() => TimeEchoFlowManager.Instance?.QuitGame();
        public void ClearSave() => TimeEchoFlowManager.Instance?.ClearSavedProgress();
        public void LoadScene(string sceneName) => TimeEchoFlowManager.Instance?.LoadCustomScene(sceneName);
    }
}
