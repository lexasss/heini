using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GazeClient : MonoBehaviour
{
    // to be set in inspector

    public bool simulate;
    public bool useTobiiSDK = true;

    public Button options;
    public Button calibrate;
    public Button toggleTracking;
    public Text deviceName;
    public Text tobiiModel;
    public Button tobiiToggleTracking;
    public Dropdown tobiiEye;
    public Text debug;
    public GameObject etudControls;
    public GameObject tobiiControls;

    // public members

    public event EventHandler Start = delegate { };
    public event EventHandler Stop = delegate { };
    public event EventHandler State = delegate { };
    public event EventHandler Sample = delegate { };

    public bool IsTracking { get; private set; } = false;
    public GazeIO.Sample LastSample { get; private set; }
    public RawPoint Location { get; private set; } = new RawPoint(0, 0f, 0f);

    // overrides

    void Awake()
    {
        etudControls.SetActive(!useTobiiSDK);
        tobiiControls.SetActive(useTobiiSDK);

        _log = FindObjectOfType<Log>();

        _smoother = new Smoother<RawPoint>
        {
            SaccadeThreshold = 30,
            TimeWindow = 150,
            DampFixation = 700
        };

        if (_isSimulated)
        {
            _simulator = FindObjectOfType<GazeSimulator>();
            _simulator.Sample += OnSimulatorSample;
            _simulator.State += OnSimulatorState;
            _simulator.Device += OnSimulatorDevice;
            _simulator.Initialize();
            return;
        }

        if (useTobiiSDK)
        {
            _tobii = GetComponent<TobiiClient>();
            _tobii.Error += OnTobiiError;
            _tobii.Ready += OnTobiiReady;
            _tobii.Toggled += OnTobiiToggled;
            _tobii.Data += OnTobiiData;

            tobiiEye.value = (int)_tobii.eye;
        }
        else
        {
            _ws = new WebSocketSharp.WebSocket("ws://localhost:8086/");
            _ws.OnOpen += (sender, e) =>
            {
                print("WS:> Connected");
            };
            _ws.OnClose += (sender, e) =>
            {
                print("WS:> Disconnected");
            };
            _ws.OnError += (sender, e) =>
            {
                print($"WS:> Error {e.Message}");
            };
            _ws.OnMessage += (sender, e) =>
            {
            //print($"WS:> MSG {e.Data}");
            lock (_messages)
                {
                    _messages.Enqueue(e.Data);
                }
            };

            _ws.ConnectAsync();
        }
    }

    void Update()
    {
        lock (_messages)
        {
            while (_messages.Count > 0)
            {
                ParseMessage(_messages.Dequeue());
            }
        }
    }

    void OnDestroy()
    {
        if (_ws != null)
        { 
            _ws.Close();
        }
        if (_tobii != null)
        {
            _tobii.Close();
            _tobii = null;
        }
    }

    void OnApplicationQuit()
    {
        if (_ws != null && IsTracking && _hasInitiatedTracking)
        {
            _ws.Send(GazeIO.Request.ToggleTracking);
        }
        if (_tobii != null)
        {
            _tobii.Close();
            _tobii = null;
        }
    }

    // methods

    public void ShowOptions()
    {
        if (!_isSimulated && !useTobiiSDK)
            _ws.Send(GazeIO.Request.ShowOptions);
    }

    public void Calibrate()
    {
        if (!_isSimulated && !useTobiiSDK)
            _ws.Send(GazeIO.Request.Calibrate);
    }

    public void ToggleTracking()
    {
        if (_isSimulated)
        {
            _simulator.ToggleTracking();
        }
        else if (_tobii != null)
        {
            _tobii.ToggleTracking();
        }
        else if (_ws != null)
        {
            if (!IsTracking)
            {
                _hasInitiatedTracking = true;
            }
            _ws.Send(GazeIO.Request.ToggleTracking);
        }
    }

    public void TobiiSetEye()
    {
        if (_tobii != null)
            _tobii.eye = (TobiiClient.Eye)tobiiEye.value;
    }


    // internal

    bool _isSimulated => simulate/* || Environment.UserName == "olequ"*/;

    TobiiClient _tobii = null;
    WebSocketSharp.WebSocket _ws = null;
    GazeSimulator _simulator = null;
    Log _log;

    readonly Queue<string> _messages = new Queue<string>();
    
    Vector2 _scale = new Vector2(1f, 1f);
    Vector2 _offset = new Vector2(0f, 0f);
    Smoother<RawPoint> _smoother;
    bool _hasInitiatedTracking = false;
    bool _trackingInitialized = false;

    void ParseMessage(string message)
    {
        GazeIO.Sample sample = JsonUtility.FromJson<GazeIO.Sample>(message);
        if (sample.IsValid)
        {
            LastSample = sample;
            //print($"WS:> sample = {sample.x}, {sample.y}");
            UpdateCursorLocation(sample);
            return;
        }

        GazeIO.State state = JsonUtility.FromJson<GazeIO.State>(message);
        if (state.IsValid)
        {
            //print($"WS:> status = {state.value}");
            UpdateState(state);
            return;
        }

        GazeIO.Device device = JsonUtility.FromJson<GazeIO.Device>(message);
        if (device.IsValid)
        {
            //print($"WS:> device name = {device.name}");
            UpdateDeviceInfo(device);
            return;
        }
    }

    void UpdateDeviceInfo(GazeIO.Device device)
    {
        deviceName.text = device.name;
    }

    void UpdateState(GazeIO.State state)
    {
        bool trackingChanged = state.IsTracking != IsTracking;

        IsTracking = state.IsTracking;

        // gaze ui and controls
        options.interactable = !IsTracking && !state.IsBusy;
        calibrate.interactable = !IsTracking && state.IsConnected && !state.IsBusy;
        toggleTracking.interactable = state.IsConnected && state.IsCalibrated && !state.IsBusy && !IsTracking;
        toggleTracking.GetComponentInChildren<Text>().text = IsTracking ? "Stop" : "Start";

        State(this, new EventArgs());

        if (IsTracking && !_trackingInitialized)
        {
            InitializeTracking();
            _log.ClearEvents();
        }

        if (trackingChanged)
        {
            // input module
            GetComponent<StandaloneInputModule>().enabled = !IsTracking;

            if (IsTracking)
            {
                Start(this, new EventArgs());
            }
            else
            {
                Stop(this, new EventArgs());
            }
        }
    }

    void UpdateCursorLocation(GazeIO.Sample sample)
    {
        Vector2 location = GazeToGameWindow(sample);

        Location = _smoother.Feed(new RawPoint(sample.ts, location.x, location.y));
        // debug.text = $"S = {aSample.x:N0} {aSample.y:N0}; F = {this.location.x:N0} {this.location.y:N0}";

        Sample(this, new EventArgs());
    }

    Vector2 GazeToGameWindow(GazeIO.Sample sample)
    {
        return new Vector2(
            sample.x - Screen.width / 2 - _offset.x,
            Screen.height / 2 - (sample.y - _offset.y)
        );
    }

    void InitializeTracking()
    {
        Rect rc = Camera.main.pixelRect;
        _scale.x = rc.width / Screen.currentResolution.width;
        _scale.y = rc.height / Screen.currentResolution.height;

        try
        {
            rc = WinAPI.GetWindowRect();

            _offset = new Vector2(
                rc.x + (rc.width - Screen.width) / 2,
                rc.y + (rc.height - Screen.height) / 2 + (Application.isEditor ? 17 : 0) // toolbar
            );
        }
        catch (Exception) { }

        _trackingInitialized = true;
    }

    // Tobii
    void OnTobiiError(object sender, string error)
    {
        deviceName.text = error;
        print($"TOBII:> ERROR: {error}");
    }

    void OnTobiiReady(object sender, string model)
    {
        deviceName.text = model;
        tobiiToggleTracking.interactable = true;

        if (!_trackingInitialized)
        {
            InitializeTracking();
        }
    }

    void OnTobiiToggled(object sender, bool isTracking)
    {
        tobiiToggleTracking.GetComponentInChildren<Text>().text = isTracking ? "Stop" : "Start";

        if (isTracking)
        {
            _log.ClearEvents();
            Start(this, new EventArgs());
        }
        else
        {
            Stop(this, new EventArgs());
        }
    }

    void OnTobiiData(object sender, GazeIO.Sample sample)
    {
        LastSample = sample;
        UpdateCursorLocation(sample);
    }

    // Simulator
    void OnSimulatorDevice(object sender, GazeSimulator.DeviceArgs args)
    {
        if (useTobiiSDK)
        {
            OnTobiiReady(null, args.Device.name);
        }
        else
        {
            UpdateDeviceInfo(args.Device);
        }
    }

    void OnSimulatorState(object sender, GazeSimulator.StateArgs args)
    {
        if (useTobiiSDK)
        {
            OnTobiiToggled(null, args.State.IsTracking);
        }
        else
        {
            UpdateState(args.State);
        }
    }

    void OnSimulatorSample(object sender, GazeSimulator.SampleArgs args)
    {
        LastSample = args.Sample;
        UpdateCursorLocation(args.Sample);
    }
}
