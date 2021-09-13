using UnityEngine;
using UnityEngine.UI;

public class GazePoint : MonoBehaviour
{
    // overrides

    void Start()
    {
        _image = GetComponent<Image>();
        _image.enabled = _enabled;

        var gazeSimulator = FindObjectOfType<GazeSimulator>();

        _correctionY = gazeSimulator.Enabled ? GazeSimulator.TOOLBAR_HEIGHT : 0;
    }

    void Update()
    {
        bool pIsPressed = Input.GetKeyDown(KeyCode.P);
        if (pIsPressed)
        {
            _enabled = !_enabled;
            _image.enabled = _enabled;
        }
    }

    // methods

    public void MoveTo(GazeIO.Sample gazePoint)
    {
        if (_enabled)
        {
            _image.transform.localPosition = new Vector3(gazePoint.x - Screen.width / 2, Screen.height / 2 - gazePoint.y + _correctionY, 0);
        }
    }


    // internal

    Image _image;
    bool _enabled = false;

    int _correctionY = 0;
}
