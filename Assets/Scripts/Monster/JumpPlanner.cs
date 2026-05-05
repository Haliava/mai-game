using UnityEngine;

public struct JumpPlan
{
    public Vector3 StartPoint;
    public Vector3 ControlPoint;
    public Vector3 EndPoint;
    public Vector3 StartNormal;
    public Vector3 EndNormal;
    public float Duration;
    public SurfaceNavigationNode TargetNode;
}

public class JumpPlanner : MonoBehaviour
{
    [SerializeField] float minJumpArcHeight = 2f;
    [SerializeField] float jumpArcHeightPerMeter = 0.15f;
    [SerializeField] float maxJumpArcHeight = 20f;
    [SerializeField] float jumpDurationPerMeter = 0.06f;
    [SerializeField] float minJumpDuration = 0.45f;
    [SerializeField] float maxJumpDuration = 3.5f;
    [SerializeField] float landingSearchRadius = 4f;

    public float LandingSearchRadius { get { return landingSearchRadius; } }

    public JumpPlan CreatePlan(Vector3 start, Vector3 end, Vector3 startNormal, Vector3 endNormal, SurfaceNavigationNode targetNode)
    {
        float distance = Vector3.Distance(start, end);
        float arcHeight = Mathf.Clamp(
            minJumpArcHeight + distance * jumpArcHeightPerMeter,
            minJumpArcHeight,
            maxJumpArcHeight);

        float duration = Mathf.Clamp(
            distance * jumpDurationPerMeter,
            minJumpDuration,
            maxJumpDuration);

        Vector3 arcDirection = startNormal.normalized + endNormal.normalized;
        if (arcDirection.sqrMagnitude < 0.001f) arcDirection = Vector3.up;
        arcDirection.Normalize();

        JumpPlan plan = new JumpPlan();
        plan.StartPoint = start;
        plan.EndPoint = end;
        plan.ControlPoint = (start + end) * 0.5f + arcDirection * arcHeight;
        plan.StartNormal = startNormal.sqrMagnitude > 0.001f ? startNormal.normalized : Vector3.up;
        plan.EndNormal = endNormal.sqrMagnitude > 0.001f ? endNormal.normalized : Vector3.up;
        plan.Duration = duration;
        plan.TargetNode = targetNode;
        return plan;
    }

    public static Vector3 Evaluate(JumpPlan plan, float t)
    {
        float u = 1f - t;
        return u * u * plan.StartPoint + 2f * u * t * plan.ControlPoint + t * t * plan.EndPoint;
    }

    public static Vector3 Derivative(JumpPlan plan, float t)
    {
        return 2f * (1f - t) * (plan.ControlPoint - plan.StartPoint) + 2f * t * (plan.EndPoint - plan.ControlPoint);
    }
}
