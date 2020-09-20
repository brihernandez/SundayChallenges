using UnityEngine;
using TMPro;

public class MaverickUI : MonoBehaviour
{
    [SerializeField] private Canvas canvas = null;
    [SerializeField] private TextMeshProUGUI targetNameLabel = null;
    [SerializeField] private TextMeshProUGUI rangeLabel = null;
    [SerializeField] private RectTransform crosshair = null;

    public void SetCamera(Camera camera)
    {
        canvas.worldCamera = camera;
    }

    public void SetCameraAngle(float azimuth, float elevation)
    {
        crosshair.anchoredPosition = new Vector2(azimuth / 1f, elevation / 1f);
    }

    public void SetTargetName(string name)
    {
        targetNameLabel.text = name.Length > 0
            ? name
            : "NO TGT";
    }

    public void SetRange(float range)
    {
        rangeLabel.text = range < 0
            ? "---"
            : range.ToString("0");
    }
}
