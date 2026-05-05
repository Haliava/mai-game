using UnityEngine;

public class JumpExecutor : MonoBehaviour
{
    [SerializeField] SurfaceWalkerMotor motor;
    [SerializeField] JumpPlanner planner;
    [SerializeField] LayerMask walkableMask = ~0;
    [SerializeField] float landingProbeRadius = 0.35f;

    JumpPlan activePlan;
    float jumpTime;
    bool jumping;

    public bool IsJumping { get { return jumping; } }
    public JumpPlan ActivePlan { get { return activePlan; } }

    public void Configure(SurfaceWalkerMotor headMotor, JumpPlanner jumpPlanner, LayerMask mask)
    {
        motor = headMotor;
        planner = jumpPlanner;
        walkableMask = mask;
    }

    public void StartJump(JumpPlan plan)
    {
        activePlan = plan;
        jumpTime = 0f;
        jumping = true;
    }

    public bool Tick(float deltaTime)
    {
        if (!jumping || motor == null) return false;
        jumpTime += deltaTime;
        float duration = Mathf.Max(0.01f, activePlan.Duration);
        float t = Mathf.Clamp01(jumpTime / duration);

        Vector3 position = JumpPlanner.Evaluate(activePlan, t);
        Vector3 forward = JumpPlanner.Derivative(activePlan, t);
        Vector3 normal = Vector3.Slerp(activePlan.StartNormal, activePlan.EndNormal, t).normalized;
        motor.SetPose(position, forward, normal, t < 0.98f);

        if (t < 1f) return true;

        RaycastHit landingHit;
        float searchRadius = planner != null ? planner.LandingSearchRadius : 4f;
        if (SurfaceProbe.TryFindSurfaceAround(activePlan.EndPoint, transform.root, walkableMask, landingProbeRadius, searchRadius, out landingHit))
        {
            motor.SetPose(landingHit.point + landingHit.normal * motor.SurfaceOffset, forward, landingHit.normal, false);
        }
        else
        {
            motor.SetPose(activePlan.EndPoint, forward, activePlan.EndNormal, false);
        }

        jumping = false;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (!jumping) return;
        Gizmos.color = Color.yellow;
        Vector3 previous = activePlan.StartPoint;
        for (int i = 1; i <= 20; i++)
        {
            float t = i / 20f;
            Vector3 point = JumpPlanner.Evaluate(activePlan, t);
            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
