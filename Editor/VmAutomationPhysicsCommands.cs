using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Physics commands: raycasting, overlap queries, collision layer management.
    /// </summary>
    public static class VmAutomationPhysicsCommands
    {
        public static object Raycast(Dictionary<string, object> args)
        {
            if (!(args.TryGetValue("origin", out object originValue) &&
                  originValue is Dictionary<string, object>) ||
                !(args.TryGetValue("direction", out object directionValue) &&
                  directionValue is Dictionary<string, object>))
                return VmAutomationResponse.Error(
                    "origin and direction must be vector objects.",
                    "invalid_arguments");
            Vector3 origin = VmAutomationGameObjectCommands.DictToVector3(args.ContainsKey("origin") ? args["origin"] as Dictionary<string, object> : null);
            Vector3 direction = VmAutomationGameObjectCommands.DictToVector3(args.ContainsKey("direction") ? args["direction"] as Dictionary<string, object> : null);
            float maxDistance = args.ContainsKey("maxDistance") ? Convert.ToSingle(args["maxDistance"]) : Mathf.Infinity;
            if (maxDistance < 0f)
                return VmAutomationResponse.Error("maxDistance cannot be negative.",
                    "invalid_arguments");
            bool use2D = Use2D(args);
            int layerMask = args.ContainsKey("layerMask")
                ? Convert.ToInt32(args["layerMask"])
                : use2D ? Physics2D.DefaultRaycastLayers : Physics.DefaultRaycastLayers;

            if (use2D)
                return Raycast2D(args, origin, direction, maxDistance, layerMask);

            if (direction == Vector3.zero)
                direction = Vector3.forward;

            bool allHits = args.ContainsKey("all") && Convert.ToBoolean(args["all"]);

            if (allHits)
            {
                RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxDistance, layerMask)
                    .OrderBy(hit => hit.distance).ToArray();
                int maxResults = GetMaxResults(args);
                var results = hits.Take(maxResults).Select(HitToDict).ToList();
                return new Dictionary<string, object>
                {
                    { "hitCount", hits.Length },
                    { "hits", results },
                    { "truncated", hits.Length > results.Count },
                    { "origin", VmAutomationGameObjectCommands.Vector3ToDict(origin) },
                    { "direction", VmAutomationGameObjectCommands.Vector3ToDict(direction) },
                    { "dimension", "3D" },
                };
            }
            else
            {
                RaycastHit hit;
                bool didHit = Physics.Raycast(origin, direction, out hit, maxDistance, layerMask);
                if (!didHit)
                    return new Dictionary<string, object>
                    {
                        { "hit", false },
                        { "origin", VmAutomationGameObjectCommands.Vector3ToDict(origin) },
                        { "direction", VmAutomationGameObjectCommands.Vector3ToDict(direction) },
                        { "dimension", "3D" },
                    };

                return new Dictionary<string, object>
                {
                    { "hit", true },
                    { "hitInfo", HitToDict(hit) },
                    { "origin", VmAutomationGameObjectCommands.Vector3ToDict(origin) },
                    { "direction", VmAutomationGameObjectCommands.Vector3ToDict(direction) },
                    { "dimension", "3D" },
                };
            }
        }

        public static object OverlapSphere(Dictionary<string, object> args)
        {
            if (!(args.TryGetValue("center", out object centerValue) &&
                  centerValue is Dictionary<string, object>))
                return VmAutomationResponse.Error("center must be a vector object.",
                    "invalid_arguments");
            Vector3 center = VmAutomationGameObjectCommands.DictToVector3(args.ContainsKey("center") ? args["center"] as Dictionary<string, object> : null);
            float radius = args.ContainsKey("radius") ? Convert.ToSingle(args["radius"]) : 1f;
            if (radius < 0f)
                return VmAutomationResponse.Error("radius cannot be negative.",
                    "invalid_arguments");
            bool use2D = Use2D(args);
            int maxResults = GetMaxResults(args);
            int layerMask = args.ContainsKey("layerMask")
                ? Convert.ToInt32(args["layerMask"])
                : use2D ? Physics2D.AllLayers : Physics.AllLayers;

            if (use2D)
            {
                Collider2D[] colliders2D = Physics2D.OverlapCircleAll(
                    new Vector2(center.x, center.y), radius, layerMask);
                var results2D = colliders2D.OrderBy(ColliderSortKey)
                    .Take(maxResults).Select(Collider2DToDict).ToList();
                return new Dictionary<string, object>
                {
                    { "dimension", "2D" },
                    { "center", VmAutomationGameObjectCommands.Vector3ToDict(center) },
                    { "radius", radius },
                    { "count", colliders2D.Length },
                    { "colliders", results2D },
                    { "truncated", colliders2D.Length > results2D.Count },
                };
            }

            var colliders = Physics.OverlapSphere(center, radius, layerMask);
            var results = colliders.OrderBy(ColliderSortKey)
                .Take(maxResults).Select(Collider3DToDict).ToList();

            return new Dictionary<string, object>
            {
                { "center", VmAutomationGameObjectCommands.Vector3ToDict(center) },
                { "radius", radius },
                { "count", colliders.Length },
                { "colliders", results },
                { "truncated", colliders.Length > results.Count },
                { "dimension", "3D" },
            };
        }

        public static object OverlapBox(Dictionary<string, object> args)
        {
            if (!(args.TryGetValue("center", out object centerValue) &&
                  centerValue is Dictionary<string, object>) ||
                !(args.TryGetValue("halfExtents", out object extentsValue) &&
                  extentsValue is Dictionary<string, object>))
                return VmAutomationResponse.Error(
                    "center and halfExtents must be vector objects.",
                    "invalid_arguments");
            Vector3 center = VmAutomationGameObjectCommands.DictToVector3(args.ContainsKey("center") ? args["center"] as Dictionary<string, object> : null);
            Vector3 halfExtents = VmAutomationGameObjectCommands.DictToVector3(args.ContainsKey("halfExtents") ? args["halfExtents"] as Dictionary<string, object> : null);
            if (halfExtents == Vector3.zero) halfExtents = Vector3.one * 0.5f;
            bool use2D = Use2D(args);
            if (halfExtents.x < 0f || halfExtents.y < 0f ||
                (!use2D && halfExtents.z < 0f))
                return VmAutomationResponse.Error("halfExtents cannot contain negative values.",
                    "invalid_arguments");
            int maxResults = GetMaxResults(args);
            int layerMask = args.ContainsKey("layerMask")
                ? Convert.ToInt32(args["layerMask"])
                : use2D ? Physics2D.AllLayers : Physics.AllLayers;

            if (use2D)
            {
                float angle = args.ContainsKey("angle") ? Convert.ToSingle(args["angle"]) : 0f;
                Collider2D[] colliders2D = Physics2D.OverlapBoxAll(
                    new Vector2(center.x, center.y),
                    new Vector2(halfExtents.x * 2f, halfExtents.y * 2f),
                    angle, layerMask);
                var results2D = colliders2D.OrderBy(ColliderSortKey)
                    .Take(maxResults).Select(Collider2DToDict).ToList();
                return new Dictionary<string, object>
                {
                    { "dimension", "2D" },
                    { "center", VmAutomationGameObjectCommands.Vector3ToDict(center) },
                    { "halfExtents", VmAutomationGameObjectCommands.Vector3ToDict(halfExtents) },
                    { "angle", angle },
                    { "count", colliders2D.Length },
                    { "colliders", results2D },
                    { "truncated", colliders2D.Length > results2D.Count },
                };
            }

            var colliders = Physics.OverlapBox(center, halfExtents, Quaternion.identity, layerMask);
            var results = colliders.OrderBy(ColliderSortKey)
                .Take(maxResults).Select(Collider3DToDict).ToList();

            return new Dictionary<string, object>
            {
                { "center", VmAutomationGameObjectCommands.Vector3ToDict(center) },
                { "halfExtents", VmAutomationGameObjectCommands.Vector3ToDict(halfExtents) },
                { "count", colliders.Length },
                { "colliders", results },
                { "truncated", colliders.Length > results.Count },
                { "dimension", "3D" },
            };
        }

        public static object GetCollisionMatrix(Dictionary<string, object> args)
        {
            var matrix = new Dictionary<string, object>();
            for (int i = 0; i < 32; i++)
            {
                string layerName = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(layerName)) continue;

                var collidesWith = new List<string>();
                for (int j = 0; j < 32; j++)
                {
                    string otherName = LayerMask.LayerToName(j);
                    if (string.IsNullOrEmpty(otherName)) continue;
                    if (!Physics.GetIgnoreLayerCollision(i, j))
                        collidesWith.Add(otherName);
                }
                matrix[layerName] = collidesWith;
            }

            return new Dictionary<string, object>
            {
                { "matrix", matrix },
            };
        }

        public static object SetCollisionLayer(Dictionary<string, object> args)
        {
            int layer1 = args.ContainsKey("layer1") ? Convert.ToInt32(args["layer1"]) : -1;
            int layer2 = args.ContainsKey("layer2") ? Convert.ToInt32(args["layer2"]) : -1;
            bool ignore = args.ContainsKey("ignore") ? Convert.ToBoolean(args["ignore"]) : true;

            // Allow layer names
            if (args.ContainsKey("layer1Name"))
                layer1 = LayerMask.NameToLayer(args["layer1Name"].ToString());
            if (args.ContainsKey("layer2Name"))
                layer2 = LayerMask.NameToLayer(args["layer2Name"].ToString());

            if (layer1 < 0 || layer2 < 0)
                return new { error = "Valid layer indices or names are required" };

            Physics.IgnoreLayerCollision(layer1, layer2, ignore);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "layer1", LayerMask.LayerToName(layer1) },
                { "layer2", LayerMask.LayerToName(layer2) },
                { "ignoreCollision", ignore },
            };
        }

        public static object SetGravity(Dictionary<string, object> args)
        {
            if (args.ContainsKey("gravity"))
            {
                var gravity = VmAutomationGameObjectCommands.DictToVector3(args["gravity"] as Dictionary<string, object>);
                Physics.gravity = gravity;
            }

            return new Dictionary<string, object>
            {
                { "gravity", VmAutomationGameObjectCommands.Vector3ToDict(Physics.gravity) },
            };
        }

        // ─── Helpers ───

        private static Dictionary<string, object> HitToDict(RaycastHit hit)
        {
            return new Dictionary<string, object>
            {
                { "gameObject", hit.collider.gameObject.name },
                { "instanceId", VmObjectId.Get(hit.collider.gameObject) },
                { "point", VmAutomationGameObjectCommands.Vector3ToDict(hit.point) },
                { "normal", VmAutomationGameObjectCommands.Vector3ToDict(hit.normal) },
                { "distance", hit.distance },
                { "colliderType", hit.collider.GetType().Name },
            };
        }

        private static object Raycast2D(Dictionary<string, object> args, Vector3 origin,
            Vector3 direction, float maxDistance, int layerMask)
        {
            var origin2D = new Vector2(origin.x, origin.y);
            var direction2D = new Vector2(direction.x, direction.y);
            if (direction2D == Vector2.zero)
                direction2D = Vector2.right;
            bool allHits = args.ContainsKey("all") && Convert.ToBoolean(args["all"]);
            if (allHits)
            {
                RaycastHit2D[] hits = Physics2D.RaycastAll(origin2D, direction2D,
                        maxDistance, layerMask)
                    .OrderBy(hit => hit.distance).ToArray();
                var results = hits.Take(GetMaxResults(args)).Select(Hit2DToDict).ToList();
                return new Dictionary<string, object>
                {
                    { "dimension", "2D" },
                    { "hitCount", hits.Length },
                    { "hits", results },
                    { "truncated", hits.Length > results.Count },
                    { "origin", Vector2ToDict(origin2D) },
                    { "direction", Vector2ToDict(direction2D) },
                };
            }

            RaycastHit2D hit = Physics2D.Raycast(origin2D, direction2D, maxDistance, layerMask);
            if (hit.collider == null)
            {
                return new Dictionary<string, object>
                {
                    { "dimension", "2D" },
                    { "hit", false },
                    { "origin", Vector2ToDict(origin2D) },
                    { "direction", Vector2ToDict(direction2D) },
                };
            }

            return new Dictionary<string, object>
            {
                { "dimension", "2D" },
                { "hit", true },
                { "hitInfo", Hit2DToDict(hit) },
                { "origin", Vector2ToDict(origin2D) },
                { "direction", Vector2ToDict(direction2D) },
            };
        }

        private static Dictionary<string, object> Hit2DToDict(RaycastHit2D hit)
        {
            return new Dictionary<string, object>
            {
                { "gameObject", hit.collider.gameObject.name },
                { "instanceId", VmObjectId.Get(hit.collider.gameObject) },
                { "point", Vector2ToDict(hit.point) },
                { "normal", Vector2ToDict(hit.normal) },
                { "distance", hit.distance },
                { "fraction", hit.fraction },
                { "colliderType", hit.collider.GetType().Name },
            };
        }

        private static Dictionary<string, object> Collider2DToDict(Collider2D collider)
        {
            var result = new Dictionary<string, object>
            {
                { "gameObject", collider.gameObject.name },
                { "colliderType", collider.GetType().Name },
                { "instanceId", VmObjectId.Get(collider.gameObject) },
            };
            VmAutomationTransformSerialization.AddVectorIfDifferent(result, "position",
                collider.transform.position, Vector3.zero);
            return result;
        }

        private static Dictionary<string, object> Collider3DToDict(Collider collider)
        {
            var result = new Dictionary<string, object>
            {
                { "gameObject", collider.gameObject.name },
                { "colliderType", collider.GetType().Name },
                { "instanceId", VmObjectId.Get(collider.gameObject) },
            };
            VmAutomationTransformSerialization.AddVectorIfDifferent(result, "position",
                collider.transform.position, Vector3.zero);
            return result;
        }

        private static string ColliderSortKey(Component collider)
        {
            return VmAutomationGameObjectCommands.GetHierarchyPath(collider.gameObject) + "\n" +
                   collider.GetType().FullName;
        }

        private static int GetMaxResults(Dictionary<string, object> args)
        {
            int value = args != null && args.TryGetValue("maxResults", out object raw) &&
                        raw != null
                ? Convert.ToInt32(raw)
                : 100;
            return Math.Max(1, Math.Min(500, value));
        }

        private static Dictionary<string, object> Vector2ToDict(Vector2 value)
        {
            return new Dictionary<string, object> { { "x", value.x }, { "y", value.y } };
        }

        private static bool Use2D(Dictionary<string, object> args)
        {
            string dimension;
            if (args == null ||
                !args.TryGetValue("dimension", out object value) ||
                value == null)
            {
                dimension = VmAutomationSettings.DefaultPhysicsDimension;
            }
            else
            {
                dimension = value.ToString();
            }
            if (string.Equals(dimension, "2D", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(dimension, "3D", StringComparison.OrdinalIgnoreCase))
                return false;
            throw new ArgumentException("dimension must be 2D or 3D.");
        }
    }
}
