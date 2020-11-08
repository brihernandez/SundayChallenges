using UnityEngine;
using UnityEngine.UI;

public class Hud : MonoBehaviour
{
    [Header("Readouts")]
    [SerializeField] private Text Airspeed = null;
    [SerializeField] private Text G = null;
    [SerializeField] private Text PitchRate = null;
    [SerializeField] private Text Throttle = null;
    [SerializeField] private Text Stall = null;

    [SerializeField] private Text Flaps = null;
    [SerializeField] private Text Brakes = null;
    [SerializeField] private Text Gear = null;

    [Header("Flight Elements")]
    [SerializeField] private RectTransform FPM = null;

    private void Update()
    {
        if (Aircraft.Player == null)
            return;

        var player = Aircraft.Player;
        Airspeed.text = Units.ToKnots(player.Speed).ToString("0");

        G.text = $"{player.PitchGSmoothed:0.0}G";

        // Blank pitch rate while the plane is grounded.
        PitchRate.text = player.IsGrounded ? "0.0" : $"{-player.PitchRate:0.0}";

        if (player.FlightInput.Reheat)
            Throttle.text = "THR: AFT";
        else
            Throttle.text = $"THR: {player.FlightInput.Throttle * 100f:0}%";

        // When on the ground, cage the velocity vector to the ground..
        var velocityPos = player.IsGrounded
            ? player.transform.position + player.GetFlattenedForward() * 500f
            : player.transform.position + player.VelocityDirection * 500f;
        FPM.position = Camera.main.WorldToScreenPoint(velocityPos);

        // Flash the stalling text when stalling.
        Stall.enabled = player.IsStalling && !player.IsGrounded
            ? Mathf.Sin(Time.time * 30f) > .35f
            : false;

        Flaps.enabled = IsPartTextVisible(player.Flaps);
        Gear.enabled = IsPartTextVisible(player.Gear);
        Brakes.enabled = IsPartTextVisible(player.Brakes);
    }

    private bool IsPartTextVisible(ExtendablePart part)
    {
        // Extendable parts flash when partially extended, but are solid when fully extended.
        return part.IsExtended && !part.IsFullyExtended
            ? Mathf.Sin(Time.time * 30f) > .5f
            : part.IsFullyExtended;
    }
}
