using UnityEngine;
using System.Collections.Generic;

public class FireControl : MonoBehaviour
{
    [SerializeField]
    private List<Missile> controlledMissile = new List<Missile>();

    [SerializeField]
    private List<Transform> targetList = new List<Transform>();

    [SerializeField]
    private MaverickUI maverickUI = null;

    private Queue<Missile> missileQueue;
    private Queue<Transform> targetQueue;

    public Missile ActiveMissile => missileQueue.Count > 0 ? missileQueue.Peek() : null;
    public Transform ActiveTarget => targetQueue.Peek();

    private void Awake()
    {
        missileQueue = new Queue<Missile>(controlledMissile);
        targetQueue = new Queue<Transform>(targetList);
    }

    private void Start()
    {
        ReadyMissile(ActiveMissile);
        ActiveMissile.SetTarget(ActiveTarget);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
            CycleMissile();

        if (Input.GetKeyDown(KeyCode.T))
            CycleTarget();

        if (Input.GetKeyDown(KeyCode.Space))
            LaunchMissile();

        var targetRange = ActiveTarget != null
            ? Vector3.Distance(transform.position, ActiveTarget.position)
            : -1f;
        maverickUI.SetRange(targetRange);

        var targetName = ActiveTarget != null
            ? ActiveTarget.name
            : "";
        maverickUI.SetTargetName(targetName);

        if (ActiveMissile != null)
            maverickUI.SetCameraAngle(ActiveMissile.CameraAzimuth, ActiveMissile.CameraElevation);
    }

    public void CycleTarget()
    {
        if (targetQueue.Count == 0)
            return;

        var target = targetQueue.Dequeue();
        targetQueue.Enqueue(target);

        // Null when there are no missiles left.
        if (ActiveMissile != null)
            ActiveMissile.SetTarget(target);

        Debug.Log($"Targeted {ActiveTarget.name}");
    }

    public void CycleMissile()
    {
        if (missileQueue.Count == 0)
        {
            Debug.Log("Out of missiles");
        }
        else
        {
            var previousMissile = missileQueue.Dequeue();
            previousMissile.SetReady(false);
            missileQueue.Enqueue(previousMissile);

            var activeMissile = missileQueue.Peek();
            ReadyMissile(activeMissile);
            Debug.Log($"Selected missile {activeMissile.name}");
        }
    }

    public void LaunchMissile()
    {
        if (missileQueue.Count == 0)
        {
            Debug.Log("No missiles to launch.");
        }
        else
        {
            var toLaunch = missileQueue.Dequeue();
            toLaunch.Launch();

            if (missileQueue.Count > 0)
                ReadyMissile(missileQueue.Peek());
        }
    }

    private void ReadyMissile(Missile missile)
    {
        if (missile == null)
            return;

        missile.SetReady(true);
        missile.SetTarget(ActiveTarget);
        maverickUI.SetCamera(missile.MissileCamera);
    }
}
