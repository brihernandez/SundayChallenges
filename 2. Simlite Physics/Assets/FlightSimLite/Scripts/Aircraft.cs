using UnityEngine;

/// <summary>
/// All flight input to control the plane is read from this class. A player or AI pilot would
/// set the appropriate values to fly their plane.
/// </summary>
public class FlightInput
{
    public float Pitch = 0f;
    public float Yaw = 0f;
    public float Roll = 0f;

    public float Throttle = .67f;
    public bool Reheat = false;

    public bool Flaps = false;
    public bool Brake = false;
    public bool GearDown = false;
}

/// <summary>
/// Helper class for handling parts that extend from the plane and cause drag. Parts extend over
/// time, and their intermediate states can be read to drive visuals on the model or HUD elements.
/// </summary>
[System.Serializable]
public class ExtendablePart
{
    [Tooltip("Time to fully extend/retract the part.")]
    public float ExtendTime = 1f;
    [Tooltip("Drag added to the plane relative to its normal drag value. Higher values impart " +
        "greater drag when the part is fully extended.")]
    public float DragMultiplier = 3f;

    [System.NonSerialized]
    public float ExtendState = 0f;

    public bool IsFullyExtended => ExtendState >= 1f - Mathf.Epsilon;
    public bool IsExtended => ExtendState > Mathf.Epsilon;

    public ExtendablePart(float extendTime, float dragMultiplier)
    {
        ExtendTime = extendTime;
        DragMultiplier = dragMultiplier;
    }

    public float Update(float targetState, float deltaTime)
    {
        ExtendState = ExtendTime > 0f
            ? Mathf.MoveTowards(ExtendState, targetState, 1f / ExtendTime * deltaTime)
            : targetState;

        return ExtendState;
    }
}

public class Aircraft : MonoBehaviour
{
    [Header("Unity Properties")]
    [Tooltip("FixedUpdate is more accurate and consistent, but Update looks smoother at high FPS.")]
    public bool UseFixedUpdate = false;
    [Tooltip("Knots")]
    public float StartSpeed = 350f;

    [Header("AI Pilot")]
    public WaypointPath Path = null;
    public Transform Target = null;

    [Tooltip("Signifies that this is the player aircraft.")]
    public bool IsPlayer = false;

    [Tooltip("Scales the distance moved by the plane. Can be used to create tighter action at the cost " +
        "of things looking like they move slower than normal."), Range(.1f, 1f)]
    public float Scale = 1f;

    [Header("Ground Handling and Collisions")]
    public bool StartGrounded = false;
    public LayerMask CollisionMask = -1;
    public float GearHeight = 1.6f;

    [Header("Thrust to Weight")]
    [Tooltip("Kilograms")]
    public float Mass = 11500f;
    [Tooltip("Newtons")]
    public float MilThrust = 79000f;
    [Tooltip("Newtons")]
    public float ReheatThrust = 129000f;

    [Header("Drag and Stability")]
    [Tooltip("Unitless. Higher values result in slower planes.")]
    public float Drag = .7f;
    [Tooltip("Unitless. Higher values result in angle of attack creating more drag.")]
    public float InducedDrag = .35f;
    [Range(1f, 10f), Tooltip("Unitless. Higher values result in less AOA generated during turns.")]
    public float Responsiveness = 3f;

    [Header("Stalling")]
    [Tooltip("Knots. Flying slower than this causes a loss in altitude. Affects low speed maneverability as well. " +
        "Higher values result in a more sluggish plane at low speeds, while planes with a low stall speed can easily " +
        "reach their maximum turn rates at low speed.")]
    public float StallSpeedClean = 150f;
    [Tooltip("Knots. When flaps are fully extended, this becomes the new stall speed.")]
    public float StallSpeedFlaps = 130f;
    [Tooltip("Angle of attack (deg) the plane will have when stalled.")]
    public float StallAOA = 10f;

    [Header("Draggy Parts")]
    public ExtendablePart Flaps = new ExtendablePart(1f, 2f);
    public ExtendablePart Gear = new ExtendablePart(2f, 3f);
    public ExtendablePart Brakes = new ExtendablePart(.5f, 3f);

    [Header("G Limits")]
    [Tooltip("Positive G limit. Has a great impact on maneuverability at speed.")]
    public float MaxG = 7f;
    [Tooltip("Negative G limit.")]
    public float MinG = 3f;

    [Header("Maneuverability")]
    [Tooltip("Max theoretical pitch rate (deg/s) the plane can achieve.")]
    public float MaxPitchRate = 20f;
    [Tooltip("How quickly the aircraft reacts to pitch input.")]
    public float PitchResponse = 4f;
    [Tooltip("Max theoretical roll rate (deg/s) the plane can achieve.")]
    public float MaxRollRate = 120f;
    [Tooltip("How quickly the aircraft reacts to roll input.")]
    public float RollResponse = 5f;
    [Tooltip("Max theoretical yaw rate (deg/s) the plane can achieve.")]
    public float MaxYawRate = 6f;
    [Tooltip("How quickly the aircraft reacts to yaw input.")]
    public float YawResponse = 2f;

    /// <summary>
    /// Stick, rudder, and throttle input for flying the plane. If this is the player aircraft,
    /// input will automatically be pulled from the player. Otherwise, some AI should set this
    /// to control the aircraft.
    /// </summary>
    public FlightInput FlightInput = new FlightInput();

    /// <summary>
    /// Velocity in m/s
    /// </summary>
    public Vector3 Velocity { get; private set; } = Vector3.zero;

    /// <summary>
    /// Normalized direction of the velocity vector.
    /// </summary>
    public Vector3 VelocityDirection { get; private set; } = Vector3.forward;

    /// <summary>
    /// Speed in m/s
    /// </summary>
    public float Speed { get; private set; } = 0f;

    /// <summary>
    /// Pitch rate in deg/s
    /// </summary>
    public float PitchRate { get; private set; } = 0f;

    /// <summary>
    /// Roll rate in deg/s
    /// </summary>
    public float RollRate { get; private set; } = 0f;

    /// <summary>
    /// Yaw rate in deg/s
    /// </summary>
    public float YawRate { get; private set; } = 0f;

    /// <summary>
    /// Instantaneous G in the pitch axis. Reads 1G when upright in level flight.
    /// </summary>
    public float PitchG { get; private set; } = 1f;

    /// <summary>
    /// Smoothed value for G in the pitch axis. Reads 1G when upright in level flight.
    /// </summary>
    public float PitchGSmoothed { get; private set; } = 1f;

    /// <summary>
    /// True when the plane has reached the stall speed and in danger of losing altitude.
    /// </summary>
    public bool IsStalling => Units.ToKnots(Speed) < StallSpeedClean;

    /// <summary>
    /// Stall speed (m/s) of the aircraft taking into consideration flaps.
    /// </summary>
    public float DynamicStallSpeed { get; private set; } = 77f;

    [Header("Debug")]
    public bool IsGrounded = false;
    public float LandedPitchAngle = 0f;

    /// <summary>
    /// Direct reference to the player aircraft. Can be null if there is no player.
    /// </summary>
    public static Aircraft Player { get; private set; } = null;

    private void Awake()
    {
        Velocity = transform.forward * Units.ToMetersPerSecond(StartSpeed);
        VelocityDirection = transform.forward;
        Speed = Units.ToMetersPerSecond(StartSpeed);

        if (IsPlayer)
            Player = this;

        if (StartGrounded)
        {
            // Ground clamp the plane
            var isGroundUnderneath = Physics.Raycast(
                origin: transform.position,
                direction: Vector3.down,
                hitInfo: out RaycastHit hitInfo,
                maxDistance: 10000f,
                layerMask: CollisionMask);

            if (isGroundUnderneath)
            {
                IsGrounded = true;

                Speed = 0f;
                Velocity = Vector3.zero;

                FlightInput.Throttle = 0f;
                FlightInput.GearDown = true;
                FlightInput.Flaps = true;

                Gear.ExtendState = 1f;
                Flaps.ExtendState = 1f;

                transform.position = hitInfo.point + Vector3.up * GearHeight;
                transform.forward = Vector3.Cross(transform.right, hitInfo.normal);
            }
        }
    }

    private void FixedUpdate()
    {
        if (UseFixedUpdate)
            RunFlightModel(Time.fixedDeltaTime);
    }

    private void Update()
    {
        if (!UseFixedUpdate)
            RunFlightModel(Time.deltaTime);

        if (IsPlayer)
            GetPlayerInput();
        else
            GetAIPilotInput();
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"UNITY Collided with {collision.collider.name}");
    }

    private void RunCollisionDetection(float deltaTime)
    {
        // Check for normal forward facing collision. E.g. Hitting terrain while flying.
        bool hitSomething = Physics.Raycast(
            origin: transform.position,
            direction: VelocityDirection,
            hitInfo: out RaycastHit hitInfo,
            maxDistance: Speed * deltaTime,
            layerMask: CollisionMask);

        if (hitSomething)
        {
            Debug.Log($"{name}: Collided with {hitInfo.collider.name}!");

            // This would probably be a crash, but just bounce!
            VelocityDirection = Vector3.Reflect(VelocityDirection, hitInfo.normal).normalized;
            Velocity = VelocityDirection * Speed;
            transform.forward = VelocityDirection;
        }

        if (!IsGrounded && Gear.IsFullyExtended && transform.up.y > 0.9f)
        {
            hitSomething = Physics.Raycast(
                origin: transform.position,
                direction: -transform.up,
                hitInfo: out hitInfo,
                maxDistance: GearHeight,
                layerMask: CollisionMask);

            if (hitSomething)
            {
                IsGrounded = true;

                // Get the angle from the horizon. The way the rotation math works out, negative
                // values for pitch are what result in a pitch up rotation.
                var flattenedForward = GetFlattenedForward();
                LandedPitchAngle = -Vector3.Angle(flattenedForward, transform.forward);

                // Prevent pitch down into the ground while grounded.
                LandedPitchAngle = Mathf.Clamp(LandedPitchAngle, -30f, 0f);

                // Ground clamp.
                YawRate = 0f;
                RollRate = 0f;
                transform.position = hitInfo.point + Vector3.up * GearHeight;
            }
        }
    }

    private int selectedPoint = 0;
    private void GetAIPilotInput()
    {
        FlightInput.Throttle = 1f;

        Vector3 targetPosition = Path.Points[selectedPoint].position;

        if (Target == null)
        {
            targetPosition = Path.Points[selectedPoint].position;
            var distanceToTarget = Vector3.Distance(targetPosition, transform.position);
            if (distanceToTarget < 300f)
                selectedPoint = (selectedPoint + 1) % Path.Points.Count;
        }
        else
        {
            targetPosition = Target.position;
        }

        Vector3 localTargetDirection = transform.InverseTransformPoint(targetPosition).normalized;
        FlightInput.Pitch = -localTargetDirection.y * 3f;
        FlightInput.Pitch = Mathf.Clamp(FlightInput.Pitch, -1f, 1f);

        FlightInput.Yaw = localTargetDirection.x * 5f;
        FlightInput.Yaw = Mathf.Clamp(FlightInput.Yaw, -1f, 1f);

        var wingsLevelRoll = transform.right.y * 3f;
        var turnIntoRoll = localTargetDirection.x * 3f;

        // Literally the MouseFlight code
        var angleOffTarget = Vector3.Angle(Vector3.forward, localTargetDirection);
        var wingsLevelInfluence = Mathf.InverseLerp(0f, 1.5f, angleOffTarget);
        FlightInput.Roll = Mathf.Lerp(wingsLevelRoll, turnIntoRoll, wingsLevelInfluence);
        FlightInput.Roll = Mathf.Clamp(FlightInput.Roll, -1f, 1f);
    }

    private void GetPlayerInput()
    {
        FlightInput.Pitch = Input.GetAxis("Vertical");
        FlightInput.Roll = Input.GetAxis("Horizontal");
        FlightInput.Yaw = Input.GetAxis("Yaw");

        // Throttle works using buttons and acts as a slider. An axis, along with some afterburner
        // detent/treshold, would need to be used for direct throttle management. Afterburner is
        // activated by pressing throttle up when at full throttle. It's neat, though this kind of
        // system is only relevant for games with fuel consumption.

        float targetThrottle;
        float throttleSpeed;
        if (Input.GetButton("Fire1"))
        {
            targetThrottle = 1f;
            throttleSpeed = .25f;
        }
        else if (Input.GetButton("Fire2"))
        {
            targetThrottle = 0f;
            throttleSpeed = .25f;
            FlightInput.Reheat = false;
        }
        else
        {
            targetThrottle = FlightInput.Throttle;
            throttleSpeed = 0f;
        }

        FlightInput.Throttle = Mathf.MoveTowards(
            FlightInput.Throttle,
            targetThrottle,
            throttleSpeed * Time.deltaTime);

        if (Mathf.Approximately(FlightInput.Throttle, 1f) && Input.GetButtonDown("Fire1"))
        {
            FlightInput.Reheat = true;
        }

        if (Input.GetKeyDown(KeyCode.F))
            FlightInput.Flaps = !FlightInput.Flaps;
        if (Input.GetKeyDown(KeyCode.B))
            FlightInput.Brake = !FlightInput.Brake;
        if (Input.GetKeyDown(KeyCode.G))
            FlightInput.GearDown = !FlightInput.GearDown;
    }

    private void RunFlightModel(float deltaTime)
    {
        Flaps.Update(FlightInput.Flaps ? 1f : 0f, deltaTime);
        Gear.Update(FlightInput.GearDown ? 1f : 0f, deltaTime);
        Brakes.Update(FlightInput.Brake ? 1f : 0f, deltaTime);

        // Stall speed is affected by flaps.
        DynamicStallSpeed = Mathf.Lerp(
            Units.ToMetersPerSecond(StallSpeedClean),
            Units.ToMetersPerSecond(StallSpeedFlaps),
            Flaps.ExtendState);

        if (IsGrounded)
        {
            RunGroundHandling(deltaTime);
        }
        else
        {
            RunFlightModelLinear(deltaTime);
            RunFlightModelRotations(deltaTime);
        }

        RunCollisionDetection(deltaTime);
    }

    private void RunGroundHandling(float deltaTime)
    {
        var hitSomething = Physics.Raycast(
            origin: transform.position,
            direction: -transform.up,
            hitInfo: out RaycastHit hitInfo,
            maxDistance: GearHeight * 2f,
            layerMask: CollisionMask);

        // Panic early escape in something weird happened.
        // Might trigger if the player drove off a sheer cliff?
        if (!hitSomething)
        {
            IsGrounded = false;
            return;
        }

        Vector3 thrustForce = CalculateThrustForce();
        Vector3 dragForce = CalculateDragForce();
        Vector3 gravityForce = CalculateGravityForce();
        var accelerationVector = (thrustForce + dragForce + gravityForce) / Mass;

        if (accelerationVector.y <= 0f)
        {
            // If the velocity vector is still pointing down, the plane is still grounded.
            // Care only about the speed/acceleration in the forward direction. (No slipping.)
            var acceleration = Vector3.Dot(transform.forward, accelerationVector);
            Speed += acceleration * deltaTime;

            // Brakes get an extra boost to being stopping because of wheel brakes.
            Speed = Mathf.MoveTowards(Speed, 0f, Brakes.ExtendState * 5f * deltaTime);

            // Stalling will turn the velocity vector down towards the ground.
            var stallAOA = Maths.Remap(DynamicStallSpeed, DynamicStallSpeed * 1.5f, StallAOA, 0f, Speed);

            // The direction that the velocity vector would ideally face. This includes things that
            // affect it such as stalling, which lowers the velocity vector towards the ground.
            var targetVelocityVector = transform.forward;
            targetVelocityVector = Vector3.RotateTowards(targetVelocityVector, Vector3.down, stallAOA * Mathf.Deg2Rad, 0f);

            if (targetVelocityVector.y < 0f)
            {
                // Velocity still isn't going up, so stay ground clamped.
                targetVelocityVector.y = 0f;
                Velocity = targetVelocityVector * Speed;
                VelocityDirection = targetVelocityVector;

                transform.position += Velocity * Scale * deltaTime;

                // Handle rotation. Re-uses a lot of the same code as the flying stuff.
                PitchG = 1f;
                PitchGSmoothed = 1f;

                // Pitching uses the same control authority as in flight to simulate aerodynamics.
                var controlAuthority = GetControlAuthority();

                // Same pitching code as when in flight, but without the stalling rotation stuff.
                var targetPitch = FlightInput.Pitch * MaxPitchRate * controlAuthority;
                var stallRate = GetStallRate() * deltaTime;
                PitchRate = SmoothDamp.Move(PitchRate + stallRate, targetPitch, PitchResponse, deltaTime);
                LandedPitchAngle += PitchRate * deltaTime;

                // Prevent pitch down into the ground while grounded. The way the rotation math
                // works out, negative values for pitch are what result in a pitch up.
                LandedPitchAngle = Mathf.Clamp(LandedPitchAngle, -30f, 0f);
                var pitchRotation = Quaternion.AngleAxis(LandedPitchAngle, Vector3.right);

                // Nosewheel steering allows the plane to be turned while on the ground.
                // Blend roll and yaw so that roll can be used to steer on the ground like in old games.
                // You'd probably want this to be optional in a real game.
                const float NosewheelTurnRate = 45f;
                const float NosewheelSteeringResponse = 3f;
                float nosewheelSteeringYawRate = Speed >= 5f
                    ? Mathf.InverseLerp(45f, 15f, Speed)
                    : Mathf.InverseLerp(0f, 5f, Speed);

                // Allow for some yaw authority after nosewheel steering reaches zero power so that
                // adjustments can continue to be made at high speed. In real life, this would be
                // caused by the rudder rather than any kind of steering system. This specific
                // method has the side effect of allowing the plane to turn in place while stopped,
                // but I won't tell if you don't.
                float maxYawRate = Mathf.Max(
                    NosewheelTurnRate * 0.1f,
                    nosewheelSteeringYawRate * NosewheelTurnRate);

                var blendedYawInput = Mathf.Clamp(FlightInput.Yaw + FlightInput.Roll, -1f, 1f);
                var targetYaw = blendedYawInput * maxYawRate;

                YawRate = SmoothDamp.Move(YawRate, targetYaw, NosewheelSteeringResponse, deltaTime);
                var yawRotation = Quaternion.AngleAxis(YawRate * deltaTime, Vector3.up);

                // This SERIOUSLY breaks down if the ground beneath the player isn't flat. To fix
                // this requires some fancy vector math that I don't have a handle on quite yet.
                var flattenedForward = GetFlattenedForward();
                transform.rotation = Quaternion.LookRotation(flattenedForward, hitInfo.normal);
                transform.localRotation *= yawRotation * pitchRotation;
            }
            else
            {
                // Acceleration vector now points skywards. Take off!
                IsGrounded = false;
                Debug.Log($"{name}: Took off!");
            }
        }
        else
        {
            // The velocity vector is starting to point upwards, which means the plane wants to go
            // up and the plane has taken off.
            IsGrounded = false;
        }
    }

    public Vector3 GetFlattenedForward()
    {
        var flat = transform.forward;
        flat.y = 0f;
        return flat.normalized;
    }

    private Vector3 CalculateThrustForce()
    {
        float thrust = FlightInput.Reheat ? ReheatThrust : FlightInput.Throttle * MilThrust;
        return transform.forward * thrust;
    }

    private Vector3 CalculateGravityForce()
    {
        return Physics.gravity * Mass;
    }

    private float GetControlAuthority()
    {
        return Mathf.InverseLerp(DynamicStallSpeed * .5f, DynamicStallSpeed * 2.5f, Speed);
    }

    private float GetStallRate()
    {
        // When stalling, the plane pitches down towards the ground.
        var stallRate = Maths.Remap(
            DynamicStallSpeed * .75f, DynamicStallSpeed * 1.25f,
            MaxPitchRate, 0f,
            Speed);

        // Decrease stall turning power as the plane faces down.
        stallRate *= 1f - Vector3.Dot(transform.forward, Vector3.down);
        return stallRate;
    }

    private Vector3 CalculateDragForce()
    {
        // Drag holds the plane back the faster it goes, until it eventually reaches an equilibrium
        // between the drag force and thrust at the plane's top speed.
        float linearDrag = Mathf.Pow(Speed, 2f) * Drag;
        float totalDrag = linearDrag;

        // Extending things from the plane increases drag.
        if (Brakes.ExtendState > Mathf.Epsilon)
            totalDrag += linearDrag * Brakes.DragMultiplier * Brakes.ExtendState;
        if (Gear.ExtendState > Mathf.Epsilon)
            totalDrag += linearDrag * Gear.DragMultiplier * Gear.ExtendState;
        if (Flaps.ExtendState > Mathf.Epsilon)
            totalDrag += linearDrag * Flaps.DragMultiplier * Flaps.ExtendState;

        var linearDragForce = -transform.forward * totalDrag;

        // Induced drag decreases speed when turning. The higher the angle of attack, the more drag.
        var inducedAOA = Vector3.Angle(transform.forward, VelocityDirection);
        Vector3 inducedDragForce = -transform.forward * Mathf.Pow(Speed, 2f) * InducedDrag * inducedAOA;

        return linearDragForce + inducedDragForce;
    }

    private void RunFlightModelLinear(float deltaTime)
    {
        // Gravity can speed the plane up in a dive, or slow it in a climb.
        Vector3 gravityForce = Physics.gravity * Mass;

        // Engines provide thrust forwards.
        float thrust = FlightInput.Reheat ? ReheatThrust : FlightInput.Throttle * MilThrust;
        Vector3 thrustForce = transform.forward * thrust;

        // Drag holds the plane back the faster it goes, until it eventually reaches an equilibrium
        // between the drag force and thrust at the plane's top speed.
        float linearDrag = Mathf.Pow(Speed, 2f) * Drag;
        float totalDrag = linearDrag;

        // Extending things from the plane increases drag.
        if (Brakes.ExtendState > Mathf.Epsilon)
            totalDrag += linearDrag * Brakes.DragMultiplier * Brakes.ExtendState;
        if (Gear.ExtendState > Mathf.Epsilon)
            totalDrag += linearDrag * Gear.DragMultiplier * Gear.ExtendState;
        if (Flaps.ExtendState > Mathf.Epsilon)
            totalDrag += linearDrag * Flaps.DragMultiplier * Flaps.ExtendState;

        Vector3 dragForce = -transform.forward * totalDrag;

        // Induced drag decreases speed when turning. The higher the angle of attack, the more drag.
        var inducedAOA = Vector3.Angle(transform.forward, VelocityDirection);
        Vector3 inducedDragForce = -transform.forward * Mathf.Pow(Speed, 2f) * InducedDrag * inducedAOA;

        // Consider the forces only as they affect forward speed as a simplification of physics.
        var acceleration = (gravityForce + thrustForce + dragForce + inducedDragForce) / Mass;
        var forwardAccel = Vector3.Dot(transform.forward, acceleration);
        Speed += forwardAccel * deltaTime;

        // Stalling will turn the velocity vector down towards the ground.
        var stallAOA = Maths.Remap(DynamicStallSpeed, DynamicStallSpeed * 1.5f, StallAOA, 0f, Speed);

        // The direction that the velocity vector would ideally face. This includes things that
        // affect it such as stalling, which lowers the velocity vector towards the ground.
        var targetVelocityVector = transform.forward;
        targetVelocityVector = Vector3.RotateTowards(targetVelocityVector, Vector3.down, stallAOA * Mathf.Deg2Rad, 0f);

        // Change the direction of the velocity smoothly so that some alpha gets generated.
        VelocityDirection = SmoothDamp.Move(
            VelocityDirection, targetVelocityVector,
            Responsiveness, deltaTime);

        Velocity = VelocityDirection * Speed;
        transform.position += Velocity * Scale * deltaTime;
    }

    private void RunFlightModelRotations(float deltaTime)
    {
        PitchG = Maths.CalculatePitchG(transform, Velocity, PitchRate);
        PitchGSmoothed = SmoothDamp.Move(PitchGSmoothed, PitchG, 3f, deltaTime);

        // The stall speed affects low speed handling. The lower the stall speed, the more control
        // the plane has at low speeds. A high stall speed results in not only poor control at low
        // speed, but also requires more speed to generate the maximum turn rate.
        var controlAuthority = GetControlAuthority();

        // Limit pitch input based on G. This is a reactive system. At low framerates (e.g. 10) the
        // sample rate will cause oscillations similar to RPMs bouncing off a rev limiter. A better
        // way to do this would be to pre-calculate a max turn rate based for a given G.
        float gLerp = PitchG > 0
            ? Mathf.InverseLerp(MaxG, MaxG + MaxG * .1f, PitchG)
            : Mathf.InverseLerp(-MinG, -MinG - MinG * .1f, PitchG);
        var gLimiter = Mathf.Lerp(0f, 1f, 1f - gLerp);

        // For each axis, generate a rotation and then damp it to create smooth motion.
        var targetPitch = FlightInput.Pitch * MaxPitchRate * gLimiter * controlAuthority;
        PitchRate = SmoothDamp.Move(PitchRate, targetPitch, PitchResponse, deltaTime);
        var pitchRotation = Quaternion.AngleAxis(PitchRate * deltaTime, Vector3.right);

        var targetYaw = FlightInput.Yaw * MaxYawRate * controlAuthority;
        YawRate = SmoothDamp.Move(YawRate, targetYaw, YawResponse, deltaTime);
        var yawRotation = Quaternion.AngleAxis(YawRate * deltaTime, Vector3.up);

        var targetRoll = FlightInput.Roll * MaxRollRate * controlAuthority;
        RollRate = SmoothDamp.Move(RollRate, targetRoll, RollResponse, deltaTime);
        var rollRotation = Quaternion.AngleAxis(-RollRate * deltaTime, Vector3.forward);

        transform.localRotation *= pitchRotation * rollRotation * yawRotation;

        // When stalling, the plane pitches down towards the ground.
        var stallRate = GetStallRate();
        if (stallRate > 0f)
        {
            // Generate stall rotation.
            var stallAxis = Vector3.Cross(transform.forward, Vector3.down);
            transform.rotation = Quaternion.AngleAxis(stallRate * deltaTime, stallAxis) * transform.rotation;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Draw out the velocity vector.
        Debug.DrawLine(
            transform.position,
            transform.position + Velocity,
            Color.red);

        UnityEditor.Handles.DrawWireCube(transform.position + Velocity, Vector3.one * 10f);
    }
#endif
}
