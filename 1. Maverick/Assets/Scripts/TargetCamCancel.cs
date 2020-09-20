using UnityEngine;

public class TargetCamCancel : MonoBehaviour
{
    public Canvas canvas = null;

    private void Update()
    {
        canvas.enabled = canvas.worldCamera != null;
    }
}
