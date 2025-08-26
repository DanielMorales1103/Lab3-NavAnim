using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class Shooter : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;            
    public Transform[] waypoints;       
    public Transform shootOrigin;       

    [Header("Movimiento")]
    public float walkSpeed = 2.6f;
    public float acceleration = 18f;
    public float angularSpeedDeg = 720f;
    public float arriveThreshold = 0.35f;

    [Header("Visión (FOV grande)")]
    public float sightRange = 18f;
    public float sightFovDeg = 60f;
    public float eyeHeight = 1.6f;
    public LayerMask visionObstructionMask; 

    [Header("Disparo (solo anim)")]
    public string shootTriggerName = "Shoot"; 
    public float shootCooldown = 1.2f;        
    public float shootStopTime = 0.15f;       

    NavMeshAgent agent;
    Animator anim;
    int currentWp = -1;
    float lastShoot = -999f;
    float resumeMoveAt = 0f;

    [SerializeField] string shootStateTag = "Shoot";
    [SerializeField] float shotTurnSpeed = 900f;   
    Vector3 shotAimDir = Vector3.zero;             
    bool wasShooting = false;

    public GameObject projectilePrefab;
    public float projectileSpeed = 18f;
    public int projectileDamage = 10;
    public float shootRange = 30f;

    [SerializeField] float projectileSpawnDelay = 0.20f; 

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        agent.updateRotation = true;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeedDeg;
        agent.stoppingDistance = arriveThreshold;
        agent.speed = walkSpeed;

        PickNextWaypoint();
    }

    void Update()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        bool shootingNow = IsShooting();

        if (shootingNow || Time.time < resumeMoveAt)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;

            // tomar control de la rotación durante el shoot
            if (shootingNow && !wasShooting) agent.updateRotation = false;

            // girar suavemente hacia la dirección congelada
            if (shotAimDir.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(shotAimDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, look, shotTurnSpeed * Time.deltaTime);
            }
        }
        else
        {
            if (!shootingNow && wasShooting) agent.updateRotation = true;

            agent.isStopped = false;

            if (CanSeePlayer() && Time.time - lastShoot >= shootCooldown)
            {
                ShootOnce();
            }

            if (!agent.pathPending && agent.remainingDistance <= arriveThreshold)
                PickNextWaypoint();
        }
        wasShooting = shootingNow;
        UpdateAnimator();
    }

    void ShootOnce()
    {
        lastShoot = Time.time;
        anim.SetTrigger(shootTriggerName);

        Vector3 origin = shootOrigin ? shootOrigin.position
                                     : transform.position + Vector3.up * (eyeHeight * 0.8f);
        Vector3 targetPos = player ? player.position + Vector3.up * (eyeHeight * 0.6f)
                                   : origin + transform.forward;

        shotAimDir = targetPos - origin;
        shotAimDir.y = 0f;
        if (shotAimDir.sqrMagnitude < 0.0001f) shotAimDir = transform.forward;
        shotAimDir.Normalize();

        agent.updateRotation = false;
        transform.rotation = Quaternion.LookRotation(shotAimDir, Vector3.up);

        Vector3 aimPoint = origin + shotAimDir * shootRange;
        if (Physics.Raycast(origin, shotAimDir, out RaycastHit hit, shootRange, ~0, QueryTriggerInteraction.Ignore))
            aimPoint = hit.point;

        StartCoroutine(SpawnProjectileAfterDelay(projectileSpawnDelay, aimPoint, origin));
        
        if (shootStopTime > 0f) resumeMoveAt = Time.time + shootStopTime;
    }

    IEnumerator SpawnProjectileAfterDelay(float delay, Vector3 capturedAimPoint, Vector3 fallbackOrigin)
    {
        yield return new WaitForSeconds(delay); 

        Vector3 spawnPos = shootOrigin ? shootOrigin.position : fallbackOrigin;
        Vector3 fireDir = (capturedAimPoint - spawnPos).normalized;

        if (projectilePrefab)
        {
            var go = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(fireDir, Vector3.up));

            var pr = go.GetComponent<SimpleProjectile>();
            if (pr)
                pr.InitTowards(capturedAimPoint, projectileSpeed, projectileDamage, transform);
            else
            {
                var rb = go.GetComponent<Rigidbody>();
                if (rb)
                {
                    rb.isKinematic = false;
                    rb.useGravity = false;
                    rb.linearVelocity = fireDir * projectileSpeed;
                }
            }

            var projCol = go.GetComponent<Collider>();
            if (projCol)
            {
                foreach (var col in GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(projCol, col, true);
            }
        }
    }


    void PickNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        int next = (waypoints.Length == 1) ? 0 : Random.Range(0, waypoints.Length);
        if (next == currentWp && waypoints.Length > 1) next = (next + 1) % waypoints.Length;

        currentWp = next;
        agent.SetDestination(waypoints[currentWp].position);
        agent.speed = walkSpeed;
    }

    bool CanSeePlayer()
    {
        if (!player) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * (eyeHeight * 0.6f);

        Vector3 flat = new Vector3(target.x - eye.x, 0f, target.z - eye.z);
        float dist = flat.magnitude;
        if (dist > sightRange || dist <= 0.001f) return false;

        if (Vector3.Angle(transform.forward, flat) > sightFovDeg * 0.5f) return false;

        if (visionObstructionMask.value != 0)
        {
            Vector3 dir = (target - eye).normalized;
            if (Physics.Raycast(eye, dir, out RaycastHit hit, dist, visionObstructionMask))
                return false;
        }
        return true;
    }
    bool IsShooting()
    {
        var st = anim.GetCurrentAnimatorStateInfo(0);
        return st.IsTag(shootStateTag);
    }

    void UpdateAnimator()
    {
        if (IsShooting() || Time.time < resumeMoveAt)
        {
            anim.SetFloat("Speed", 0f, 0.1f, Time.deltaTime); 
            return;
        }

        float v = agent.velocity.magnitude;
        float param = (v > 0.05f || agent.hasPath) ? 1f : 0f;
        anim.SetFloat("Speed", param, 0.1f, Time.deltaTime);
    }

}
