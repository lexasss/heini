using UnityEngine;
using UnityEngine.UI;

public class HRClient : MonoBehaviour
{
    // to be set in inspector

    public Text status;
    public Button connectButton;

    // internal members

    NetStation _netStation;
    Log _log;

    // overides

    void Start()
    {
        _netStation = GetComponent<NetStation>();
        _netStation.Message += onNetStationMessage;

        _log = FindObjectOfType<Log>();
    }

    // public methods

    public void Connect()
    {
        status.text = $"connecting to {_netStation.Host}:{_netStation.Port}...";
        _netStation.Connect();
    }

    public void Begin()
    {
        _netStation.Begin();
    }

    public void Stop()
    {
        _netStation.End();
        _netStation.Disconnect();
    }

    public void StartSet(int setIndex)
    {
        SendEvent($"Set{setIndex}");
    }

    public void StopSet()
    {
        SendEvent("SetS");
    }

    public void StartVideo(char id)
    {
        SendEvent($"Vid{id}");
    }

    public void StopVideo()
    {
        SendEvent($"VidS");
    }

    public void InterruptVideo()
    {
        SendEvent("VidI");
    }

    // internal methods

    void SendEvent(string aMessage)
    {
        _log.HR(aMessage);
        _netStation.Event(aMessage);
    }

    void onNetStationMessage(object sender, NetStation.StateChangedEventArgs e)
    {
        status.text = e.Message;

        if (e.State == NetStation.State.CONNECTED)
        {
            connectButton.enabled = false;
            Invoke("Begin", 1);
        }
        else if (e.State == NetStation.State.NOT_CONNECTED || e.State == NetStation.State.FAILED_TO_CONNECT)
        {
            connectButton.enabled = true;
        }
    }
}
