using System;
using UnityEngine;

public class GazeSimulator : MonoBehaviour
{
    // static

    public static readonly float SAMPLING_INTERVAL = 0.03333f;
    public static readonly int TOOLBAR_HEIGHT = 17;

    // definitions

    public class SampleArgs : EventArgs
    {
        public readonly GazeIO.Sample Sample;
        public SampleArgs(ulong timestamp, float x, float y, float pupil)
        {
            Sample = new GazeIO.Sample
            {
                type = GazeIO.MessageType.Sample,
                ts = timestamp,
                x = x,
                y = y,
                p = pupil
            };
        }
    }

    public class StateArgs : EventArgs
    {
        public readonly GazeIO.State State;
        public StateArgs(GazeIO.State state)
        {
            State = state;
        }
    }

    public class DeviceArgs : EventArgs
    {
        public readonly GazeIO.Device Device;
        public DeviceArgs(string deviceName)
        {
            Device = new GazeIO.Device
            {
                type = GazeIO.MessageType.Device,
                name = deviceName
            };
        }
    }

    // events

    public event EventHandler<SampleArgs> Sample = delegate { };
    public event EventHandler<StateArgs> State = delegate { };
    public event EventHandler<DeviceArgs> Device = delegate { };

    public bool Enabled { get; private set; } = false;

    // overrides

    void Awake()
    {
        Rect rc = WinAPI.GetWindowRect();

        _offset = new Vector2(
            rc.x + (rc.width - Screen.width) / 2,
            rc.y + (rc.height - Screen.height) / 2 + TOOLBAR_HEIGHT
        ); ;

        _state.type = GazeIO.MessageType.State;
        _state.value = (int)GazeIO.StateValue.Connected | (int)GazeIO.StateValue.Calibrated;
    }

    // methods

    public void ToggleTracking()
    {
        if ((_state.value & (int)GazeIO.StateValue.Tracking) == 0)
        {
            _state.value |= (int)GazeIO.StateValue.Tracking;
            InvokeRepeating(nameof(EmitSample), SAMPLING_INTERVAL, SAMPLING_INTERVAL);
        }
        else
        {
            _state.value &= ~(int)GazeIO.StateValue.Tracking;
            _timeStamp = 0;
            CancelInvoke();
        }

        State(this, new StateArgs(_state));
    }

    public void Initialize()
    {
        Enabled = true;
        Device(this, new DeviceArgs("Simulator"));
        State(this, new StateArgs(_state));
    }


    // internal

    readonly GazeIO.State _state = new GazeIO.State();
    Vector2 _offset;
    ulong _timeStamp = 0;

    void EmitSample()
    {
        _timeStamp += (ulong)(SAMPLING_INTERVAL * 1000);

        MouseToGaze(out float x, out float y);
        
        Sample(this, new SampleArgs(_timeStamp, x, y, 6.0f));
    }

    void MouseToGaze(out float x, out float y)
    {
        x = Input.mousePosition.x + _offset.x;
        y = (Screen.height - Input.mousePosition.y) + _offset.y;
    }
}
