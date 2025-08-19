using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// FloatingUI combines three behaviors in one component:
/// 1) Lazy follow of a target with smoothing.
/// 2) A dwell based callout toggle that faces the camera.
/// 3) A Bezier line between two points with optional animated gradient.
///
/// This merges and simplifies functionality inspired by separate scripts.
/// </summary>
[AddComponentMenu("UI/Floating UI (Follow + Callout + Bezier)")]
public class FloatingUI : MonoBehaviour
{
    // =========================
    // Target and follow settings
    // =========================
    public enum PositionFollowMode { None, Follow }
    public enum RotationFollowMode { None, LookAt, LookAtWithWorldUp, Follow }

    [Header("Target Config")]
    [Tooltip("The object to follow. Defaults to main camera if not set.")]
    [SerializeField] Transform target;

    [Tooltip("Offset from the target. Interpreted in target local space when Apply Offset In Local Space is true.")]
    [SerializeField] Vector3 targetOffset = new Vector3(0f, 0f, 0.5f);

    [Tooltip("If true, read target local transform when following position. LookAt modes always use world space.")]
    [SerializeField] bool followInLocalSpace = false;

    [Tooltip("If true, apply the target offset in local space. If false, apply in world space.")]
    [SerializeField] bool applyTargetInLocalSpace = false;

    [Header("General Follow Params")]
    [Tooltip("Movement speed for smoothing to new target.")]
    [SerializeField] float movementSpeed = 6f;

    [Range(0f, 0.999f)]
    [Tooltip("Adjust movement speed based on distance. 0 for constant speed.")]
    [SerializeField] float movementSpeedVariancePercentage = 0.25f;

    [Tooltip("Snap to target when enabled.")]
    [SerializeField] bool snapOnEnable = true;

    [Header("Position Follow Params")]
    [SerializeField] PositionFollowMode positionFollowMode = PositionFollowMode.Follow;

    [Tooltip("Minimum distance before position follow starts.")]
    [SerializeField] float minDistanceAllowed = 0.01f;

    [Tooltip("Maximum distance threshold when time threshold is reached.")]
    [SerializeField] float maxDistanceAllowed = 0.3f;

    [Tooltip("Seconds before threshold expands from min distance to max distance.")]
    [SerializeField] float timeUntilThresholdReachesMaxDistance = 3f;

    [Header("Rotation Follow Params")]
    [SerializeField] RotationFollowMode rotationFollowMode = RotationFollowMode.LookAt;

    [Tooltip("Minimum angle before rotation follow starts.")]
    [SerializeField] float minAngleAllowed = 0.1f;

    [Tooltip("Maximum angle threshold when time threshold is reached.")]
    [SerializeField] float maxAngleAllowed = 5f;

    [Tooltip("Seconds before angle threshold expands from min to max.")]
    [SerializeField] float timeUntilThresholdReachesMaxAngle = 3f;

    // Smart threshold timers
    float posThresholdTimer;
    float rotThresholdTimer;

    // Cached speeds for variance
    float lowerMovementSpeed;
    float upperMovementSpeed;

    // =========================
    // Callout settings
    // =========================
    [Header("Callout / Tooltip")]
    [Tooltip("Tooltip root to toggle after dwell.")]
    [SerializeField] Transform tooltip;

    [Tooltip("Optional curve GameObject to toggle with tooltip.")]
    [SerializeField] GameObject curveObject;

    [Tooltip("Seconds to dwell before showing tooltip.")]
    [SerializeField] float dwellTime = 1f;

    [Tooltip("Unparent the tooltip at Start.")]
    [SerializeField] bool unparentTooltipOnStart = true;

    [Tooltip("Disable tooltip and curve at Start.")]
    [SerializeField] bool turnOffAtStart = true;

    [Tooltip("Canvas Transform to rotate so it faces the main camera.")]
    [SerializeField] Transform canvasToFace;

    bool gazing;
    Coroutine startCo;
    Coroutine endCo;
    Camera mainCamera;

    // =========================
    // Bezier settings
    // =========================
    [Header("Bezier Line")]
    [Tooltip("Optional text to display the current target name.")]
    [SerializeField] TMP_Text objectNameText;

    [Tooltip("Start anchor for the curve. If not set, will use computed start based on target.")]
    [SerializeField] Transform startPoint;

    [Tooltip("End anchor for the curve. If not set, will be computed from start plus End Offset.")]
    [SerializeField] Transform endPoint;

    [Tooltip("Offset from start when endPoint is not provided.")]
    [SerializeField] Vector3 endOffset = new Vector3(1f, 0.7f, -0.5f);

    [Tooltip("Controls the scale factor of the curve start handle.")]
    [SerializeField] float curveFactorStart = 1.0f;

    [Tooltip("Controls the scale factor of the curve end handle.")]
    [SerializeField] float curveFactorEnd = 1.0f;

    [Tooltip("Number of segments used to draw the curve.")]
    [SerializeField] int segmentCount = 50;

    [Tooltip("Animate the line color gradient so an opaque part travels along the line.")]
    [SerializeField] bool animateGradient = false;

    [Tooltip("Speed of the gradient animation.")]
    [SerializeField] float animSpeed = 0.25f;

    [Tooltip("Main opaque color of the gradient when animated.")]
    [SerializeField] Color gradientKeyColor = new Color(0.1254902f, 0.5882353f, 0.9529412f);

    [Tooltip("LineRenderer that draws the curve. If null, will use one on this GameObject.")]
    [SerializeField] LineRenderer lineRenderer;

    Vector3[] bezierControls = new Vector3[4];
    Vector3 lastStartPos;
    Vector3 lastEndPos;
    float gradTime;

    // =========================
    // Unity lifecycle
    // =========================
    void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        UpdateSpeedBounds();
    }

    void OnEnable()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (target == null && mainCamera != null)
            target = mainCamera.transform;

        if (unparentTooltipOnStart && tooltip != null)
            tooltip.SetParent(null);

        if (turnOffAtStart)
        {
            if (tooltip != null)
                tooltip.gameObject.SetActive(false);
            if (curveObject != null)
                curveObject.SetActive(false);
        }

        if (snapOnEnable)
        {
            var pos = ComputeTargetPosition();
            if (applyTargetInLocalSpace)
                transform.localPosition = pos;
            else
                transform.position = pos;

            var rot = ComputeTargetRotation();
            if (applyTargetInLocalSpace)
                transform.localRotation = rot;
            else
                transform.rotation = rot;
        }

        DrawCurve();
    }

    void LateUpdate()
    {
        // Face canvas toward camera
        if (mainCamera != null && canvasToFace != null)
        {
            canvasToFace.LookAt(mainCamera.transform.position, Vector3.up);
            canvasToFace.Rotate(0, 180, 0);
        }

        // Follow logic
        var dt = Time.unscaledDeltaTime;

        if (positionFollowMode != PositionFollowMode.None)
        {
            posThresholdTimer += dt;
            var targetPos = ComputeTargetPosition();
            var curPos = applyTargetInLocalSpace ? transform.localPosition : transform.position;

            var curThreshold = Mathf.Lerp(minDistanceAllowed, maxDistanceAllowed, Mathf.Clamp01(posThresholdTimer / timeUntilThresholdReachesMaxDistance));
            var dist = Vector3.Distance(curPos, targetPos);

            if (dist > curThreshold)
                posThresholdTimer = 0f;

            var speed = movementSpeed;
            if (movementSpeedVariancePercentage > 0f)
            {
                UpdateSpeedBounds();
                var t = Mathf.InverseLerp(0f, curThreshold, dist);
                speed = Mathf.Lerp(lowerMovementSpeed, upperMovementSpeed, t);
            }

            var newPos = Vector3.MoveTowards(curPos, targetPos, speed * dt);
            if (applyTargetInLocalSpace)
                transform.localPosition = newPos;
            else
                transform.position = newPos;
        }

        if (rotationFollowMode != RotationFollowMode.None)
        {
            rotThresholdTimer += dt;
            var targetRot = ComputeTargetRotation();
            var curRot = applyTargetInLocalSpace ? transform.localRotation : transform.rotation;

            var curAngThreshold = Mathf.Lerp(minAngleAllowed, maxAngleAllowed, Mathf.Clamp01(rotThresholdTimer / timeUntilThresholdReachesMaxAngle));
            var ang = Quaternion.Angle(curRot, targetRot);
            if (ang > curAngThreshold)
                rotThresholdTimer = 0f;

            var speed = movementSpeed;
            if (movementSpeedVariancePercentage > 0f)
            {
                UpdateSpeedBounds();
                var t = Mathf.InverseLerp(0f, curAngThreshold, ang);
                speed = Mathf.Lerp(lowerMovementSpeed, upperMovementSpeed, t);
            }

            var newRot = Quaternion.RotateTowards(curRot, targetRot, speed * 90f * dt);
            if (applyTargetInLocalSpace)
                transform.localRotation = newRot;
            else
                transform.rotation = newRot;
        }

        // Bezier update and optional animation
        DrawCurve();
        if (animateGradient)
            AnimateCurveGradient();
    }

    // =========================
    // Public API for callout
    // =========================
    public void GazeHoverStart()
    {
        gazing = true;
        if (startCo != null) StopCoroutine(startCo);
        if (endCo != null) StopCoroutine(endCo);
        startCo = StartCoroutine(StartDwell());
    }

    public void GazeHoverEnd()
    {
        gazing = false;
        if (endCo != null) StopCoroutine(endCo);
        endCo = StartCoroutine(EndDwell());
    }

    IEnumerator StartDwell()
    {
        yield return new WaitForSeconds(dwellTime);
        if (gazing)
            SetCalloutActive(true);
    }

    IEnumerator EndDwell()
    {
        if (!gazing)
            SetCalloutActive(false);
        yield return null;
    }

    void SetCalloutActive(bool active)
    {
        if (tooltip != null)
            tooltip.gameObject.SetActive(active);
        if (curveObject != null)
            curveObject.SetActive(active);
    }

    // =========================
    // Bezier helpers
    // =========================
    [ContextMenu("Draw Bezier Now")]
    public void DrawCurve()
    {
        if (lineRenderer == null)
            return;

        // Determine start and end
        Vector3 startPos = startPoint != null ? startPoint.position : ComputeStartForCurve();
        Vector3 endPos = endPoint != null ? endPoint.position : startPos + endOffset;

        if (objectNameText != null && target != null)
            objectNameText.text = target.name;

        if (startPos == lastStartPos && endPos == lastEndPos)
            return;

        var dist = Vector3.Distance(startPos, endPos);

        bezierControls[0] = startPos;
        // Use right vectors from either anchors or this transform as handle directions
        var startRight = startPoint != null ? startPoint.right : transform.right;
        var endRight = endPoint != null ? endPoint.right : transform.right;
        bezierControls[1] = startPos + (startRight * (dist * curveFactorStart));
        bezierControls[2] = endPos - (endRight * (dist * curveFactorEnd));
        bezierControls[3] = endPos;

        int segs = Mathf.Max(2, segmentCount);
        lineRenderer.positionCount = segs + 1;
        lineRenderer.SetPosition(0, bezierControls[0]);
        for (int i = 1; i <= segs; i++)
        {
            float t = i / (float)segs;
            var p = CubicBezier(t, bezierControls[0], bezierControls[1], bezierControls[2], bezierControls[3]);
            lineRenderer.SetPosition(i, p);
        }

        lastStartPos = startPos;
        lastEndPos = endPos;
    }

    static Vector3 CubicBezier(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;
        Vector3 p = uuu * p0;
        p += 3f * uu * t * p1;
        p += 3f * u * tt * p2;
        p += ttt * p3;
        return p;
    }

    void AnimateCurveGradient()
    {
        var grad = new Gradient();
        var colorKeys = new GradientColorKey[1];
        var alphaKeys = new GradientAlphaKey[2];
        colorKeys[0] = new GradientColorKey(gradientKeyColor, 0f);
        alphaKeys[0] = new GradientAlphaKey(.25f, gradTime);
        alphaKeys[1] = new GradientAlphaKey(1f, 1f);
        grad.SetKeys(colorKeys, alphaKeys);
        grad.mode = GradientMode.Blend;
        lineRenderer.colorGradient = grad;
        gradTime += Time.unscaledDeltaTime * animSpeed;
        if (gradTime >= 1f) gradTime = 0f;
    }

    Vector3 ComputeStartForCurve()
    {
        if (target != null)
            return target.position;
        return transform.position;
    }

    // =========================
    // Follow helpers
    // =========================
    Vector3 ComputeTargetPosition()
    {
        if (target == null)
            return applyTargetInLocalSpace ? transform.localPosition : transform.position;

        if (positionFollowMode == PositionFollowMode.None)
            return applyTargetInLocalSpace ? transform.localPosition : transform.position;

        if (followInLocalSpace)
            return target.localPosition + targetOffset;

        // world space, offset applied in chosen space
        return applyTargetInLocalSpace
            ? target.position + targetOffset
            : target.position + target.TransformVector(targetOffset);
    }

    Quaternion ComputeTargetRotation()
    {
        if (target == null)
            return applyTargetInLocalSpace ? transform.localRotation : transform.rotation;

        switch (rotationFollowMode)
        {
            case RotationFollowMode.None:
                return applyTargetInLocalSpace ? transform.localRotation : transform.rotation;
            case RotationFollowMode.Follow:
                return followInLocalSpace ? target.localRotation : target.rotation;
            case RotationFollowMode.LookAt:
            case RotationFollowMode.LookAtWithWorldUp:
                {
                    var forward = (transform.position - target.position).normalized;
                    if (rotationFollowMode == RotationFollowMode.LookAtWithWorldUp)
                        return Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, Vector3.up), Vector3.up);
                    return Quaternion.LookRotation(forward, Vector3.up);
                }
            default:
                return transform.rotation;
        }
    }

    void UpdateSpeedBounds()
    {
        if (movementSpeedVariancePercentage > 0f)
        {
            lowerMovementSpeed = movementSpeed - movementSpeedVariancePercentage * movementSpeed;
            upperMovementSpeed = movementSpeed * (1f + movementSpeedVariancePercentage);
        }
        else
        {
            lowerMovementSpeed = movementSpeed;
            upperMovementSpeed = movementSpeed;
        }
    }
}
