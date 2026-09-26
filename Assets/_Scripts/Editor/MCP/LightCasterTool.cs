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
        "Creates a light in the scene in one call. light_type: point (default), spot or area. " +
        "Set color by 'color_name' (e.g. red, warm white) or 'color' [r,g,b] in 0-1, plus intensity, range, spot_angle. " +
        "If position is omitted the light appears in front of the user.")]
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

            [ToolParameter("World position [x,y,z]. If omitted the light appears in front of the user.", Required = false)]
            public float[] position { get; set; }

            [ToolParameter("Euler rotation [x,y,z] in degrees (default [90,0,0] = pointing down).", Required = false)]
            public float[] rotation { get; set; }

            [ToolParameter("Parent object name or path.", Required = false)]
            public string parent { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("Parameters cannot be null.");

            var p = new ToolParams(@params);

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

                string parentName = p.Get("parent");
                if (!string.IsNullOrEmpty(parentName))
                {
                    var parentGo = ThesisMcpUtils.FindTarget(parentName);
                    if (parentGo == null)
                    {
                        Undo.DestroyObjectImmediate(lightGo);
                        return new ErrorResponse($"Parent '{parentName}' not found.");
                    }
                    Undo.SetTransformParent(lightGo.transform, parentGo.transform, "Set Light Parent");
                }

                lightGo.transform.position = ThesisMcpUtils.ReadVector3(p.GetRaw("position")) ?? ThesisMcpUtils.NextSpawnPosition();
                lightGo.transform.eulerAngles = ThesisMcpUtils.ReadVector3(p.GetRaw("rotation")) ?? new Vector3(90f, 0f, 0f);

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

                return new SuccessResponse($"Created {description}.", ThesisMcpUtils.Describe(lightGo));
            }
            catch (Exception e)
            {
                Debug.LogError($"[light_caster] Failed: {e}");
                return new ErrorResponse($"Error creating light: {e.Message}");
            }
        }
    }
}
