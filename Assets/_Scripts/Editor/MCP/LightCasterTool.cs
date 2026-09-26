using System;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Thesis.Editor.Mcp
{
    /// <summary>
    /// Creates a light (point, spot or area) in one call.
    /// Ported from the 'light_caster' action of the old ManageGameObject override.
    /// </summary>
    [McpForUnityTool("light_caster", Description =
        "Creates ONE light in the scene per call. light_type: point (default), spot or area. " +
        "Set color by 'color_name' (e.g. red, warm white) or 'color' [r,g,b] in 0-1, plus intensity, range, spot_angle. " +
        "To put the light over/under/next to an object, set 'reference_object' to that object's name and 'relative_position' " +
        "(above, below, front, behind, left, right); spotlights then aim at the object automatically. " +
        "Without reference_object or position the light appears in front of the user.")]
    public static class LightCasterTool
    {
        public class Parameters
        {
            [ToolParameter("Name of the new light. Default: 'Point Light', 'Spot Light', ...", Required = false)]
            public string name { get; set; }

            [ToolParameter("point (default), spot or area.", Required = false, DefaultValue = "point")]
            public string light_type { get; set; }

            [ToolParameter("Brightness (default 1).", Required = false, DefaultValue = "1")]
            public float? intensity { get; set; }

            [ToolParameter("Reach in meters for point/spot lights (default 10).", Required = false, DefaultValue = "10")]
            public float? range { get; set; }

            [ToolParameter("Cone angle in degrees for spot lights (default 30).", Required = false, DefaultValue = "30")]
            public float? spot_angle { get; set; }

            [ToolParameter("Light color as [r,g,b] with values 0-1.", Required = false)]
            public float[] color { get; set; }

            [ToolParameter("Named light color: " + ThesisMcpUtils.SupportedColorNames + ". Also accepts hex like #FFAA00.", Required = false)]
            public string color_name { get; set; }

            [ToolParameter("Name of the object to place the light relative to, e.g. 'Balcony'.", Required = false)]
            public string reference_object { get; set; }

            [ToolParameter("Where the light goes relative to reference_object: above (default), below, front, behind, left or right.", Required = false, DefaultValue = "above")]
            public string relative_position { get; set; }

            [ToolParameter("Gap in meters between the light and reference_object (default 1).", Required = false, DefaultValue = "1")]
            public float? distance { get; set; }

            [ToolParameter("World position [x,y,z]. With reference_object: offset from that object's center. If omitted the light appears in front of the user.", Required = false)]
            public float[] position { get; set; }

            [ToolParameter("Euler rotation [x,y,z] in degrees. Default: spotlights aim at reference_object, otherwise [90,0,0] = pointing down.", Required = false)]
            public float[] rotation { get; set; }

            [ToolParameter("Same as reference_object (the light is placed relative to this object, not attached to it).", Required = false)]
            public string parent { get; set; }
        }

        // Guards against the LLM repeating the identical call in consecutive tool rounds
        private static string _lastRequestKey;
        private static double _lastRequestTime;
        private static object _lastResult;
        private const double RepeatWindowSeconds = 5.0;

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("Parameters cannot be null.");

            var p = new ToolParams(@params);

            // Same request again within a few seconds = the LLM repeating itself, not a request for another light
            string requestKey = @params.ToString(Newtonsoft.Json.Formatting.None);
            double now = EditorApplication.timeSinceStartup;
            if (requestKey == _lastRequestKey && now - _lastRequestTime < RepeatWindowSeconds && _lastResult != null)
            {
                Debug.Log("[light_caster] Ignored identical repeat call; returning the light that was just created.");
                return _lastResult;
            }

            object result = CreateLight(p);
            if (result is SuccessResponse)
            {
                _lastRequestKey = requestKey;
                _lastRequestTime = now;
                _lastResult = result;
            }
            return result;
        }

        private static object CreateLight(ToolParams p)
        {
            try
            {
                // --- Light type ---
                string lightTypeName = (p.Get("light_type") ?? "point").Trim().ToLowerInvariant();
                LightType lightType;
                string label;
                switch (lightTypeName)
                {
                    case "point":
                        lightType = LightType.Point; label = "Point"; break;
                    case "spot":
                    case "spotlight":
                        lightType = LightType.Spot; label = "Spot"; break;
                    case "area":
                    case "rectangle":
                        lightType = LightType.Rectangle; label = "Area"; break;
                    default:
                        return new ErrorResponse($"Invalid light_type '{lightTypeName}'. Supported: point, spot, area.");
                }

                // --- Numeric settings ---
                float intensity = ThesisMcpUtils.ReadFloat(p.GetRaw("intensity")) ?? 1f;
                float range = ThesisMcpUtils.ReadFloat(p.GetRaw("range")) ?? 10f;
                float spotAngle = ThesisMcpUtils.ReadFloat(p.GetRaw("spot_angle")) ?? 30f;
                if (intensity < 0f) return new ErrorResponse("intensity must be 0 or greater.");
                if (range <= 0f) return new ErrorResponse("range must be greater than 0.");
                spotAngle = Mathf.Clamp(spotAngle, 1f, 179f);

                // --- Colour (name/hex first, then array) ---
                Color lightColor = Color.white;
                JToken colorNameToken = p.GetRaw("color_name");
                JToken colorToken = p.GetRaw("color");
                if (colorNameToken != null && colorNameToken.Type != JTokenType.Null)
                {
                    if (!ThesisMcpUtils.TryReadColor(colorNameToken, out lightColor))
                        return new ErrorResponse($"Unknown color '{colorNameToken}'. Supported: {ThesisMcpUtils.SupportedColorNames}, or hex.");
                }
                else if (colorToken != null && colorToken.Type != JTokenType.Null)
                {
                    if (!ThesisMcpUtils.TryReadColor(colorToken, out lightColor))
                        return new ErrorResponse($"Could not read color '{colorToken}'. Use [r,g,b] with values 0-1.");
                }
                lightColor.a = 1f;

                // --- Create ---
                string name = p.Get("name");
                var lightGo = new GameObject(ThesisMcpUtils.MakeUniqueName(string.IsNullOrEmpty(name) ? $"{label} Light" : name));
                Undo.RegisterCreatedObjectUndo(lightGo, "Create Light");

                // The LLM often uses 'parent' to mean "over this object", so both are treated as a placement reference.
                // The light is never attached to the object (avoids inheriting its scale, or following the camera).
                string referenceName = p.Get("reference_object");
                if (string.IsNullOrEmpty(referenceName))
                    referenceName = p.Get("parent");

                Vector3? explicitPosition = ThesisMcpUtils.ReadVector3(p.GetRaw("position"));
                Vector3? explicitRotation = ThesisMcpUtils.ReadVector3(p.GetRaw("rotation"));
                string placement;

                if (!string.IsNullOrEmpty(referenceName) &&
                    !referenceName.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    var reference = ThesisMcpUtils.FindTarget(referenceName);
                    if (reference == null)
                    {
                        Undo.DestroyObjectImmediate(lightGo);
                        return new ErrorResponse($"Reference object '{referenceName}' not found. Check the object's name in the scene.");
                    }

                    Bounds b = ThesisMcpUtils.GetObjectBounds(reference);
                    string relation = p.Get("relative_position");
                    bool useOffset = explicitPosition.HasValue && string.IsNullOrEmpty(relation);

                    if (useOffset)
                    {
                        lightGo.transform.position = b.center + explicitPosition.Value;
                        placement = $"offset {ThesisMcpUtils.Fmt(explicitPosition.Value)} from '{reference.name}'";
                    }
                    else
                    {
                        float gap = Mathf.Max(0f, ThesisMcpUtils.ReadFloat(p.GetRaw("distance")) ?? 1f);
                        if (!TryPlaceRelative(b, relation, gap, out Vector3 pos, out string side))
                        {
                            Undo.DestroyObjectImmediate(lightGo);
                            return new ErrorResponse($"Unknown relative_position '{relation}'. Use above, below, front, behind, left or right.");
                        }
                        lightGo.transform.position = pos;
                        placement = $"{side} '{reference.name}'";
                    }

                    // Spotlights point at the object they were placed next to
                    if (explicitRotation.HasValue)
                        lightGo.transform.eulerAngles = explicitRotation.Value;
                    else if (lightType == LightType.Spot && (b.center - lightGo.transform.position).sqrMagnitude > 0.0001f)
                        lightGo.transform.rotation = Quaternion.LookRotation(b.center - lightGo.transform.position);
                    else
                        lightGo.transform.eulerAngles = new Vector3(90f, 0f, 0f);
                }
                else
                {
                    lightGo.transform.position = explicitPosition ?? ThesisMcpUtils.NextSpawnPosition();
                    lightGo.transform.eulerAngles = explicitRotation ?? new Vector3(90f, 0f, 0f);
                    placement = explicitPosition.HasValue ? "at the given position" : "in front of the user";
                }

                var light = lightGo.AddComponent<Light>();
                light.type = lightType;
                light.color = lightColor;
                light.intensity = intensity;
                if (lightType == LightType.Point || lightType == LightType.Spot)
                    light.range = range;
                if (lightType == LightType.Spot)
                    light.spotAngle = spotAngle;

                // Play-mode "gizmo" (bulb + ring) so the new light is visible in the Game view / headset
                lightGo.AddComponent<LightMarker>();

                ThesisMcpUtils.MarkDirty(lightGo);
                Selection.activeGameObject = lightGo;

                string description = FormattableString.Invariant(
                    $"{label.ToLowerInvariant()} light '{lightGo.name}' at {ThesisMcpUtils.Fmt(lightGo.transform.position)}, color #{ColorUtility.ToHtmlStringRGB(lightColor)}, intensity {intensity:0.##}");
                if (lightType != LightType.Rectangle) description += FormattableString.Invariant($", range {range:0.##}");
                if (lightType == LightType.Spot) description += FormattableString.Invariant($", spot angle {spotAngle:0.#}");

                return new SuccessResponse($"Created {description} ({placement}).", ThesisMcpUtils.Describe(lightGo));
            }
            catch (Exception e)
            {
                Debug.LogError($"[light_caster] Failed: {e}");
                return new ErrorResponse($"Error creating light: {e.Message}");
            }
        }

        /// <summary>
        /// Position next to an object's bounds. "front" is the side facing the user,
        /// "left"/"right" are from the user's point of view.
        /// </summary>
        private static bool TryPlaceRelative(Bounds b, string relation, float gap, out Vector3 position, out string side)
        {
            string r = (relation ?? "above").Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "");
            Vector3 right = ThesisMcpUtils.ViewerRight();
            Vector3 towardUser = -Vector3.Cross(right, Vector3.up); // horizontal direction from the object toward the user

            float Extent(Vector3 dir) => Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z))));

            switch (r)
            {
                case "above": case "over": case "on": case "ontop": case "ontopof": case "top": case "overhead":
                    position = new Vector3(b.center.x, b.max.y + gap, b.center.z); side = "above"; return true;
                case "below": case "under": case "underneath": case "beneath": case "bottom":
                    position = new Vector3(b.center.x, b.min.y - gap, b.center.z); side = "below"; return true;
                case "front": case "infront": case "infrontof": case "before":
                    position = b.center + towardUser * (Extent(towardUser) + gap); side = "in front of"; return true;
                case "behind": case "back": case "rear":
                    position = b.center - towardUser * (Extent(towardUser) + gap); side = "behind"; return true;
                case "left": case "leftof":
                    position = b.center - right * (Extent(right) + gap); side = "left of"; return true;
                case "right": case "rightof": case "next": case "nextto": case "beside":
                    position = b.center + right * (Extent(right) + gap); side = "right of"; return true;
                default:
                    position = default; side = null; return false;
            }
        }
    }
}
