public static class Units
{
    /// <summary>
    /// Converts from m/s to knots
    /// </summary>
    public static float ToKnots(float ms)
    {
        return ms * 1.94384f;
    }

    /// <summary>
    /// Converts from knots to m/s
    /// </summary>
    public static float ToMetersPerSecond(float knots)
    {
        return knots * 0.514444f;
    }
}
