using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class GoToMainMenu : MonoBehaviour
{
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            Time.timeScale = 1f; // in case the game was paused
            SceneManager.LoadScene(mainMenuSceneName);
        });
    }
}