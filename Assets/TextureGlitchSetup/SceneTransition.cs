using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public string sceneToLoad;

    private bool loading = false;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        videoPlayer.loopPointReached += OnVideoFinished;
    }

    public void ChangeScene(string sceneName)
    {
        if (loading)
            return;

        loading = true;
        sceneToLoad = sceneName;

        videoPlayer.Stop();
        videoPlayer.Play();
    }

    private void OnVideoFinished(VideoPlayer player)
    {
        SceneManager.LoadScene(sceneToLoad);
    }
}