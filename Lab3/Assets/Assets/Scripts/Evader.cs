using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class Evader : MonoBehaviour
{
    [Header("Refs")]
    Transform player;
    public Transform[] waypoints;

    [Header("Movimiento")]
    public float walkSpeed = 2.2f;
    public float runSpeed = 5.0f;
    public float acceleration = 22f;
    public float angularSpeedDeg = 720f;
    public float arriveThreshold = 0.35f;

    [Header("Visión (FOV)")]
    public float sightRange = 12f;
    public float sightFovDeg = 90f;
    public float eyeHeight = 1.6f;
    public LayerMask visionObstructionMask;

    [Header("Tuning")]
    public float reselectionCooldown = 0.4f; 

    NavMeshAgent agent;
    Animator anim;

    int lastVisited = -1;  
    int currentTarget = -1;  
    float lastRetargetTime = -999f;

    bool runningUntilArrive = false;
    private float waitUntil = -1f;
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

        SelectNextTarget(-1, -1); 
    }

    void Update()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        bool sees = CanSeePlayer();

        if (sees && Time.time - lastRetargetTime > reselectionCooldown && waitUntil < 0f)
        {
            SelectNextTarget(excludeA: currentTarget, excludeB: lastVisited);
            lastRetargetTime = Time.time;
            runningUntilArrive = true;
        }

        if (waitUntil >= 0f)
        {
            agent.isStopped = true;
            agent.speed = walkSpeed; 

            if (Time.time >= waitUntil)
            {
                waitUntil = -1f;
                agent.isStopped = false;
                lastVisited = currentTarget;
                SelectNextTarget(excludeA: lastVisited, excludeB: -1);
            }
        }
        else
        {
            agent.isStopped = false;
            agent.speed = runningUntilArrive ? runSpeed : walkSpeed;

            if (!agent.pathPending && agent.remainingDistance <= arriveThreshold)
            {
                runningUntilArrive = false;
                waitUntil = Time.time + 3f; 
            }
        }

        float v = agent.velocity.magnitude;
        float speedParam;

        if (waitUntil >= 0f) speedParam = 0f;                  
        else if (agent.hasPath)
            speedParam = runningUntilArrive ? 1f : 0.5f;              
        else speedParam = 0f;                  

        anim.SetFloat("Speed", speedParam, 0.1f, Time.deltaTime);
    }

    void SelectNextTarget(int excludeA, int excludeB)
    {
        int n = waypoints?.Length ?? 0;
        if (n == 0) return;

        List<int> candidates = new List<int>(n);
        for (int i = 0; i < n; i++)
        {
            if (waypoints[i] == null) continue;
            if (i == excludeA || i == excludeB) continue;
            candidates.Add(i);
        }
        if (candidates.Count == 0)
        {
            for (int i = 0; i < n; i++)
            {
                if (waypoints[i] == null) continue;
                if (i == excludeA) continue;
                candidates.Add(i);
            }
        }
        if (candidates.Count == 0)
        {
            for (int i = 0; i < n; i++) if (waypoints[i] != null) candidates.Add(i);
        }
        if (candidates.Count == 0) return;

        int next = candidates[Random.Range(0, candidates.Count)];
        currentTarget = next;
        agent.SetDestination(waypoints[currentTarget].position);
    }

    bool CanSeePlayer()
    {
        if (!player) return false;

        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * (eyeHeight * 0.6f);

        Vector3 flatDir = new Vector3(target.x - eye.x, 0f, target.z - eye.z);
        float dist = flatDir.magnitude;
        if (dist > sightRange || dist <= 0.001f) return false;

        float halfFov = sightFovDeg * 0.5f;
        if (Vector3.Angle(transform.forward, flatDir) > halfFov) return false;

        if (visionObstructionMask.value != 0)
        {
            Vector3 dir = (target - eye).normalized;
            if (Physics.Raycast(eye, dir, out RaycastHit hit, dist, visionObstructionMask))
                return false;
        }
        return true;
    }

    void OnValidate()
    {
        var a = GetComponent<NavMeshAgent>();
        if (a)
        {
            a.stoppingDistance = arriveThreshold;
            a.acceleration = acceleration;
            a.angularSpeed = angularSpeedDeg;
        }
    }
}
