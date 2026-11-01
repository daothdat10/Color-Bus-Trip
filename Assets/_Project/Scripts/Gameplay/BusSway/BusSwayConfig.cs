using UnityEngine;

[CreateAssetMenu(
    fileName = "BusSwayConfig",
    menuName = "Bus/Bus Sway Config"
)]
public sealed class BusSwayConfig : ScriptableObject
{
    [Header("Dynamic Sway")]
    [Min(0f)]
    public float tiltPerAcceleration = 1.8f;

    [Range(0f, 30f)]
    public float maxTiltDegrees = 6f;

    [Min(0f)]
    public float maxAcceleration = 12f;

    [Min(0f)]
    public float accelerationSmoothing = 0.12f;

    [Min(0f)]
    public float accelerationDeadZone = 0.05f;

    [Min(0f)]
    public float spring = 28f;

    [Min(0f)]
    public float damping = 8f;

    [Min(0f)]
    public float horizontalSwayMultiplier = 1f;

    [Header("Impulse")]
    [Min(0f)]
    public float turnSwayVelocityPerDegree = 0.7f;

    [Min(0f)]
    public float impulseSpring = 32f;

    [Min(0f)]
    public float impulseDamping = 8f;

    [Range(0f, 30f)]
    public float maxImpulseAngle = 5f;

    [Header("Passenger Boarding Punch")]
    public Vector3 boardPunchRotationPower = new Vector3(-2f, 0f, 3f);

    public Vector3 boardPunchPositionPower = new Vector3(0f, 0.04f, 0f);

    [Min(0f)]
    public float boardPunchDuration = 0.24f;

    private void OnValidate()
    {
        tiltPerAcceleration = Mathf.Max(0f, tiltPerAcceleration);
        maxTiltDegrees = Mathf.Clamp(maxTiltDegrees, 0f, 30f);
        maxAcceleration = Mathf.Max(0f, maxAcceleration);
        accelerationSmoothing = Mathf.Max(0f, accelerationSmoothing);
        accelerationDeadZone = Mathf.Max(0f, accelerationDeadZone);
        spring = Mathf.Max(0f, spring);
        damping = Mathf.Max(0f, damping);
        horizontalSwayMultiplier = Mathf.Max(0f, horizontalSwayMultiplier);
        turnSwayVelocityPerDegree = Mathf.Max(0f, turnSwayVelocityPerDegree);
        impulseSpring = Mathf.Max(0f, impulseSpring);
        impulseDamping = Mathf.Max(0f, impulseDamping);
        maxImpulseAngle = Mathf.Clamp(maxImpulseAngle, 0f, 30f);
        boardPunchDuration = Mathf.Max(0f, boardPunchDuration);
    }
}
