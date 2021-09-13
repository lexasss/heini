using System;
using UnityEngine;
using UnityEngine.Video;

public class VideoSets : MonoBehaviour
{
    // to be set in inspector

    public VideoClip[] set1;
    public VideoClip[] set2;
    public VideoClip[] set3;
    public VideoClip[] set4;

    // props

    public bool HasMoreVideos => _currentSet != null && (_index + 1) < _currentSet.Length;
    public int CurrentSetID { get; private set; } = -1;

    // methods

    public void Awake()
    {
        _sets = new VideoClip[4][]
        {
            set1,
            set2,
            set3,
            set4,
        };
    }

    public void Reset()
    {
        _index = -1;
    }

    public void SelectSet(int index)
    {
        if (index < 0 || index >= _sets.Length)
        {
            throw new ArgumentOutOfRangeException();
        }

        CurrentSetID = index;
        _currentSet = _sets[index];
    }

    public VideoClip Next()
    {
        if (_currentSet == null)
        {
            return null;
        }

        if (++_index == _currentSet.Length)
        {
            _index = -1;
            return null;
        }

        return _currentSet[_index];
    }


    // Internal

    int _index = -1;
    VideoClip[][] _sets;
    VideoClip[] _currentSet = null;
}
