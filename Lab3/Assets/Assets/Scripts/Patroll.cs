using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class Patroll : MonoBehaviour
{
    public enum AIState { Patrol, WaitAtWaypoint, Chase }

    [Header("Refs")]
    Transform player;
    public Transform[] waypoints;

    [Header("Movimiento")]
    public float patrolSpeed = 3f;
    public float chaseSpeed = 4.8f;
    public float arriveWpThreshold = 0.4f; 
    public float waitAtWaypoint = 0.25f;   

    [Header("Visión (FOV 90°)")]
    public float sightRange = 7f;
    public float sightFovDeg = 90f;   
    public float eyeHeight = 1.6f;
    public LayerMask visionObstructionMask;

    [Header("Perder objetivo")]
    public float loseTargetTime = 2.0f;

    NavMeshAgent agent;
    Animator anim;
    AIState state = AIState.Patrol;
    int wpIndex = 0;
    float waitUntil = 0f;
    float lastSeenTime = -999f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();
        player = GameObject.FindGameObjectWithTag("Player")?.transform;

        agent.updateRotation = true;
        agent.acceleration = 20f;
        agent.angularSpeed = 720f;
        agent.stoppingDistance = 0.35f;

        if (waypoints != null && waypoints.Length > 0)
            agent.SetDestination(waypoints[0].position);
        SetState(AIState.Patrol);
    }

    void Update()
    {
        switch (state)
        {
            case AIState.Patrol:
                TickPatrol();
                break;
            case AIState.WaitAtWaypoint:
                TickWait();
                break;
            case AIState.Chase:
                TickChase();
                break;
        }

        UpdateAnimator();
    }

    void TickPatrol()
    {
        if (CanSeePlayer())
        {
            lastSeenTime = Time.time;
            SetState(AIState.Chase);
            return;
        }

        agent.isStopped = false;
        agent.speed = patrolSpeed;

        if (!agent.pathPending && agent.remainingDistance <= arriveWpThreshold)
        {
            if (waitAtWaypoint > 0f)
            {
                waitUntil = Time.time + waitAtWaypoint;
                SetState(AIState.WaitAtWaypoint);
                return;
            }

            NextWaypoint();
        }
    }

    void TickWait()
    {
        if (CanSeePlayer())
        {
            lastSeenTime = Time.time;
            SetState(AIState.Chase);
            return;
        }

        agent.isStopped = true; 

        if (Time.time >= waitUntil)
        {
            NextWaypoint();
            SetState(AIState.Patrol);
        }
    }

    void TickChase()
    {
        if (CanSeePlayer())
        {
            lastSeenTime = Time.time;
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            agent.SetDestination(player.position);
            return;
        }

        if (Time.time - lastSeenTime < loseTargetTime)
        {
            agent.isStopped = false;
            agent.speed = chaseSpeed;
            return;
        }

        wpIndex = FindNearestWaypointIndex();
        agent.SetDestination(waypoints[wpIndex].position);
        SetState(AIState.Patrol);
    }

    void SetState(AIState next) => state = next;

    void NextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;
        wpIndex = (wpIndex + 1) % waypoints.Length;
        agent.isStopped = false;
        agent.SetDestination(waypoints[wpIndex].position);
    }

    int FindNearestWaypointIndex()
    {
        if (waypoints == null || waypoints.Length == 0) return 0;
        int best = 0; float bestSqr = float.MaxValue;
        Vector3 p = transform.position;
        for (int i = 0; i < waypoints.Length; i++)
        {
            float d = (waypoints[i].position - p).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = i; }
        }
        return best;
    }

    bool CanSeePlayer()
    {
        if (!player) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * (eyeHeight * 0.6f);

        Vector3 flatDir = new Vector3(target.x - eye.x, 0f, target.z - eye.z);
        float dist = flatDir.magnitude;
        if (dist > sightRange || dist < 0.001f) return false;

        if (Vector3.Angle(transform.forward, flatDir) > sightFovDeg * 0.5f) return false;

        if (visionObstructionMask.value != 0)
        {
            Vector3 dir = (target - eye).normalized;
            if (Physics.Raycast(eye, dir, out RaycastHit hit, dist, visionObstructionMask))
                return false;
        }

        return true;
    }

    void UpdateAnimator()
    {
        float v = agent.velocity.magnitude;
        float blend = (v < 0.05f) ? 0f :
                      (Mathf.Abs(agent.speed - patrolSpeed) < 0.01f ? 0.5f : 1f);
        anim.SetFloat("Speed", blend, 0.1f, Time.deltaTime);
    }
}
