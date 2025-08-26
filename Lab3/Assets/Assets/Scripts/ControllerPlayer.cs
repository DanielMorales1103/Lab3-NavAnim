using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class ControllerPlayer : MonoBehaviour
{
    [Header("Velocidades")]
    public float walkSpeed = 2.2f;
    public float runSpeed = 5.0f;

    [Header("Transiciones de llegada")]
    public float slowDownRadius = 2.5f;
    public float arriveThreshold = 0.35f;

    [Header("Raycast")]
    public LayerMask groundMask;
    public float navmeshSampleMaxDist = 2f;

    NavMeshAgent agent;
    Animator anim;
    Vector3 currentTarget;
    bool hasTarget;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponent<Animator>();

        agent.updateRotation = true;
        agent.autoBraking = true;
        agent.autoRepath = true;
    }

    void Update()
    {
        HandleClick();
        UpdateMovementAndAnimation();
    }

    void HandleClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Camera cam = Camera.main;
            if (!cam) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 500f, groundMask))
            {
                if (NavMesh.SamplePosition(hit.point, out NavMeshHit navHit, navmeshSampleMaxDist, NavMesh.AllAreas))
                {
                    currentTarget = navHit.position;
                    hasTarget = true;

                    agent.speed = runSpeed;
                    agent.isStopped = false;
                    agent.SetDestination(currentTarget);
                }
            }
        }
    }

    void UpdateMovementAndAnimation()
    {
        if (hasTarget && !agent.pathPending)
        {
            float remaining = agent.remainingDistance;

            if (remaining > slowDownRadius)
            {
                agent.speed = runSpeed;
            }
            else if (remaining > arriveThreshold)
            {
                agent.speed = walkSpeed;
            }

            if (remaining <= arriveThreshold)
            {
                hasTarget = false;
                agent.isStopped = true;
            }
        }

        float targetAnim;
        float v = agent.velocity.magnitude;

        if (v <= 0.05f) targetAnim = 0f;
        else if (agent.speed <= walkSpeed + 0.01f) targetAnim = 0.5f;
        else targetAnim = 1f;

        anim.SetFloat("Speed", targetAnim, 0.1f, Time.deltaTime);
    }

    void OnValidate()
    {
        if (arriveThreshold < 0.05f) arriveThreshold = 0.05f;
        if (slowDownRadius < arriveThreshold + 0.1f)
            slowDownRadius = arriveThreshold + 0.1f;
    }
}
