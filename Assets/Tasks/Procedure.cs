using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class Procedure : MonoBehaviour
{
    // to set in inspector

    public Text infoDisplay;
    public AudioSource sessionDone;
    public AudioSource backgroundAudio;
    public VideoPlayer videoPlayer;
    public Image restingImage;
    public GameObject setupUI;


    // overrides

    void Awake()
    {
        setupUI.SetActive(true);
    }
    
    void Start()
    {
        _hrClient = GetComponent<HRClient>();
        _log = GetComponent<Log>();

        _videoSets = GetComponent<VideoSets>();

        _gazePoint = FindObjectOfType<GazePoint>();

        _gazeClient = FindObjectOfType<GazeClient>();
        _gazeClient.Start += OnGazeClientStart;
        _gazeClient.Sample += OnGazeClientSample;

        videoPlayer.loopPointReached += OnVideoStopped;
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.Escape))
        {
            if (videoPlayer.isPlaying)
            {
                InterruptVideo();
            }
        }
        if (Input.GetKey(KeyCode.Space))
        {
            if (restingImage.gameObject.activeSelf)
            {
                restingImage.gameObject.SetActive(false);
                NextVideo();
            }
        }
    }

    // methods

    public void StartSet1()
    {
        StartSet(0);
    }

    public void StartSet2()
    {
        StartSet(1);
    }

    public void StartSet3()
    {
        StartSet(2);
    }

    public void StartSet4()
    {
        StartSet(3);
    }

    public void StartSet5()
    {
        StartSet(4);
    }

    public void StartSet6()
    {
        StartSet(5);
    }

    public void Finish()
    {
        if (_gazeClient.IsTracking)
        {
            _gazeClient.ToggleTracking();
        }

        _hrClient.Stop();
        _log.Close();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }


    // internal

    HRClient _hrClient;
    Log _log;

    GazePoint _gazePoint;
    GazeClient _gazeClient;

    VideoSets _videoSets;

    void HideUI()
    {
        setupUI.SetActive(false);
        Cursor.visible = false;
    }

    void ShowUI()
    {
        setupUI.SetActive(true);
        Cursor.visible = true;
    }

    void StartSet(int setID)
    {
        HideUI();

        _videoSets.SelectSet(setID);
        _hrClient.StartSet(setID + 1);

        //backgroundAudio.Play();

        infoDisplay.text = "starting...";

        Invoke(nameof(WaitForNextVideo), 1f);
    }

    void WaitForNextVideo()
    {
        infoDisplay.text = "";
        restingImage.gameObject.SetActive(true);
    }

    void NextVideo()
    {
        restingImage.gameObject.SetActive(false);

        var video = _videoSets.Next();

        if (video != null)
        {
            videoPlayer.clip = video;
            videoPlayer.gameObject.SetActive(true);

            _hrClient.StartVideo(video.name.Split('.')[0].Last());

            videoPlayer.Play();
        }
        else
        {
            throw new Exception("Internal error: no more videos");
        }
    }

    void InterruptVideo()
    {
        _hrClient.InterruptVideo();

        videoPlayer.Stop();

        OnVideoStopped(videoPlayer);
    }

    void OnGazeClientStart(object sender, EventArgs e)
    {
        var buttons = FindObjectsOfType(typeof(Button)).Where(btn => (btn as Button).CompareTag("only-gaze-active"));
        foreach (var btn in buttons)
        {
            (btn as Button).interactable = true;
        }
    }

    void OnGazeClientSample(object sender, EventArgs e)
    {
        _gazePoint.MoveTo(_gazeClient.LastSample);
    }

    void OnVideoStopped(VideoPlayer player)
    {
        _hrClient.StopVideo();

        videoPlayer.gameObject.SetActive(false);

        if (_videoSets.HasMoreVideos)
        {
            WaitForNextVideo();
        }
        else
        {
            //backgroundAudio.Stop();
            _hrClient.StopSet();

            sessionDone.Play();

            ShowUI();
        }
    }
}
