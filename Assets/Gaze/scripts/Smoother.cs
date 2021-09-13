using System;
using System.Collections.Generic;
using UnityEngine;

/**
  * <summary>Smoothes gaze point.
  * Smoothing can be controlling via <see cref="DampFixation"/> and <see cref="DampSaccade"/> (smoothing strenghs) parameters.
  * <see cref="TimeWindow"/> should be long enough to contain at least 6 samples.
  * <see cref="SaccadeThreshold"/> affects the fixation/saccade damping mode: 
  * with smaller values saccade damping (mild smoothing) is applied more often.
  * </summary>
  * <typeparam name="T">Data type (point or vector)</typeparam>
  */
public class Smoother<T> where T : IRawData
{
    /** <summary>Gaze state</summary> */
    private enum GazeState
    {
        /** <summary>gaze state is not know yet</summary> */
        Unknown,
        /** Gaze is in the fixation state */
        Fixation,
        /** Gaze is in the saccade state */
        Saccade
    }

    /** <summary>Data damping (smoothing strength) during fixations.</summary> */
    public uint DampFixation { get; set; } = 100;

    /** <summary>Data damping (smoothing strength) during saccades.</summary> */
    public uint DampSaccade { get; set; } = 1;

    /** <summary>Buffer time window, ms. Should be long enough to contain at least 6 samples</summary> */
    public uint TimeWindow { get; set; } = 100;

    /**
      * <summary>Distance of the average values of two parts of the buffer are compared against this threshold to determine the gaze state
      * (fixation / saccade) and therefore smoothing strength.</summary>
      */
    public double SaccadeThreshold { get; set; } = 0.02;

    /** <summary>Sampling interval, ms. If the interval is set to 0 initially (default) then it is computed automatically.</summary> */
    public ulong Interval { get; set; } = 0u;


    /** <summary>Resets the internal state</summary> */
    public void Reset()
    {
        _buffer.Clear();
        _isBufferFull = false;
        _current = default;
        Interval = 0u;
        _state = GazeState.Unknown;
    }

    /**
      * <summary>Take raw data and outputs smoothed data</summary>
      * <param name="data">Data to smooth</param>
      * <returns>Smoothed data</returns>
      * */
    public T Feed(T data)
    {
        bool isBufferFull = AddToBuffer(data);
        if (!isBufferFull)
        {
            _current = (T)Copier.Invoke(null, new object[] { data });
            return _current;
        }

        _state = EstimateState();
        if (_state == GazeState.Unknown)
        {
            _current = (T)Copier.Invoke(null, new object[] { data });
            return _current;
        }

        if (Interval == 0u)
        {
            Interval = EstimateInterval(data);
            _current = (T)Copier.Invoke(null, new object[] { data });
        }

        double alfa = (double)_damp / Interval;
        _current.Shift(data, alfa, data.Timestamp);

        return _current;
    }


    // Internal

    readonly Queue<T> _buffer = new Queue<T>();

    bool _isBufferFull = false;
    T _current = default;
    GazeState _state = GazeState.Fixation;

    uint _damp => _state == GazeState.Fixation ? DampFixation : DampSaccade;

    readonly System.Reflection.MethodInfo Copier = typeof(T).GetMethod("CopyFrom", new Type[] { typeof(T) });

    bool AddToBuffer(T data)
    {
        _buffer.Enqueue(data);

        ulong firstTimestamp = _buffer.Peek().Timestamp;

        if (!_isBufferFull)
            _isBufferFull = data.Timestamp - firstTimestamp >= TimeWindow && _buffer.Count > 3;

        while (data.Timestamp - _buffer.Peek().Timestamp >= TimeWindow)
            _buffer.Dequeue();

        return _isBufferFull;
    }

    GazeState EstimateState()
    {
        float avgXB = 0f;
        float avgYB = 0f;
        float avgXA = 0f;
        float avgYA = 0f;
        float ptsBeforeCount = 0f;
        float ptsAfterCount = 0f;

        ulong oldestTimestamp = _buffer.Peek().Timestamp;

        foreach (T data in _buffer)
        {
            ulong dt = data.Timestamp - oldestTimestamp;
            if (dt > TimeWindow / 2)
            {
                avgXB += data.X;
                avgYB += data.Y;
                ptsBeforeCount++;
            }
            else
            {
                avgXA += data.X;
                avgYA += data.Y;
                ptsAfterCount++;
            }
        }

        if (ptsBeforeCount > 0 && ptsAfterCount > 0)
        {
            avgXB /= ptsBeforeCount;
            avgYB /= ptsBeforeCount;
            avgXA /= ptsAfterCount;
            avgYA /= ptsAfterCount;

            var dx = avgXB - avgXA;
            var dy = avgYB - avgYA;
            var dist = Math.Sqrt(dx * dx + dy * dy);

            return dist > SaccadeThreshold ? GazeState.Saccade : GazeState.Fixation;
        }

        return GazeState.Unknown;
    }

    ulong EstimateInterval(T data)
    {
        if (_buffer.Count < 2)
            return 0;

        ulong duration = data.Timestamp - _buffer.Peek().Timestamp;
        return (ulong)((int)duration / (_buffer.Count - 1));
    }
}

/** <summary>Interface for shoothing data</summary> */
public interface IRawData
{
    /** <summary>Timestamp, ms</summary> */
    ulong Timestamp { get; }

    /** <summary>Gaze X</summary> */
    float X { get; }

    /** <summary>Gaze Y</summary> */
    float Y { get; }

    /**
      * <summary>Applies smooting</summary>
      * <param name="refs">Latest raw data</param>
      * <param name="alfa">Alfa</param>
      * <param name="timestamp">New timestamp</param>
      */
    void Shift(IRawData refs, double alfa, ulong timestamp);
}

/** <summary>Raw 2D gaze point</summary> */
public class RawPoint : IRawData
{
    /** <summary>Timestamp, ms</summary> */
    public ulong Timestamp { get; private set; }

    /** <summary>Gaze X</summary> */
    public float X { get; private set; }

    /** <summary>Gaze Y</summary> */
    public float Y { get; private set; }

    /**
      * <summary>Constructor</summary>
      * <param name="timestamp">Timestamp, ms</param>
      * <param name="x">Gaze X</param>
      * <param name="y">Gaze Y</param>
      */
    public RawPoint(ulong timestamp, float x, float y)
    {
        Timestamp = timestamp;
        X = x;
        Y = y;
    }

    /**
      * <summary>Copies values from another data</summary>
      * <param name="refs">Reference to copy from</param>
      */
    public static RawPoint CopyFrom(IRawData refs)
    {
        return new RawPoint(refs.Timestamp, refs.X, refs.Y);
    }

    /**
      * <summary>Applies smooting</summary>
      * <param name="refs">Latest raw data</param>
      * <param name="alfa">Alfa</param>
      * <param name="timestamp">New timestamp</param>
      */
    public void Shift(IRawData refs, double alfa, ulong timestamp)
    {
        Timestamp = timestamp;
        X = (float)((refs.X + alfa * X) / (1.0 + alfa));
        Y = (float)((refs.Y + alfa * Y) / (1.0 + alfa));
    }
}

/** <summary>Raw 3D gaze point</summary> */
public class RawVector : IRawData
{
    /** <summary>Timestamp, ms</summary> */
    public ulong Timestamp { get; private set; }

    /** <summary>Gaze X</summary> */
    public float X { get; private set; }

    /** <summary>Gaze Y</summary> */
    public float Y { get; private set; }

    /** <summary>Gaze Z</summary> */
    public float Z { get; private set; }

    /** <summary>Original vector</summary> */
    public Vector3 VectorOriginal => _vector;

    /** <summary>Shifted vector</summary> */
    public Vector3 VectorShifted => new Vector3(X, Y, Z);

    /**
      * <summary>Constructor</summary>
      * <param name="timestamp">Timestamp, ms</param>
      * <param name="vector">Gaze vector</param>
      */
    public RawVector(ulong timestamp, Vector3 vector)
    {
        _vector = vector;

        Timestamp = timestamp;

        X = (float)Math.Tan(Math.Asin(_vector.x));
        Y = (float)Math.Tan(Math.Asin(_vector.y));
    }

    /**
      * <summary>Copies values from another data</summary>
      * <param name="refs">Reference to copy from</param>
      */
    public static RawVector CopyFrom(IRawData refs)
    {
        RawVector r = (RawVector)refs;
        if (r == null)
        {
            throw new ArgumentException("Cannot copy from the instance of another type");
        }

        return new RawVector(r.Timestamp, new Vector3(r.X, r.Y, r.Z));
    }

    /**
      * <summary>Applies smooting</summary>
      * <param name="refs">Latest raw data</param>
      * <param name="alfa">Alfa</param>
      * <param name="timestamp">New timestamp</param>
      */
    public void Shift(IRawData refs, double alfa, ulong timestamp)
    {
        Timestamp = timestamp;
        X = (float)((refs.X + alfa * X) / (1.0 + alfa));
        Y = (float)((refs.Y + alfa * Y) / (1.0 + alfa));
        Z = (float)Math.Sqrt(1 - X * X - Y * Y);
    }


    // internal

    Vector3 _vector;
}
