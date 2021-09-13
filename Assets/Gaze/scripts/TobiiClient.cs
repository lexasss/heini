using System;
using UnityEngine;
using Tobii.Research;
using GazeIO;

public class TobiiClient : MonoBehaviour
{
    public enum Eye
    {
        Left = 0,
        Right = 1,
        Both = 2
    }

    public Eye eye { get; set; } = Eye.Both;

    public event EventHandler<string> Error = delegate { };
    public event EventHandler<string> Ready = delegate { };
    public event EventHandler<bool> Toggled = delegate { };
    public event EventHandler<Sample> Data = delegate { };

    // overrides

    void Start()
    {
        SearchEyeTrackers();
    }

    // methods

    public void ToggleTracking()
    {
        if (_eyeTracker != null)
        {
            if (!_isStreaming)
            {
                _eyeTracker.GazeDataReceived += OnGazeDataReceived;
                InvokeRepeating(nameof(StreamData), 0.1f, 0.033f);
            }
            else
            {
                CancelInvoke(nameof(StreamData));
                _eyeTracker.GazeDataReceived -= OnGazeDataReceived;
            }

            _isStreaming = !_isStreaming;
            Toggled(this, _isStreaming);
        }
    }

    public void Close()
    {
        if (_eyeTracker != null)
        {
            _eyeTracker.Dispose();
            _eyeTracker = null;
        }
    }


    // internal

    IEyeTracker _eyeTracker = null;
    bool _isStreaming = false;
    Sample _lastSample = null;

    async void SearchEyeTrackers()
    {
        var collection = await EyeTrackingOperations.FindAllEyeTrackersAsync();
        if (collection.Count > 0)
        {
            var tracker = collection[0];

            try
            {
                _eyeTracker = EyeTrackingOperations.GetEyeTracker(tracker.Address);
            }
            catch (Exception ex)
            {
                Error(this, ex.Message);
            }

            if (_eyeTracker != null)
            {
                Ready(this, _eyeTracker.DeviceName);
            }
        }
        else
        {
            Invoke(nameof(SearchEyeTrackers), 5);
        }
    }

    void StreamData()
    {
        if (_lastSample != null)
        {
            Sample sample;
            lock (_lastSample)
            {
                sample = Sample.Copy(_lastSample);
                _lastSample = null;
            }

            Data(this, sample);
        }
    }

    void OnGazeDataReceived(object sender, GazeDataEventArgs args)
    {
        var left = args.LeftEye;
        var right = args.RightEye;
        var gpLeft = left.GazePoint.PositionOnDisplayArea;
        var gpRight = right.GazePoint.PositionOnDisplayArea;

        var sample = new Sample
        {
            type = MessageType.Sample,
            ts = (ulong)(args.SystemTimeStamp / 1000),
            p = 0
        };

        // sample
        var leftPointIsValid = !float.IsNaN(gpLeft.X) && !float.IsNaN(gpLeft.Y);
        var rightPointIsValid = !float.IsNaN(gpRight.X) && !float.IsNaN(gpRight.Y);

        if (leftPointIsValid && (eye == Eye.Left || (eye == Eye.Both && !rightPointIsValid)))
        {
            sample.x = gpLeft.X * Screen.width;
            sample.y = gpLeft.Y * Screen.height;
        }
        else if (rightPointIsValid && (eye == Eye.Right || (eye == Eye.Both && !leftPointIsValid)))
        {
            sample.x = gpRight.X * Screen.width;
            sample.y = gpRight.Y * Screen.height;
        }
        else if (eye == Eye.Both && leftPointIsValid && rightPointIsValid)
        {
            sample.x = (gpLeft.X + gpRight.X) / 2 * Screen.width;
            sample.y = (gpLeft.Y + gpRight.Y) / 2 * Screen.height;
        }
        else
        {
            sample.x = -Screen.width;
            sample.y = -Screen.height;
        }

        sample.x = (float)Math.Round(sample.x);
        sample.y = (float)Math.Round(sample.y);

        // pupil
        var leftPupilIsValid = !float.IsNaN(left.Pupil.PupilDiameter);
        var rightPupilIsValid = !float.IsNaN(right.Pupil.PupilDiameter);

        if (leftPupilIsValid && (eye == Eye.Left || (eye == Eye.Both && !rightPupilIsValid)))
        {
            sample.p = left.Pupil.PupilDiameter;
        }
        else if (rightPupilIsValid && (eye == Eye.Right || (eye == Eye.Both && !leftPupilIsValid)))
        {
            sample.p = right.Pupil.PupilDiameter;
        }
        else if (eye == Eye.Both && leftPupilIsValid && rightPupilIsValid)
        {
            sample.p = (left.Pupil.PupilDiameter + right.Pupil.PupilDiameter) / 2;
        }
        else
        {
            sample.p = 0;
        }

        // update sample storage

        if (_lastSample != null)
        {
            lock (_lastSample)
            {
                _lastSample = sample;
            }
        }
        else
        {
            _lastSample = sample;
        }
    }
}
