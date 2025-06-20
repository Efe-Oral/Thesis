using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutomaticDescriptor : MonoBehaviour
{
    public float rayLength = 10f;
    private LineRenderer lineRenderer;
    private Camera mainCamera;
    private GameObject lastHitObject;
    public Vector3 rayCameraDistance = new Vector3(0, 0, 10);

    // Start is called before the first frame update
    void Start()
    {
        mainCamera = Camera.main;
        InitializeLineRenderer();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateRaycast();
    }

    private void InitializeLineRenderer()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.01f;
        lineRenderer.endWidth = 0.01f;
        lineRenderer.positionCount = 2;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = Color.green;
        lineRenderer.endColor = Color.yellow;
    }

    private void UpdateRaycast()
    {
        if (mainCamera == null) return;
        Vector3 origin = rayCameraDistance + mainCamera.transform.position;
        Vector3 direction = mainCamera.transform.forward;
        Ray ray = new Ray(origin, direction);
        RaycastHit hit;
        Vector3 endPosition = origin + direction * rayLength;

        if (Physics.Raycast(ray, out hit, rayLength))
        {
            endPosition = hit.point;
            HandleRaycastHit(hit);
        }
        else
        {
            lastHitObject = null;
        }

        DrawRay(origin, endPosition);
    }

    private void DrawRay(Vector3 start, Vector3 end)
    {
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    private void HandleRaycastHit(RaycastHit hit)
    {
        Debug.Log("Looking at: " + hit.collider.gameObject.name);
        lastHitObject = hit.collider.gameObject;
    }

    private void OnDrawGizmos()
    {
        if (lastHitObject != null)
        {
            Debug.Log("Last hit object is NOT null");
            Gizmos.color = Color.yellow;
            var meshRenderer = lastHitObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                Gizmos.matrix = lastHitObject.transform.localToWorldMatrix;
                Bounds bounds = meshRenderer.bounds;
                Gizmos.DrawWireCube(bounds.center - lastHitObject.transform.position, bounds.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}
