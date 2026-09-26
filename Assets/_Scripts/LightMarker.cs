using UnityEngine;

/// <summary>
/// Play-mode "gizmo" for a Light: a small glowing bulb in the light's color, a camera-facing ring,
/// and (for spot lights) a direction line. Pulses for a few seconds after it appears so a newly
/// created light is easy to spot in the Game view / VR headset, where editor gizmos are not drawn.
///
/// The visuals are created at runtime only and never saved into the scene. They have no collider
/// and cast no shadows, so they don't change the lighting.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public class LightMarker : MonoBehaviour
{
    [Tooltip("Diameter of the glowing bulb in meters.")]
    [SerializeField] private float bulbSize = 0.12f;

    [Tooltip("Radius of the camera-facing ring in meters.")]
    [SerializeField] private float ringRadius = 0.2f;

    [Tooltip("Line width of the ring and spot direction line in meters.")]
    [SerializeField] private float lineWidth = 0.012f;

    [Tooltip("How long the marker pulses after it appears (seconds). 0 = no pulse.")]
    [SerializeField] private float highlightSeconds = 3f;

    [Tooltip("Hide the marker after the highlight ends. Off = the marker stays visible.")]
    [SerializeField] private bool hideAfterHighlight = true;

    private const int RingSegments = 40;

    private Light targetLight;
    private Transform root;
    private Transform ring;
    private Transform bulb;
    private LineRenderer ringLine;
    private LineRenderer directionLine;
    private Material bulbMaterial;
    private Material lineMaterial;
    private float startTime;

    private void Start()
    {
        targetLight = GetComponent<Light>();
        BuildVisuals();
        startTime = Time.time;
        SyncWithLight();
    }

    private void LateUpdate()
    {
        if (root == null || targetLight == null)
            return;

        SyncWithLight();
        FaceCamera();
        AnimateHighlight();
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
        if (bulbMaterial != null) Destroy(bulbMaterial);
        if (lineMaterial != null) Destroy(lineMaterial);
    }

    // ------------------------------------------------------------------ Build

    private const string RootName = "LightMarker (runtime)";

    private void BuildVisuals()
    {
        // A duplicated light also copies the original's marker visuals; remove them and build our own
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name == RootName)
                Destroy(child.gameObject);
        }

        root = new GameObject(RootName).transform;
        root.gameObject.hideFlags = HideFlags.DontSave;
        root.SetParent(transform, false);

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        bulbMaterial = new Material(unlit);
        lineMaterial = new Material(unlit);

        // Bulb
        var bulbGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bulbGo.name = "Bulb";
        bulbGo.hideFlags = HideFlags.DontSave;
        DestroyImmediate(bulbGo.GetComponent<Collider>());
        bulb = bulbGo.transform;
        bulb.SetParent(root, false);
        bulb.localScale = Vector3.one * bulbSize;
        var bulbRenderer = bulbGo.GetComponent<MeshRenderer>();
        bulbRenderer.sharedMaterial = bulbMaterial;
        MakeInvisibleToLighting(bulbRenderer);

        // Camera-facing ring
        var ringGo = new GameObject("Ring") { hideFlags = HideFlags.DontSave };
        ring = ringGo.transform;
        ring.SetParent(root, false);
        ringLine = CreateLine(ringGo, RingSegments, loop: true, worldSpace: false);
        for (int i = 0; i < RingSegments; i++)
        {
            float a = i * Mathf.PI * 2f / RingSegments;
            ringLine.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ringRadius);
        }

        // Direction line for spot lights
        if (targetLight.type == LightType.Spot)
        {
            var dirGo = new GameObject("Direction") { hideFlags = HideFlags.DontSave };
            dirGo.transform.SetParent(root, false);
            directionLine = CreateLine(dirGo, 2, loop: false, worldSpace: false);
        }
    }

    private LineRenderer CreateLine(GameObject go, int points, bool loop, bool worldSpace)
    {
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.positionCount = points;
        line.loop = loop;
        line.useWorldSpace = worldSpace;
        line.widthMultiplier = lineWidth;
        line.numCapVertices = 2;
        MakeInvisibleToLighting(line);
        return line;
    }

    private static void MakeInvisibleToLighting(Renderer r)
    {
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    // ------------------------------------------------------------------ Update

    private void SyncWithLight()
    {
        root.gameObject.SetActive(targetLight.enabled && (!hideAfterHighlight || Time.time - startTime < highlightSeconds));

        Color c = targetLight.color;
        c.a = 1f;
        SetColor(bulbMaterial, c);
        SetColor(lineMaterial, Color.Lerp(c, Color.white, 0.35f));

        if (directionLine != null)
        {
            float length = Mathf.Clamp(targetLight.range * 0.25f, 0.3f, 1.5f);
            directionLine.SetPosition(0, Vector3.zero);
            directionLine.SetPosition(1, Vector3.forward * length);
        }
    }

    private void FaceCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 toCamera = cam.transform.position - ring.position;
        if (toCamera.sqrMagnitude > 0.0001f)
            ring.rotation = Quaternion.LookRotation(-toCamera, cam.transform.up);
    }

    private void AnimateHighlight()
    {
        float t = Time.time - startTime;
        float scale = 1f;
        if (highlightSeconds > 0f && t < highlightSeconds)
        {
            // Starts large and pulses while shrinking back to normal size
            float fade = 1f - t / highlightSeconds;
            scale = 1f + fade * (1.5f + 0.5f * Mathf.Sin(t * 10f));
        }
        ring.localScale = Vector3.one * scale;
    }

    private static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    // ------------------------------------------------------------------ Scene view

#if UNITY_EDITOR
    /// <summary>
    /// Same bulb / ring / direction look in the Scene view. Drawn in Edit mode, and in Play mode
    /// whenever the runtime marker is hidden, so the two never draw on top of each other.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (root != null && root.gameObject.activeInHierarchy)
            return;

        var l = targetLight != null ? targetLight : GetComponent<Light>();
        if (l == null || !l.enabled)
            return;

        Color c = l.color;
        c.a = 1f;
        Vector3 pos = transform.position;

        Gizmos.color = c;
        Gizmos.DrawSphere(pos, bulbSize * 0.5f);

        UnityEditor.Handles.color = Color.Lerp(c, Color.white, 0.35f);
        var sceneCam = UnityEditor.SceneView.currentDrawingSceneView != null
            ? UnityEditor.SceneView.currentDrawingSceneView.camera
            : null;
        Vector3 normal = sceneCam != null ? (sceneCam.transform.position - pos).normalized : Vector3.forward;
        UnityEditor.Handles.DrawWireDisc(pos, normal, ringRadius, 2f);

        if (l.type == LightType.Spot)
        {
            float length = Mathf.Clamp(l.range * 0.25f, 0.3f, 1.5f);
            UnityEditor.Handles.DrawLine(pos, pos + transform.forward * length, 2f);
        }
    }
#endif
}
