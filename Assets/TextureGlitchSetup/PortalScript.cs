using System;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class Portal : MonoBehaviour
{
    public GameObject videoCanvas;
    public VideoPlayer videoPlayer;
    public string nextScene;

    private bool triggered = false;

    private void Start()
    {
        videoPlayer.loopPointReached += VideoFinished;
        videoCanvas.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered)
            return;

        if (!other.CompareTag("Player"))
            return;

        triggered = true;

        // Show the video
        videoCanvas.SetActive(true);

        // Play the glitch
        videoPlayer.Play();
    }

    private void VideoFinished(VideoPlayer player)
    {
        SceneManager.LoadScene(nextScene);
    }
}