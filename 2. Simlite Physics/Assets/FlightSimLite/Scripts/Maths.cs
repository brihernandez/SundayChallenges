﻿using UnityEngine;

public static class Maths
{
    /// <summary>
    /// Remaps the value <paramref name="value"/> from range X to range Y.
    /// </summary>
    public static float Remap(float xLow, float xHigh, float yLow, float yHigh, float value)
    {
        var lerp = Mathf.InverseLerp(xLow, xHigh, value);
        return Mathf.Lerp(yLow, yHigh, lerp);
    }

    /// <summary>
    /// Remaps the value <paramref name="value"/> from range X to range Y.
    /// </summary>
    public static Vector3 Remap(Vector3 xLow, Vector3 xHigh, Vector3 yLow, Vector3 yHigh, float value)
    {
        return new Vector3(
            Remap(xLow.x, xHigh.x, yLow.x, yHigh.x, value),
            Remap(xLow.y, xHigh.y, yLow.y, yHigh.y, value),
            Remap(xLow.z, xHigh.z, yLow.z, yHigh.z, value));
    }

    public static float CalculatePitchG(Transform transform, Vector3 velocity, float pitchRateDeg)
    {
        // Angular velocity is in radians per second.
        var pitchRate = pitchRateDeg * Mathf.Deg2Rad;
        Vector3 localVelocity = transform.InverseTransformDirection(velocity);

        // If there is no angular velocity in the pitch, then there's no force generated by a turn.
        // Return only the planet's gravity as it would be felt in the vertical.
        if (Mathf.Abs(pitchRate) < Mathf.Epsilon)
            return transform.up.y;

        // Local pitch velocity (X) is positive when pitching down.

        // Radius of turn = velocity / angular velocity
        float radius = localVelocity.z / pitchRate;

        // The radius of the turn will be negative when in a pitching down turn.

        // Force is mass * radius * angular velocity^2
        float verticalForce = (localVelocity.z * localVelocity.z) / radius;

        // Express in G
        float verticalG = -verticalForce / 9.8f;

        // Add the planet's gravity in. When the up is facing directly up, then the full
        // force of gravity will be felt in the vertical.
        verticalG += transform.up.y;

        return verticalG;
    }
}