using System;

/**
 * <summary>Definitions of structures and constants used in communication with ETUDriver service</summary>
 * */
namespace GazeIO
{
    /** <summary>State values</summary> */
    [Flags]
    public enum StateValue : int
    {
        Connected = 0x01,
        Calibrated = 0x02,
        Tracking = 0x04,
        Busy = 0x08,
    }

    /** <summary>Request string</summary> */
    internal static class Request
    {
        public static string ShowOptions => "SHOW_OPTIONS";
        public static string Calibrate => "CALIBRATE";
        public static string ToggleTracking => "TOGGLE_TRACKING";
        public static string SetDevice => "SET_DEVICE";
        public static string PassValue => "PASS_VALUE";
    }

    /** <summary>Content of the "type" field of JSON object received from the server</summary> */
    public static class MessageType
    {
        public static string Sample => "sample";
        public static string State => "state";
        public static string Device => "device";
        public static string Custom => "custom";
    }

    /** <summary>Interface for the JSON message received from the server</summary> */
    interface IMessage
    {
        /** <summary>Returns true if the "type" field of JSON object corresponds to the structure that implement this interface</summary> */
        bool IsValid { get; }
    }

    /** <summary>Message with a device name</summary> */
    public class Device : IMessage
    {
        public string type = "";
        /** <summary>Device name</summary> */
        public string name = "";

        public bool IsValid => type == MessageType.Device;
    }

    /** <summary>Message with the ETUDriver state</summary> */
    public class State : IMessage
    {
        public string type = "";
        /** <summary>State flags. Use "isXxxx" properties to get the value of a particular state flag</summary> */
        public int value = -1;

        public bool IsValid => type == MessageType.State;

        public bool IsConnected => (value & (int)StateValue.Connected) > 0;
        public bool IsCalibrated => (value & (int)StateValue.Calibrated) > 0;
        public bool IsTracking => (value & (int)StateValue.Tracking) > 0;
        public bool IsBusy => (value & (int)StateValue.Busy) > 0;
    }

    /** <summary>Message with a gaze sample</summary> */
    public class Sample : IMessage
    {
        /** <summary>Eyes in tracker's camera view</summary> */
        public class EyesInCamera
        {
            /** <summary>Left eye X</summary> */
            public float xl = .0f;
            /** <summary>Left eye Y</summary> */
            public float yl = .0f;
            /** <summary>Right eye X</summary> */
            public float xr = .0f;
            /** <summary>Right eye Y</summary> */
            public float yr = .0f;
        }

        public string type = "";
        /** <summary>Timestamp, ms</summary> */
        public ulong ts = 0;
        /** <summary>Gaze X</summary> */
        public float x = .0f;
        /** <summary>Gaze Y</summary> */
        public float y = .0f;
        /** <summary>Pupil size</summary> */
        public float p = .0f;
        /** <summary>Eyes in camera view</summary> */
        public EyesInCamera ec = new EyesInCamera();

        public bool IsValid => type == MessageType.Sample;

        public static Sample Copy(Sample refs)
        {
            var s = new Sample
            {
                type = MessageType.Sample,
                ts = refs.ts,
                x = refs.x,
                y = refs.y,
                p = refs.p,
                ec = new EyesInCamera()
                {
                    xl = refs.ec.xl,
                    yl = refs.ec.yl,
                    xr = refs.ec.xr,
                    yr = refs.ec.yr
                }
            };
            return s;
        }
    }
}
