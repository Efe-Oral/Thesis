using System;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Thesis.Editor.Mcp
{
    /// <summary>
    /// Thesis-specific GameObject actions that the stock manage_gameobject tool does not cover:
    ///   create - spawns in front of the viewer, auto-names primitives, makes objects grabbable in VR
    ///   place  - puts an object on top of / below / next to another one, using their real sizes
    ///   scale  - relative scaling ("bigger", "smaller", x times) or an exact scale
    ///   color  - recolors an object (named colour, hex or rgb)
    ///   delete - removes an object (refuses camera rig / voice pipeline objects)
    /// Ported from the old ManageGameObject override (UnityMcpBridge era).
    /// </summary>
    [McpForUnityTool("thesis_gameobject", Description =
        "Thesis VR scene tool. Actions: " +
        "'create' spawns a primitive (cube, sphere, capsule, cylinder, plane, quad) or prefab in front of the user, " +
        "auto-named and grabbable in VR (position optional). " +
        "'place' moves 'target' relative to 'reference_object' using their sizes: on/top/above, below/under, front, behind/back, left, right. " +
        "'scale' resizes 'target': scale_command bigger/smaller/reset with optional multiplier, or exact 'scale' [x,y,z]. " +
        "'color' changes the color of 'target' (color_name or color [r,g,b]). " +
        "'duplicate' makes one copy of 'target' next to it (works for any object, including lights). " +
        "'delete' removes 'target'. " +
        "For lights use the light_caster tool.")]
    public static class ThesisGameObjectTool
    {
        public class Parameters
        {
            [ToolParameter("Action: 'create', 'place', 'scale', 'color', 'duplicate' or 'delete'.")]
            public string action { get; set; }

            [ToolParameter("Name, path or instance ID of the object to place/scale/color/duplicate/delete.", Required = false)]
            public string target { get; set; }

            [ToolParameter("color: named color (" + ThesisMcpUtils.SupportedColorNames + ") or hex like #FF8800.", Required = false)]
            public string color_name { get; set; }

            [ToolParameter("color: color as [r,g,b] with values 0-1.", Required = false)]
            public float[] color { get; set; }

            [ToolParameter("create: name of the new object. Optional for primitives (auto 'Cube', 'Cube 1', ...).", Required = false)]
            public string name { get; set; }

            [ToolParameter("create: cube, sphere, capsule, cylinder, plane or quad.", Required = false)]
            public string primitive_type { get; set; }

            [ToolParameter("create: prefab asset path or prefab name to instantiate instead of a primitive.", Required = false)]
            public string prefab_path { get; set; }

            [ToolParameter("create: world position [x,y,z]. If omitted the object appears in front of the user.", Required = false)]
            public float[] position { get; set; }

            [ToolParameter("create: euler rotation [x,y,z] in degrees.", Required = false)]
            public float[] rotation { get; set; }

            [ToolParameter("create: initial scale [x,y,z]. scale: exact new scale [x,y,z].", Required = false)]
            public float[] scale { get; set; }

            [ToolParameter("create: parent object name or path.", Required = false)]
            public string parent { get; set; }

            [ToolParameter("create: add the VR grab component (default true).", Required = false, DefaultValue = "true")]
            public bool? grabbable { get; set; }

            [ToolParameter("place: the object to place 'target' relative to.", Required = false)]
            public string reference_object { get; set; }

            [ToolParameter("place: on, top, above, below, under, front, behind, back, left or right (default right).", Required = false)]
            public string relative_position { get; set; }

            [ToolParameter("place: extra world offset [x,y,z] added after placement.", Required = false)]
            public float[] offset { get; set; }

            [ToolParameter("scale: 'bigger', 'smaller' or 'reset'.", Required = false)]
            public string scale_command { get; set; }

            [ToolParameter("scale: factor for bigger/smaller (default 2). E.g. 3 = three times bigger.", Required = false)]
            public float? multiplier { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("Parameters cannot be null.");

            var p = new ToolParams(@params);
            string action = p.Get("action")?.Trim().ToLowerInvariant();

            try
            {
                switch (action)
                {
                    case "create":
                        return Create(p);
                    case "place":
                    case "move": // old override action name
                        return Place(p);
                    case "scale":
                        return Scale(p);
                    case "color":
                    case "colour":
                    case "change_color": // old override action name
                        return Recolor(p);
                    case "duplicate":
                    case "copy":
                    case "clone":
                        return Duplicate(p);
                    case "delete":
                    case "remove":
                        return Delete(p);
                    case null:
                    case "":
                        return new ErrorResponse("'action' is required: create, place, scale, color, duplicate or delete.");
                    default:
                        return new ErrorResponse($"Unknown action '{action}'. Valid actions: create, place, scale, color, duplicate, delete.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[thesis_gameobject] Action '{action}' failed: {e}");
                return new ErrorResponse($"Error during '{action}': {e.Message}");
            }
        }

        // ------------------------------------------------------------------ create

        private static object Create(ToolParams p)
        {
            string name = p.Get("name");
            string primitiveType = p.Get("primitive_type");
            string prefabPath = p.Get("prefab_path");
            GameObject go = null;

            if (!string.IsNullOrEmpty(prefabPath))
            {
                string resolved = ResolvePrefabPath(prefabPath, out string error);
                if (resolved == null)
                    return new ErrorResponse(error);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(resolved);
                if (prefab == null)
                    return new ErrorResponse($"Prefab not found at '{resolved}'.");

                go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.name = ThesisMcpUtils.MakeUniqueName(string.IsNullOrEmpty(name) ? prefab.name : name);
            }
            else if (!string.IsNullOrEmpty(primitiveType))
            {
                if (!Enum.TryParse(primitiveType.Trim(), true, out PrimitiveType type))
                {
                    return new ErrorResponse(
                        $"Invalid primitive_type '{primitiveType}'. Valid: {string.Join(", ", Enum.GetNames(typeof(PrimitiveType)))}.");
                }
                go = GameObject.CreatePrimitive(type);
                go.name = ThesisMcpUtils.MakeUniqueName(string.IsNullOrEmpty(name) ? type.ToString() : name);
            }
            else
            {
                if (string.IsNullOrEmpty(name))
                    return new ErrorResponse("Provide 'primitive_type', 'prefab_path' or a 'name' for an empty object.");
                go = new GameObject(ThesisMcpUtils.MakeUniqueName(name));
            }

            Undo.RegisterCreatedObjectUndo(go, $"Create '{go.name}'");

            // Parent first so an explicit position is interpreted in world space consistently
            string parentName = p.Get("parent");
            if (!string.IsNullOrEmpty(parentName) &&
                !parentName.Equals("none", StringComparison.OrdinalIgnoreCase) &&
                !parentName.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                var parentGo = ThesisMcpUtils.FindTarget(parentName);
                if (parentGo == null)
                {
                    Undo.DestroyObjectImmediate(go);
                    return new ErrorResponse($"Parent '{parentName}' not found.");
                }
                Undo.SetTransformParent(go.transform, parentGo.transform, "Set Parent");
            }

            Vector3? position = ThesisMcpUtils.ReadVector3(p.GetRaw("position"));
            go.transform.position = position ?? ThesisMcpUtils.NextSpawnPosition();

            Vector3? rotation = ThesisMcpUtils.ReadVector3(p.GetRaw("rotation"));
            if (rotation.HasValue)
                go.transform.eulerAngles = rotation.Value;

            Vector3? scale = ThesisMcpUtils.ReadVector3(p.GetRaw("scale"));
            if (scale.HasValue)
                go.transform.localScale = scale.Value;

            string grab = p.GetBool("grabbable", true) ? ThesisMcpUtils.TryAddXRGrab(go) : "disabled";

            ThesisMcpUtils.MarkDirty(go);
            Selection.activeGameObject = go;

            return new SuccessResponse(
                $"Created '{go.name}' at {ThesisMcpUtils.Fmt(go.transform.position)} (VR grab: {grab}).",
                ThesisMcpUtils.Describe(go));
        }

        private static string ResolvePrefabPath(string prefabPath, out string error)
        {
            error = null;
            if (prefabPath.Contains("/"))
                return prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ? prefabPath : prefabPath + ".prefab";

            string[] guids = AssetDatabase.FindAssets($"t:Prefab {prefabPath}");
            var paths = guids.Select(AssetDatabase.GUIDToAssetPath).ToList();
            var exact = paths.Where(pp => System.IO.Path.GetFileNameWithoutExtension(pp)
                .Equals(prefabPath, StringComparison.OrdinalIgnoreCase)).ToList();

            if (exact.Count == 1) return exact[0];
            if (paths.Count == 1) return paths[0];

            error = paths.Count == 0
                ? $"No prefab named '{prefabPath}' found in the project."
                : $"Multiple prefabs match '{prefabPath}': {string.Join(", ", paths)}. Use the full path.";
            return null;
        }

        // ------------------------------------------------------------------ place

        private static object Place(ToolParams p)
        {
            var targetGo = ThesisMcpUtils.FindTarget(p.GetRaw("target"));
            if (targetGo == null)
                return new ErrorResponse($"Target '{p.Get("target")}' not found.");

            // 'target_object' was the old override's parameter name
            JToken refToken = p.GetRaw("reference_object") ?? p.GetRaw("target_object");
            var referenceGo = ThesisMcpUtils.FindTarget(refToken);
            if (referenceGo == null)
                return new ErrorResponse($"Reference object '{refToken}' not found.");
            if (referenceGo == targetGo)
                return new ErrorResponse("Target and reference object are the same object.");

            string relation = (p.Get("relative_position") ?? "right").Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "");

            Bounds refBounds = ThesisMcpUtils.GetObjectBounds(referenceGo);
            Bounds srcBounds = ThesisMcpUtils.GetObjectBounds(targetGo);
            // Offset between the target's pivot and its bounds centre, so meshes with off-centre pivots land correctly
            Vector3 pivotToCenter = srcBounds.center - targetGo.transform.position;

            Vector3 center;
            switch (relation)
            {
                case "on":
                case "ontop":
                case "ontopof":
                case "top":
                case "above":
                case "over":
                    // Resting on the reference's top surface
                    center = new Vector3(refBounds.center.x, refBounds.max.y + srcBounds.extents.y, refBounds.center.z);
                    break;
                case "below":
                case "under":
                case "underneath":
                case "bottom":
                    center = new Vector3(refBounds.center.x, refBounds.min.y - srcBounds.extents.y, refBounds.center.z);
                    break;
                case "front":
                case "infront":
                case "infrontof":
                case "before":
                    center = refBounds.center + Vector3.forward * (refBounds.extents.z + srcBounds.extents.z + Gap);
                    break;
                case "back":
                case "behind":
                case "rear":
                    center = refBounds.center + Vector3.back * (refBounds.extents.z + srcBounds.extents.z + Gap);
                    break;
                case "left":
                case "leftof":
                    center = refBounds.center + Vector3.left * (refBounds.extents.x + srcBounds.extents.x + Gap);
                    break;
                case "right":
                case "rightof":
                case "next":
                case "nextto":
                case "beside":
                    center = refBounds.center + Vector3.right * (refBounds.extents.x + srcBounds.extents.x + Gap);
                    break;
                default:
                    return new ErrorResponse(
                        $"Unknown relative_position '{relation}'. Use on, above, below, front, behind, left or right.");
            }

            Vector3 newPosition = center - pivotToCenter;
            Vector3? extra = ThesisMcpUtils.ReadVector3(p.GetRaw("offset"));
            if (extra.HasValue)
                newPosition += extra.Value;

            Undo.RecordObject(targetGo.transform, "Place GameObject");
            targetGo.transform.position = newPosition;

            // Stop a grabbable object from keeping momentum it had before the move
            var rb = targetGo.GetComponent<Rigidbody>();
            if (rb != null && Application.isPlaying && !rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            ThesisMcpUtils.MarkDirty(targetGo);

            return new SuccessResponse(
                $"Placed '{targetGo.name}' {relation} '{referenceGo.name}' at {ThesisMcpUtils.Fmt(newPosition)}.",
                ThesisMcpUtils.Describe(targetGo));
        }

        private const float Gap = 0.05f;

        // ------------------------------------------------------------------ scale

        private static object Scale(ToolParams p)
        {
            var targetGo = ThesisMcpUtils.FindTarget(p.GetRaw("target"));
            if (targetGo == null)
                return new ErrorResponse($"Target '{p.Get("target")}' not found.");

            Vector3 current = targetGo.transform.localScale;
            Vector3 newScale;

            Vector3? exact = ThesisMcpUtils.ReadVector3(p.GetRaw("scale"));
            string command = p.Get("scale_command")?.ToLowerInvariant();
            float? multiplier = ThesisMcpUtils.ReadFloat(p.GetRaw("multiplier"));

            if (exact.HasValue)
            {
                newScale = exact.Value;
            }
            else if (!string.IsNullOrEmpty(command))
            {
                float factor = Mathf.Abs(multiplier ?? 2f);
                if (factor < 0.0001f)
                    return new ErrorResponse("multiplier must be greater than 0.");

                if (ContainsAny(command, "reset", "normal", "original"))
                    newScale = Vector3.one;
                else if (ContainsAny(command, "smaller", "decrease", "half", "reduce", "shrink"))
                    newScale = current / (command.Contains("half") && !multiplier.HasValue ? 2f : factor);
                else if (ContainsAny(command, "bigger", "larger", "increase", "double", "grow", "times"))
                    newScale = current * factor;
                else
                    return new ErrorResponse($"Unknown scale_command '{command}'. Use bigger, smaller or reset.");
            }
            else if (multiplier.HasValue)
            {
                newScale = current * Mathf.Abs(multiplier.Value);
            }
            else
            {
                return new ErrorResponse("Provide 'scale_command' (bigger/smaller/reset), 'multiplier', or exact 'scale' [x,y,z].");
            }

            newScale = Vector3.Max(newScale, Vector3.one * 0.0001f);

            Undo.RecordObject(targetGo.transform, "Scale GameObject");
            targetGo.transform.localScale = newScale;
            ThesisMcpUtils.MarkDirty(targetGo);

            return new SuccessResponse(
                $"Scaled '{targetGo.name}' from {ThesisMcpUtils.Fmt(current)} to {ThesisMcpUtils.Fmt(newScale)}.",
                ThesisMcpUtils.Describe(targetGo));
        }

        private static bool ContainsAny(string s, params string[] words) => words.Any(s.Contains);

        // ------------------------------------------------------------------ color

        private static object Recolor(ToolParams p)
        {
            var targetGo = ThesisMcpUtils.FindTarget(p.GetRaw("target"));
            if (targetGo == null)
                return new ErrorResponse($"Target '{p.Get("target")}' not found.");

            JToken nameToken = p.GetRaw("color_name");
            JToken colorToken = p.GetRaw("color");
            Color newColor;
            if (nameToken != null && nameToken.Type != JTokenType.Null)
            {
                if (!ThesisMcpUtils.TryReadColor(nameToken, out newColor))
                    return new ErrorResponse($"Unknown color '{nameToken}'. Supported: {ThesisMcpUtils.SupportedColorNames}, or hex.");
            }
            else if (colorToken != null && colorToken.Type != JTokenType.Null)
            {
                if (!ThesisMcpUtils.TryReadColor(colorToken, out newColor))
                    return new ErrorResponse($"Could not read color '{colorToken}'. Use [r,g,b] with values 0-1.");
            }
            else
            {
                return new ErrorResponse("Provide 'color_name' (e.g. red) or 'color' [r,g,b].");
            }

            var renderers = targetGo.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new ErrorResponse($"'{targetGo.name}' has no renderer, so it has no color to change.");

            foreach (var renderer in renderers)
            {
                Undo.RecordObject(renderer, "Change Color");
                var source = renderer.sharedMaterial;
                // Give this object its own material so other objects sharing the original keep their color
                var mat = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = $"{targetGo.name}_Color";
                mat.color = newColor;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", newColor);
                renderer.sharedMaterial = mat;
                EditorUtility.SetDirty(renderer);
            }

            ThesisMcpUtils.MarkDirty(targetGo);
            return new SuccessResponse(
                $"Changed color of '{targetGo.name}' to #{ColorUtility.ToHtmlStringRGB(newColor)}.",
                ThesisMcpUtils.Describe(targetGo));
        }

        // ------------------------------------------------------------------ duplicate

        /// <summary>
        /// Instantiate-based copy, so it works in Play mode (the stock manage_gameobject duplicate is Edit-mode only).
        /// The copy is placed beside the original, to the viewer's right.
        /// </summary>
        private static object Duplicate(ToolParams p)
        {
            var original = ThesisMcpUtils.FindTarget(p.GetRaw("target"));
            if (original == null)
                return new ErrorResponse($"Target '{p.Get("target")}' not found.");

            if (original.GetComponentInChildren<Camera>(true) != null ||
                original.GetComponentInChildren<McpPromptSender>(true) != null ||
                original.GetComponentInChildren<SpeechRecognition>(true) != null ||
                ThesisMcpUtils.IsProtected(original))
            {
                return new ErrorResponse($"'{original.name}' is part of the VR rig / voice pipeline and cannot be duplicated.");
            }

            var copy = UnityEngine.Object.Instantiate(original, original.transform.parent);
            copy.name = ThesisMcpUtils.MakeUniqueName(p.Get("name") ?? $"{original.name} (Copy)");
            Undo.RegisterCreatedObjectUndo(copy, $"Duplicate '{original.name}'");

            Vector3? position = ThesisMcpUtils.ReadVector3(p.GetRaw("position"));
            if (position.HasValue)
            {
                copy.transform.position = position.Value;
            }
            else
            {
                Vector3 side = ThesisMcpUtils.ViewerRight();
                float width = Vector3.Scale(ThesisMcpUtils.GetObjectBounds(original).size, new Vector3(Mathf.Abs(side.x), Mathf.Abs(side.y), Mathf.Abs(side.z))).magnitude;
                copy.transform.position = original.transform.position + side * (Mathf.Max(width, 0.5f) + Gap * 4);
            }
            copy.transform.rotation = original.transform.rotation;

            // Don't inherit the original's momentum
            var rb = copy.GetComponent<Rigidbody>();
            if (rb != null && Application.isPlaying && !rb.isKinematic)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            ThesisMcpUtils.MarkDirty(copy);
            return new SuccessResponse(
                $"Duplicated '{original.name}' as '{copy.name}' at {ThesisMcpUtils.Fmt(copy.transform.position)}.",
                ThesisMcpUtils.Describe(copy));
        }

        // ------------------------------------------------------------------ delete

        private static object Delete(ToolParams p)
        {
            var targetGo = ThesisMcpUtils.FindTarget(p.GetRaw("target"));
            if (targetGo == null)
                return new ErrorResponse($"Target '{p.Get("target")}' not found.");

            // Protect the parts of the scene the VR demo depends on
            if (targetGo.GetComponentInChildren<Camera>(true) != null ||
                targetGo.GetComponentInChildren<McpPromptSender>(true) != null ||
                targetGo.GetComponentInChildren<SpeechRecognition>(true) != null ||
                ThesisMcpUtils.IsProtected(targetGo))
            {
                return new ErrorResponse($"'{targetGo.name}' is part of the VR rig / voice pipeline and cannot be deleted.");
            }

            string name = targetGo.name;
            Undo.DestroyObjectImmediate(targetGo);
            return new SuccessResponse($"Deleted '{name}'.");
        }
    }
}
