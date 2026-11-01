using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class BusSwayController : MonoBehaviour
{
    private const float DefaultTiltPerAcceleration = 1.8f;
    private const float DefaultMaxTiltDegrees = 6f;
    private const float DefaultMaxAcceleration = 12f;
    private const float DefaultAccelerationSmoothing = 0.12f;
    private const float DefaultAccelerationDeadZone = 0.05f;
    private const float DefaultSpring = 28f;
    private const float DefaultDamping = 8f;
    private const float DefaultHorizontalSwayMultiplier = 1f;
    private const float DefaultImpulseDamping = 10f;
    private const float DefaultImpulseSpring = 32f;
    private const float DefaultTurnSwayVelocityPerDegree = 0.7f;
    private const float DefaultMaxImpulseAngle = 5f;
    private const float MinimumDeltaTime = 0.0001f;

    [Header("References")]
    [SerializeField] private Transform tray;
    [SerializeField] private BusSwayConfig config;

    [Header("Runtime")]
    [SerializeField] private bool dynamicSwayActive = true;

    private Quaternion initialLocalRotation;
    private Vector3 lastRootPosition;
    private Vector3 lastRootVelocity;
    private Vector3 lastRootForward;
    private Vector3 smoothedAcceleration;
    private Vector3 swayAngle;
    private Vector3 swayAngleVelocity;
    private Vector3 impulseAngle;
    private Vector3 impulseAngleVelocity;
    private Vector3 initialLocalPosition;
    private Vector3 boardPunchRotation;
    private Vector3 boardPunchPosition;
    private Sequence passengerBoardSequence;
    private Sequence turnSwaySequence;
    private float turnSwayAngle;
    private float turnSwaySpeedFactor = 1f;
    private bool isPassengerBoardPlaying;
    private bool hasMotionSample;
    private bool isInitialized;
    private float smoothedTurnRate;
    public Transform Tray => tray;
    public Vector3 CurrentSwayAngle => swayAngle + impulseAngle +
        new Vector3(0f, 0f, turnSwayAngle);

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        if (isInitialized)
        {
            ResetMotionSample();
        }
    }

    private void LateUpdate()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (!isInitialized || tray == null || tray == transform)
        {
            return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, MinimumDeltaTime);
        Vector3 rootPosition = transform.position;
        Vector3 rootForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (rootForward.sqrMagnitude <= Mathf.Epsilon)
        {
            rootForward = lastRootForward.sqrMagnitude > Mathf.Epsilon
                ? lastRootForward : Vector3.forward;
        }
        rootForward.Normalize();

        if (!hasMotionSample)
        {
            lastRootPosition = rootPosition;
            lastRootVelocity = Vector3.zero;
            lastRootForward = rootForward;
            hasMotionSample = true;
            Sway(deltaTime);
            return;
        }

        Vector3 rootVelocity = (rootPosition - lastRootPosition) / deltaTime;
        float speed = Vector3.Dot(rootVelocity, rootForward);
        float previousSpeed = Vector3.Dot(lastRootVelocity, lastRootForward);
        float yawRate = Vector3.SignedAngle(
            lastRootForward, rootForward, Vector3.up) * Mathf.Deg2Rad / deltaTime;
        Vector3 localAcceleration = new Vector3(
            speed * yawRate, 0f, (speed - previousSpeed) / deltaTime);
        float blend = 1f - Mathf.Exp(-deltaTime / 0.12f);
        smoothedTurnRate = Mathf.Lerp(
            smoothedTurnRate,
            yawRate * Mathf.Rad2Deg,
            blend);

        float maximumAcceleration = GetMaxAcceleration();
        if (maximumAcceleration > 0f)
        {
            localAcceleration = Vector3.ClampMagnitude(
                localAcceleration, maximumAcceleration);
        }

        lastRootPosition = rootPosition;
        lastRootVelocity = rootVelocity;
        lastRootForward = rootForward;

        smoothedAcceleration = SmoothAcceleration(
            smoothedAcceleration,
            localAcceleration,
            deltaTime
        );

        Sway(deltaTime);
    }

    public void SimulateSway(Vector3 angleVelocity)
    {
        if (!IsFinite(angleVelocity))
        {
            return;
        }

        if (!isInitialized)
        {
            Initialize();
        }

        swayAngleVelocity += angleVelocity;
        Sway();
    }

    public void PlayTurnSway(float signedYawDegrees)
    {
        if (!dynamicSwayActive || !IsFinite(signedYawDegrees) ||
            Mathf.Abs(signedYawDegrees) < 5f)
        {
            return;
        }

        if (!isInitialized)
        {
            Initialize();
        }

        if (!isInitialized || GetMaxImpulseAngle() <= 0f)
        {
            return;
        }

        turnSwaySequence?.Kill();
        float direction = Mathf.Sign(signedYawDegrees);
        float peak = Mathf.Min(
            Mathf.Abs(signedYawDegrees) * GetTurnSwayVelocityPerDegree() * 0.2f,
            GetMaxImpulseAngle());

        turnSwaySequence = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .Append(DOTween.To(
                () => turnSwayAngle, angle => turnSwayAngle = angle,
                direction * peak, 0.18f).SetEase(Ease.OutCubic))
            .Append(DOTween.To(
                () => turnSwayAngle, angle => turnSwayAngle = angle,
                -direction * peak * 0.2f, 0.22f).SetEase(Ease.InOutSine))
            .Append(DOTween.To(
                () => turnSwayAngle, angle => turnSwayAngle = angle,
                0f, 0.28f).SetEase(Ease.OutSine));
        SetTurnSwaySpeed(turnSwaySpeedFactor);
    }

    public void SetTurnSwaySpeed(float speedFactor)
    {
        turnSwaySpeedFactor = Mathf.Clamp01(speedFactor);
        if (turnSwaySequence == null || !turnSwaySequence.IsActive())
            return;

        turnSwaySequence.timeScale = Mathf.Max(turnSwaySpeedFactor, 0.001f);
        if (turnSwaySpeedFactor <= 0f)
            turnSwaySequence.Pause();
        else
            turnSwaySequence.Play();
    }

    public void Sway()
    {
        Sway(Mathf.Max(Time.deltaTime, MinimumDeltaTime));
    }

    private void Sway(float deltaTime)
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (!isInitialized || tray == null || tray == transform)
        {
            return;
        }

        Vector3 targetAngle = dynamicSwayActive
            ? CalculateTargetAngle(smoothedAcceleration)
            : Vector3.zero;
        if (dynamicSwayActive)
        {
            float turnBank = Mathf.Clamp(
     smoothedTurnRate * 0.03f, -30f, 30f);

            float maxTilt = GetMaxTiltDegrees();
            targetAngle.z = Mathf.Clamp(
                targetAngle.z + turnBank, -maxTilt, maxTilt);
        }
        AdvanceSpring(targetAngle, deltaTime);
        AdvanceImpulse(deltaTime);
        ApplySwayRotation();
    }


    public void ResetSway()
    {
        KillPassengerBoardSequence();
        turnSwaySequence?.Kill();
        turnSwaySequence = null;
        turnSwayAngle = 0f;
        turnSwaySpeedFactor = 1f;
        swayAngle = Vector3.zero;
        swayAngleVelocity = Vector3.zero;
        impulseAngle = Vector3.zero;
        impulseAngleVelocity = Vector3.zero;
        boardPunchRotation = Vector3.zero;
        boardPunchPosition = Vector3.zero;
        smoothedAcceleration = Vector3.zero;
        ResetMotionSample();

        if (tray != null && tray != transform)
        {
            ApplySwayRotation();
        }
    }

    private void Initialize()
    {
        if (isInitialized)
        {
            return;
        }

        ResolveTray();

        if (tray == null || tray == transform)
        {
            return;
        }

        initialLocalRotation = tray.localRotation;
        initialLocalPosition = tray.localPosition;
        ResetMotionSample();
        isInitialized = true;
    }

    private void ResolveTray()
    {
        if (tray != null)
        {
            return;
        }

        tray = transform.Find("Car 1");

        if (tray == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                string childName = child.name.ToLowerInvariant();

                if (childName.Contains("wheel") || childName.Contains("banhxe"))
                {
                    continue;
                }

                if (child.GetComponentInChildren<MeshRenderer>(true) != null)
                {
                    tray = child;
                    break;
                }
            }
        }

        if (tray == null && transform.childCount == 1)
        {
            tray = transform.GetChild(0);
        }
    }

    private void ResetMotionSample()
    {
        smoothedTurnRate = 0f;
        lastRootPosition = transform.position;
        lastRootVelocity = Vector3.zero;
        lastRootForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        hasMotionSample = false;
    }

    private Vector3 SmoothAcceleration(
        Vector3 current,
        Vector3 target,
        float deltaTime
    )
    {
        float smoothingTime = GetAccelerationSmoothing();

        if (smoothingTime <= 0f)
        {
            return target;
        }

        float blend = 1f - Mathf.Exp(-deltaTime / smoothingTime);
        return Vector3.Lerp(current, target, blend);
    }

    private Vector3 CalculateTargetAngle(Vector3 localAcceleration)
    {
        float deadZone = GetAccelerationDeadZone();
        float accelerationMagnitude = localAcceleration.magnitude;

        if (accelerationMagnitude <= deadZone)
        {
            return Vector3.zero;
        }

        if (deadZone > 0f)
        {
            localAcceleration *=
                (accelerationMagnitude - deadZone) / accelerationMagnitude;
        }

        float maximumAcceleration = GetMaxAcceleration();

        if (maximumAcceleration > 0f)
        {
            localAcceleration = Vector3.ClampMagnitude(
                localAcceleration,
                maximumAcceleration
            );
        }

        float tiltPerAcceleration = GetTiltPerAcceleration();
        float maximumTilt = GetMaxTiltDegrees();
        float horizontalMultiplier = GetHorizontalSwayMultiplier();

        float pitch = -localAcceleration.z * tiltPerAcceleration * 0.35f;
        float roll = localAcceleration.x * tiltPerAcceleration * horizontalMultiplier;

        return new Vector3(
            Mathf.Clamp(pitch, -maximumTilt * 0.35f, maximumTilt * 0.35f),
            0f,
            Mathf.Clamp(roll, -maximumTilt, maximumTilt)
        );
    }

    private void AdvanceSpring(Vector3 targetAngle, float deltaTime)
    {
        Vector3 springForce =
            (targetAngle - swayAngle) * GetSpring() -
            swayAngleVelocity * GetDamping();
        // Giảm damping riêng trục nghiêng để thân xe vượt nhẹ qua 0.
        springForce.z =
            (targetAngle.z - swayAngle.z) * GetSpring()
            - swayAngleVelocity.z * GetDamping() * 0.5f;

        swayAngleVelocity += springForce * deltaTime;
        swayAngle += swayAngleVelocity * deltaTime;

        float maximumTilt = GetMaxTiltDegrees();
        swayAngle.x = Mathf.Clamp(swayAngle.x, -maximumTilt * 0.35f, maximumTilt * 0.35f);
        swayAngle.z = Mathf.Clamp(swayAngle.z, -maximumTilt, maximumTilt);
    }

    private void AdvanceImpulse(float deltaTime)
    {
        Vector3 restoringForce =
            -impulseAngle * GetImpulseSpring() -
            impulseAngleVelocity * GetImpulseDamping();
        impulseAngleVelocity += restoringForce * deltaTime;
        impulseAngle += impulseAngleVelocity * deltaTime;

        float maximumImpulse = GetMaxImpulseAngle();
        impulseAngle.x = Mathf.Clamp(impulseAngle.x, -maximumImpulse, maximumImpulse);
        impulseAngle.y = Mathf.Clamp(impulseAngle.y, -maximumImpulse, maximumImpulse);
        impulseAngle.z = Mathf.Clamp(impulseAngle.z, -maximumImpulse, maximumImpulse);
    }

    private float GetTiltPerAcceleration()
    {
        return config == null
            ? DefaultTiltPerAcceleration
            : Mathf.Max(0f, config.tiltPerAcceleration);
    }

    private float GetMaxTiltDegrees()
    {
        return config == null
            ? DefaultMaxTiltDegrees
            : Mathf.Clamp(config.maxTiltDegrees, 0f, 30f);
    }

    private float GetMaxAcceleration()
    {
        return config == null
            ? DefaultMaxAcceleration
            : Mathf.Max(0f, config.maxAcceleration);
    }

    private float GetAccelerationSmoothing()
    {
        return config == null
            ? DefaultAccelerationSmoothing
            : Mathf.Max(0f, config.accelerationSmoothing);
    }

    private float GetAccelerationDeadZone()
    {
        return config == null
            ? DefaultAccelerationDeadZone
            : Mathf.Max(0f, config.accelerationDeadZone);
    }

    private float GetSpring()
    {
        return config == null ? DefaultSpring : Mathf.Max(0f, config.spring);
    }

    private float GetDamping()
    {
        return config == null ? DefaultDamping : Mathf.Max(0f, config.damping);
    }

    private float GetHorizontalSwayMultiplier()
    {
        return config == null
            ? DefaultHorizontalSwayMultiplier
            : Mathf.Max(0f, config.horizontalSwayMultiplier);
    }

    private float GetImpulseDamping()
    {
        return config == null
            ? DefaultImpulseDamping
            : Mathf.Max(0f, config.impulseDamping);
    }

    private float GetImpulseSpring()
    {
        return config == null
            ? DefaultImpulseSpring
            : Mathf.Max(0f, config.impulseSpring);
    }

    private float GetTurnSwayVelocityPerDegree()
    {
        return config == null
            ? DefaultTurnSwayVelocityPerDegree
            : Mathf.Max(0f, config.turnSwayVelocityPerDegree);
    }

    private float GetMaxImpulseAngle()
    {
        return config == null
            ? DefaultMaxImpulseAngle
            : Mathf.Clamp(config.maxImpulseAngle, 0f, 30f);
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void OnDisable()
    {
        KillPassengerBoardSequence();
        turnSwaySequence?.Kill();
        turnSwaySequence = null;
        turnSwayAngle = 0f;
        turnSwaySpeedFactor = 1f;

        if (tray != null && tray != transform)
        {
            tray.localPosition = initialLocalPosition;
            tray.localRotation = initialLocalRotation;
        }
    }


    private void ApplySwayRotation()
    {
        if (isInitialized && tray != null && tray != transform)
        {
            tray.localPosition = initialLocalPosition + boardPunchPosition;
            tray.localRotation =
                initialLocalRotation * Quaternion.Euler(
                    swayAngle + impulseAngle + boardPunchRotation +
                    new Vector3(0f, 0f, turnSwayAngle)
                );
        }
    }


    private void FinishPassengerBoardAnimation()
    {
        isPassengerBoardPlaying = false;
        boardPunchRotation = Vector3.zero;
        boardPunchPosition = Vector3.zero;
        passengerBoardSequence = null;
        ApplySwayRotation();
    }

    private void KillPassengerBoardSequence()
    {
        if (passengerBoardSequence != null)
        {
            passengerBoardSequence.Kill(false);
            passengerBoardSequence = null;
        }

        if (isPassengerBoardPlaying)
        {
            FinishPassengerBoardAnimation();
        }
    }

    public void PlayPassengerBoardedAnimation()
    {
        if (!isInitialized)
        {
            Initialize();
        }

        if (!isInitialized || tray == null || tray == transform)
        {
            return;
        }

        KillPassengerBoardSequence();

        isPassengerBoardPlaying = true;
        float duration = GetBoardPunchDuration();
        float halfDuration = Mathf.Max(duration * 0.5f, MinimumDeltaTime);
        Vector3 rotationPower = GetBoardPunchRotationPower();
        Vector3 positionPower = GetBoardPunchPositionPower();

        passengerBoardSequence = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .Append(DOTween.To(
                () => boardPunchRotation,
                value =>
                {
                    boardPunchRotation = value;
                    ApplySwayRotation();
                },
                rotationPower,
                halfDuration
            ).SetEase(Ease.OutQuad))
            .Join(DOTween.To(
                () => boardPunchPosition,
                value =>
                {
                    boardPunchPosition = value;
                    ApplySwayRotation();
                },
                positionPower,
                halfDuration
            ).SetEase(Ease.OutQuad))
            .Append(DOTween.To(
                () => boardPunchRotation,
                value =>
                {
                    boardPunchRotation = value;
                    ApplySwayRotation();
                },
                Vector3.zero,
                halfDuration
            ).SetEase(Ease.InQuad))
            .Join(DOTween.To(
                () => boardPunchPosition,
                value =>
                {
                    boardPunchPosition = value;
                    ApplySwayRotation();
                },
                Vector3.zero,
                halfDuration
            ).SetEase(Ease.InQuad))
            .OnComplete(FinishPassengerBoardAnimation)
            .OnKill(() =>
            {
                if (isPassengerBoardPlaying)
                {
                    FinishPassengerBoardAnimation();
                }
            });
    }

    private float GetBoardPunchDuration()
    {
        return config == null
            ? 0.24f
            : Mathf.Max(0f, config.boardPunchDuration);
    }

    private Vector3 GetBoardPunchRotationPower()
    {
        return config == null
            ? new Vector3(-2f, 0f, 3f)
            : config.boardPunchRotationPower;
    }

    private Vector3 GetBoardPunchPositionPower()
    {
        return config == null
            ? new Vector3(0f, 0.04f, 0f)
            : config.boardPunchPositionPower;
    }

    private void OnDestroy()
    {
        KillPassengerBoardSequence();
        turnSwaySequence?.Kill();
        turnSwaySequence = null;
    }

public void Init()
    {
        Initialize();
    }
}
