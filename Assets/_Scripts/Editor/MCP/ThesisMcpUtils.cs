using System;
using System.Globalization;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Thesis.Editor.Mcp
{
    /// <summary>
    /// Shared helpers for the thesis' custom MCP tools (ThesisGameObjectTool, LightCasterTool).
    /// </summary>
    internal static class ThesisMcpUtils
    {
        // --- Spawn placement state (objects created without an explicit position) ---
        private static Vector3 lastSpawnBase = Vector3.zero;
        private static int spawnCount = 0;
        private const float SpawnSpacing = 1f;
        private const int MaxSpawnsInRow = 5;

        private static Type _cachedXRGrabType;
        private static bool _xrGrabTypeSearched;

        // ---------------------------------------------------------------- Parsing

        /// <summary>Reads a float regardless of whether it arrived as a number or a string (culture-invariant).</summary>
        public static float? ReadFloat(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
                return token.Value<float>();
            if (token.Type == JTokenType.String &&
                float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                return f;
            return null;
        }

        /// <summary>Parses [x,y,z], {x,y,z} or a JSON string of either.</summary>
        public static Vector3? ReadVector3(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;
            if (token.Type == JTokenType.String)
            {
                try { token = JToken.Parse(token.ToString()); }
                catch { return null; }
            }
            return VectorParsing.ParseVector3(token);
        }

        /// <summary>
        /// Parses a colour from a name ("red"), hex ("#FF8800"), [r,g,b(,a)] in 0-1 (or 0-255) range, or {r,g,b,a}.
        /// </summary>
        public static bool TryReadColor(JToken token, out Color color)
        {
            color = Color.white;
            if (token == null || token.Type == JTokenType.Null)
                return false;

            if (token.Type == JTokenType.String)
            {
                string s = token.ToString().Trim();
                if (TryParseColorName(s, out color))
                    return true;
                if (ColorUtility.TryParseHtmlString(s.StartsWith("#") ? s : "#" + s, out color))
                    return true;
                try { token = JToken.Parse(s); }
                catch { return false; }
            }

            Color? parsed = VectorParsing.ParseColor(token);
            if (!parsed.HasValue)
                return false;

            Color c = parsed.Value;
            // Accept 0-255 values from the LLM as well
            if (c.r > 1f || c.g > 1f || c.b > 1f)
                c = new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a > 1f ? c.a / 255f : c.a);
            color = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
            return true;
        }

        public const string SupportedColorNames =
            "red, green, blue, yellow, orange, cyan, magenta, purple, pink, gold, white, warm white, cool white, black, gray/grey";

        public static bool TryParseColorName(string colorName, out Color color)
        {
            color = Color.white;
            switch (colorName?.Trim().ToLowerInvariant())
            {
                case "red": color = Color.red; return true;
                case "green": color = Color.green; return true;
                case "blue": color = Color.blue; return true;
                case "yellow": color = Color.yellow; return true;
                case "orange": color = new Color(1f, 0.647f, 0f); return true;
                case "cyan": color = Color.cyan; return true;
                case "magenta": color = Color.magenta; return true;
                case "purple": color = new Color(0.5f, 0f, 0.5f); return true;
                case "pink": color = new Color(1f, 0.753f, 0.796f); return true;
                case "gold": color = new Color(1f, 0.843f, 0f); return true;
                case "white": color = Color.white; return true;
                case "warm white": color = new Color(1f, 0.84f, 0.67f); return true;
                case "cool white": color = new Color(0.8f, 0.9f, 1f); return true;
                case "black": color = Color.black; return true;
                case "gray":
                case "grey": color = Color.gray; return true;
                default: return false;
            }
        }

        // ---------------------------------------------------------------- Lookup

        /// <summary>
        /// Finds a scene GameObject by instance ID, hierarchy path (contains '/') or name. Includes inactive objects.
        /// </summary>
        public static GameObject FindTarget(JToken target)
        {
            if (target == null || target.Type == JTokenType.Null)
                return null;

            string term = target.ToString().Trim();
            if (string.IsNullOrEmpty(term))
                return null;

            if (target.Type == JTokenType.Integer || int.TryParse(term, out _))
            {
                var byId = GameObjectLookup.FindByTarget(term, "by_id", includeInactive: true);
                if (byId != null)
                    return byId;
            }

            if (term.Contains("/"))
            {
                var byPath = GameObjectLookup.FindByTarget(term, "by_path", includeInactive: true);
                if (byPath != null)
                    return byPath;
            }

            var byName = GameObjectLookup.FindByTarget(term, "by_name", includeInactive: true);
            if (byName != null)
                return byName;

            // Forgiving fallback for voice input: case-insensitive name match
            return GameObjectLookup.GetAllSceneObjects(true)
                .FirstOrDefault(go => string.Equals(go.name, term, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Returns baseName, or "baseName N" if that name is already used in the scene.</summary>
        public static string MakeUniqueName(string baseName)
        {
            var existing = GameObjectLookup.GetAllSceneObjects(true).Select(go => go.name).ToHashSet();
            string name = baseName;
            int counter = 1;
            while (existing.Contains(name))
            {
                name = $"{baseName} {counter}";
                counter++;
            }
            return name;
        }

        // ---------------------------------------------------------------- Placement

        /// <summary>
        /// Position in front of the viewer: Camera.main (4m) in Play mode, the Scene view camera (2m) in Edit mode.
        /// Consecutive spawns from the same viewpoint are offset to the right so they don't overlap.
        /// </summary>
        public static Vector3 NextSpawnPosition()
        {
            Vector3 basePosition = Vector3.zero;
            Vector3 rightOffset = Vector3.right * SpawnSpacing;

            Transform viewer = null;
            float distance = 2f;
            if (Application.isPlaying)
            {
                if (Camera.main != null)
                {
                    viewer = Camera.main.transform;
                    distance = 4f;
                }
            }
            else if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                viewer = SceneView.lastActiveSceneView.camera.transform;
            }

            if (viewer != null)
            {
                basePosition = viewer.position + viewer.forward * distance;
                rightOffset = viewer.right * SpawnSpacing;
            }

            Vector3 finalPosition;
            if (spawnCount == 0 || Vector3.Distance(lastSpawnBase, basePosition) > 5f)
            {
                // First object, or the viewer moved significantly: start a new row
                finalPosition = basePosition;
                spawnCount = 1;
            }
            else
            {
                finalPosition = basePosition + rightOffset * spawnCount;
                spawnCount++;
            }

            if (spawnCount > MaxSpawnsInRow)
                spawnCount = 0;

            lastSpawnBase = basePosition;
            return finalPosition;
        }

        /// <summary>The viewer's horizontal right direction (Camera.main in Play mode, Scene view in Edit mode).</summary>
        public static Vector3 ViewerRight()
        {
            Transform viewer = null;
            if (Application.isPlaying && Camera.main != null)
                viewer = Camera.main.transform;
            else if (!Application.isPlaying && SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
                viewer = SceneView.lastActiveSceneView.camera.transform;

            if (viewer == null)
                return Vector3.right;

            Vector3 right = Vector3.ProjectOnPlane(viewer.right, Vector3.up);
            return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
        }

        /// <summary>World-space bounds from renderers, then colliders, else a 1m cube at the pivot.</summary>
        public static Bounds GetObjectBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    b.Encapsulate(renderers[i].bounds);
                return b;
            }

            var colliders = go.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                Bounds b = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                    b.Encapsulate(colliders[i].bounds);
                return b;
            }

            return new Bounds(go.transform.position, Vector3.one);
        }

        // ---------------------------------------------------------------- XR

        /// <summary>
        /// Adds MyXRGrabInteractable (or XRGrabInteractable as a fallback) so the object can be grabbed in VR.
        /// Only for objects with a collider; the grab component also adds the required Rigidbody.
        /// </summary>
        public static string TryAddXRGrab(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() == null)
                return "skipped (no collider)";

            if (!_xrGrabTypeSearched)
            {
                _cachedXRGrabType = GameObjectLookup.FindComponentType("MyXRGrabInteractable")
                    ?? GameObjectLookup.FindComponentType("UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable");
                _xrGrabTypeSearched = true;
            }

            if (_cachedXRGrabType == null)
            {
                Debug.LogWarning("[ThesisMcp] MyXRGrabInteractable / XRGrabInteractable type not found; VR grab not added.");
                return "skipped (type not found)";
            }

            if (go.GetComponent(_cachedXRGrabType) != null)
                return _cachedXRGrabType.Name;

            Undo.AddComponent(go, _cachedXRGrabType);
            return _cachedXRGrabType.Name;
        }

        // ---------------------------------------------------------------- Safety

        /// <summary>True for the XR rig (XROrigin component, or anything under an "XR Origin" object) and the EventSystem.</summary>
        public static bool IsProtected(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                string typeName = c.GetType().Name;
                if (typeName == "XROrigin" || typeName == "EventSystem" || typeName == "XRInteractionManager")
                    return true;
            }
            for (var t = go.transform; t != null; t = t.parent)
            {
                if (t.name.IndexOf("XR Origin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.GetComponent("XROrigin") != null)
                    return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- Misc

        public static void MarkDirty(GameObject go)
        {
            EditorUtility.SetDirty(go);
            if (!Application.isPlaying && go.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(go.scene);
        }

        /// <summary>Compact result payload; keeps responses short for the local LLM.</summary>
        public static object Describe(GameObject go)
        {
            var t = go.transform;
            return new
            {
                name = go.name,
                instanceID = go.GetInstanceID(),
                path = GameObjectLookup.GetGameObjectPath(go),
                position = new[] { Round(t.position.x), Round(t.position.y), Round(t.position.z) },
                rotation = new[] { Round(t.eulerAngles.x), Round(t.eulerAngles.y), Round(t.eulerAngles.z) },
                scale = new[] { Round(t.localScale.x), Round(t.localScale.y), Round(t.localScale.z) },
            };
        }

        public static string Fmt(Vector3 v) =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);

        private static float Round(float f) => (float)Math.Round(f, 3);
    }
}
