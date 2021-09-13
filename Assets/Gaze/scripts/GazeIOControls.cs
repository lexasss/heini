using UnityEngine;

public class GazeIOControls : MonoBehaviour
{
    // overrides

    void Start()
    {
        _gazeClient = FindObjectOfType<GazeClient>();
    }

    // methods

    public void Options()
    {
        _gazeClient.ShowOptions();
    }

    public void Calibrate()
    {
        _gazeClient.Calibrate();
    }

    public void ToggleTracking()
    {
        _gazeClient.ToggleTracking();
    }


    // internal

    GazeClient _gazeClient;
}
