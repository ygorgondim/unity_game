using UnityEngine;

namespace WizardRoyale.Domain
{
    // Position remains the ground/shadow anchor, but the shared capsule is aligned to the
    // elevated body. Sprite size, facing, hats and visual bobbing never change its geometry.
    public static class CharacterHitbox
    {
        public const float Radius = 0.18f * GameTuning.CharacterScale;
        public const float HalfSegment = 0.06f * GameTuning.CharacterScale;
        public const float BodyElevation = 0.12f * GameTuning.CharacterScale;
        public const float CenterHeight = BodyElevation + 0.26f * GameTuning.CharacterScale;

        public static Vector2 Center(Vector2 position)
        {
            return position + Vector2.up * CenterHeight;
        }

        public static Vector2 ClosestAxisPoint(Vector2 position, Vector2 point)
        {
            Vector2 center = Center(position);
            return new Vector2(center.x,
                Mathf.Clamp(point.y, center.y - HalfSegment, center.y + HalfSegment));
        }

        public static bool IntersectsCircle(Vector2 position, Vector2 circleCenter, float circleRadius)
        {
            float combinedRadius = Radius + circleRadius;
            return (circleCenter - ClosestAxisPoint(position, circleCenter)).sqrMagnitude
                <= combinedRadius * combinedRadius;
        }

        public static bool TryGetCircleSeparation(Vector2 position, Vector2 circleCenter, float circleRadius,
            out Vector2 separation)
        {
            Vector2 offset = ClosestAxisPoint(position, circleCenter) - circleCenter;
            return TryGetSeparation(offset, Radius + circleRadius, out separation);
        }

        public static bool TryGetCharacterSeparation(Vector2 position, Vector2 otherPosition,
            out Vector2 separation)
        {
            // The capsules have parallel vertical axes with the same fixed length.
            Vector2 delta = Center(position) - Center(otherPosition);
            Vector2 axisOffset = new Vector2(delta.x,
                Mathf.Sign(delta.y) * Mathf.Max(0f, Mathf.Abs(delta.y) - HalfSegment * 2f));
            return TryGetSeparation(axisOffset, Radius * 2f, out separation);
        }

        private static bool TryGetSeparation(Vector2 axisOffset, float combinedRadius, out Vector2 separation)
        {
            separation = Vector2.zero;
            if (axisOffset.sqrMagnitude >= combinedRadius * combinedRadius) return false;
            float distance = axisOffset.magnitude;
            Vector2 normal = distance > 0.000001f ? axisOffset / distance : Vector2.right;
            separation = normal * (combinedRadius - distance);
            return true;
        }

        public static bool IntersectsSegment(Vector2 position, Vector2 start, Vector2 end, float beamRadius)
        {
            Vector2 center = Center(position);
            Vector2 bottom = center - Vector2.up * HalfSegment;
            Vector2 top = center + Vector2.up * HalfSegment;
            Vector2 beam = end - start;

            // Crossing the capsule axis means the distance between the two segments is zero.
            if (Mathf.Abs(beam.x) > 0.000001f)
            {
                float t = (center.x - start.x) / beam.x;
                float y = start.y + beam.y * t;
                if (t >= 0f && t <= 1f && y >= bottom.y && y <= top.y) return true;
            }

            float combinedRadius = Radius + beamRadius;
            float distanceSquared = Mathf.Min(
                (start - ClosestAxisPoint(position, start)).sqrMagnitude,
                (end - ClosestAxisPoint(position, end)).sqrMagnitude,
                DistanceSquaredToSegment(bottom, start, end),
                DistanceSquaredToSegment(top, start, end));
            return distanceSquared <= combinedRadius * combinedRadius;
        }

        public static Vector2 AttackOrigin(Vector2 position, Vector2 direction, float projectileRadius)
        {
            // The capsule support plane leaves the entire projectile outside its owner's body.
            float clearance = Radius + HalfSegment * Mathf.Abs(direction.y) + projectileRadius + 0.02f;
            return Center(position) + direction * clearance;
        }

        private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            if (segment.sqrMagnitude <= 0.000001f) return (point - start).sqrMagnitude;
            float t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude);
            return (point - (start + segment * t)).sqrMagnitude;
        }
    }
}
