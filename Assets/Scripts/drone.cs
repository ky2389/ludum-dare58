using UnityEngine;

public class drone : MonoBehaviour
{
    [Header("Target & Body Movement")]
    public Transform target;
    public float rotationSpeed = 90f;       // deg/s (body yaw speed)
    public float angleThreshold = 15f;      // start rotating body if angle > threshold
    public float followDistance = 75f;      // move toward target if farther than this
    public float speed = 3f;                // m/s

    [Header("Lasers")]
    public Transform[] laserStarts;         // turret heads / muzzles
    public LineRenderer[] lineRenderers;    // must match laserStarts length
    public float width = 0.02f;
    public float laserRotationSpeed = 360f; // deg/s (head tracking speed)
    public float laserThreshold = 30f;      // head must be within this angle to fire
    public float maxLaserDistance = 2000f;
    public GameObject hitEffect;            // optional impact VFX

    [Tooltip("If true and hitEffect has a ParticleSystem, destroy by its duration; otherwise use fixed seconds below.")]
    public bool autodestroyByParticleDuration = true;
    public float hitEffectLifetime = 2f;    // fallback lifetime (seconds)

    [Header("Debug")]
    public bool debugRays = false;

    void Start()
    {
        // Basic safety init for line renderers
        int n = Mathf.Min(laserStarts.Length, lineRenderers.Length);
        for (int i = 0; i < n; i++)
        {
            var lr = lineRenderers[i];
            if (!lr) continue;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = width;
            lr.endWidth = width;
        }
    }

    void Update()
    {
        if (!target) return;

        Track();

        int n = Mathf.Min(laserStarts.Length, lineRenderers.Length);
        for (int i = 0; i < n; i++)
        {
            LaserAttack(laserStarts[i], lineRenderers[i]);
        }
    }

    // Rotate body toward target and move closer if too far.
    private void Track()
    {
        Quaternion targetRot = Quaternion.LookRotation(target.position - transform.position);
        float angle = Quaternion.Angle(transform.rotation, targetRot);
        if (angle > angleThreshold)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist > followDistance)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, target.position, speed * Time.deltaTime);
        }
    }

    // Per-head laser logic (draw beam, rotate head, spawn VFX)
    private void LaserAttack(Transform laserStart, LineRenderer lr)
    {
        if (!laserStart || !lr) return;

        // Desired head rotation to face the target
        Vector3 toTarget = target.position - laserStart.position;
        Quaternion desired = Quaternion.LookRotation(toTarget);
        float headAngle = Quaternion.Angle(laserStart.rotation, desired);

        // Line of sight check from head to target
        bool inSight = false;
        Vector3 dir = toTarget.normalized;
        if (Physics.Raycast(laserStart.position, dir, out RaycastHit losHit, maxLaserDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform ht = losHit.transform;
            inSight = (ht == target || ht.IsChildOf(target));
        }

        if (debugRays)
            Debug.DrawRay(laserStart.position, laserStart.forward * 5f, Color.red);

        if (headAngle < laserThreshold && inSight)
        {
            // Track head toward target
            laserStart.rotation = Quaternion.RotateTowards(
                laserStart.rotation, desired, laserRotationSpeed * Time.deltaTime);

            lr.SetPosition(0, laserStart.position);

            // Fire straight ahead; stop at first hit
            if (Physics.Raycast(laserStart.position, laserStart.forward, out RaycastHit hit, maxLaserDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                lr.SetPosition(1, hit.point);

                if (hitEffect)
                {
                    var fx = Instantiate(hitEffect, hit.point, Quaternion.identity);

                    if (autodestroyByParticleDuration)
                    {
                        var ps = fx.GetComponent<ParticleSystem>();
                        if (ps != null)
                            Destroy(fx, ps.main.duration + ps.main.startLifetime.constantMax);
                        else
                            Destroy(fx, hitEffectLifetime);
                    }
                    else
                    {
                        Destroy(fx, hitEffectLifetime);
                    }
                }
            }
            else
            {
                lr.SetPosition(1, laserStart.position + laserStart.forward * maxLaserDistance);
            }
        }
        else
        {
            // Hide beam when not firing
            lr.SetPosition(0, laserStart.position);
            lr.SetPosition(1, laserStart.position);
        }
    }
}
