using System;
using Back2War.Core.World;
using UnityEngine;
using WorldEntityId = Back2War.Core.World.EntityId;

namespace Back2War.Core.Input
{
    /// <summary>
    /// Deterministic ray-query helpers.
    /// Tie-break for selectable hits: nearest hit, then lowest EntityId.
    /// </summary>
    public static class WorldRaycaster
    {
        public struct SelectableHit
        {
            public WorldEntityId Entity;
            public Selectable Selectable;
            public Vector3 Point;
            public float Distance;
        }

        public static bool RaycastSelectable(
            Camera cam,
            Vector2 screenPos,
            LayerMask selectableMask,
            out SelectableHit result,
            float maxDistance = 10000f)
        {
            result = default;

            if (cam == null)
            {
                return false;
            }

            Ray ray = cam.ScreenPointToRay(screenPos);
            RaycastHit[] hits = Physics.RaycastAll(ray, maxDistance, selectableMask, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            bool found = false;
            long bestDistanceKey = long.MaxValue;
            uint bestId = uint.MaxValue;
            SelectableHit best = default;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider collider = hits[i].collider;
                if (collider == null)
                {
                    continue;
                }

                Selectable selectable = collider.GetComponentInParent<Selectable>();
                if (selectable == null)
                {
                    continue;
                }

                WorldEntityId entity = selectable.GetComponent<WorldEntityId>();
                if (entity == null)
                {
                    continue;
                }

                long distanceKey = QuantizeDistance(hits[i].distance);
                if (!found || distanceKey < bestDistanceKey || (distanceKey == bestDistanceKey && entity.Id < bestId))
                {
                    found = true;
                    bestDistanceKey = distanceKey;
                    bestId = entity.Id;
                    best = new SelectableHit
                    {
                        Entity = entity,
                        Selectable = selectable,
                        Point = hits[i].point,
                        Distance = hits[i].distance
                    };
                }
            }

            if (!found)
            {
                return false;
            }

            result = best;
            return true;
        }

        public static bool RaycastGround(
            Camera cam,
            Vector2 screenPos,
            LayerMask groundMask,
            out Vector3 groundPoint,
            float maxDistance = 10000f)
        {
            groundPoint = default;

            if (cam == null)
            {
                return false;
            }

            Ray ray = cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            groundPoint = hit.point;
            return true;
        }

        private static long QuantizeDistance(float distance)
        {
            return (long)Math.Round(distance * 1000000.0d);
        }
    }
}
