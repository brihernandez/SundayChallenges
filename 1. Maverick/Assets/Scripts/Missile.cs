using JetBrains.Annotations;
using UnityEngine;

public class Missile : MonoBehaviour
{
    public float speed = 100f;

    [SerializeField] private Camera missileCam = null;
    [SerializeField] private TrailRenderer trail = null;

    public bool isLaunched = false;

    private Transform target = null;

    public bool IsReady { get; private set; } = false;
    public Camera MissileCamera => missileCam;

    public float CameraElevation { get; private set; } = 0f;
    public float CameraAzimuth { get; private set; } = 0f;

    public bool IsLocked => target != null;

    private void Awake()
    {
        SetReady(false);
        trail.enabled = false;
    }

    private void FixedUpdate()
    {
        if (isLaunched && target != null)
        {
            // When launched, the missile just magically flies forwards.
            transform.Translate(Vector3.forward * Time.deltaTime * speed);

            // Track the missile's target.
            if (target != null)
                transform.LookAt(target);
        }

        // Point the missile's camera at the target.
        if (target != null)
            missileCam.transform.LookAt(target);

        CalculateCameraAngles();
    }

    private void CalculateCameraAngles()
    {
        // Deduce the elevation/azimuth of the camera by flattening vectors
        // and then doing angle calculations on them. If somebody knows a better
        // way to do this, I'm all ears 😊

        // Get where the camera is looking in local space.
        var toLocalCamForward = transform.InverseTransformDirection(missileCam.transform.forward);

        // To get the vertical, take the camera's look direction in local space, but
        // remove the x component, leaving a vector that is along the vertical axis.
        Vector3 vertical = toLocalCamForward;
        vertical.x = 0f;
        var elevation = Vector3.Angle(Vector3.forward, vertical);
        elevation *= Mathf.Sign(toLocalCamForward.y);

        // Same idea as above, but for horizontal.
        Vector3 horizontal = toLocalCamForward;
        horizontal.y = 0f;
        var azimuth = Vector3.Angle(Vector3.forward, horizontal);
        azimuth *= Mathf.Sign(toLocalCamForward.x);

        CameraAzimuth = azimuth;
        CameraElevation = elevation;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Don't delete trails immediately! Deleting trails before they've had a chance
        // to fade out is one of my biggest video game pet peeves.
        trail.transform.SetParent(null);
        trail.emitting = false;

        Destroy(gameObject);
    }

    public void SetTarget(Transform target)
    {
        this.target = target;
    }

    public void Launch()
    {
        isLaunched = true;
        trail.enabled = true;
        transform.SetParent(null);
    }

    public void SetReady(bool ready)
    {
        IsReady = ready;
        missileCam.enabled = ready;
        target = null;
    }
}
