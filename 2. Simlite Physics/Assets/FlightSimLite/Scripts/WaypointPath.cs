using UnityEngine;

using System.Collections.Generic;

public class WaypointPath : MonoBehaviour
{
    public List<Transform> Points = new List<Transform>();

    private void OnDrawGizmos()
    {
        for (int i = 0; i < Points.Count; ++i)
        {
            Gizmos.DrawWireSphere(Points[i].position, 50f);
            Debug.DrawLine(
                Points[i].position,
                Points[(i + 1) % Points.Count].position);
        }
    }
}
