using System.Collections.Generic;
using UnityEngine;

namespace SushiDojo.UI
{
    public enum BubbleBodyShape { RoundedRect, Ellipse }

    /// <summary>
    /// Shared speech bubble geometry (2D, counter-clockwise outlines), used by the world and UI bubbles:
    /// body outline, tail toward a target, hand-drawn wobble, textured fill and a pressure-varying stroke.
    /// </summary>
    public static class BubbleGeometry
    {
        const float MinTailLength = 0.0001f;
        const float MiterLimit = 2f;
        const float PressureSeedOffset = 31.7f;
        const float WobbleSeedOffset = 71.3f;
        const float BoilSeedStep = 13.37f;

        public static void BuildBody(List<Vector2> result, BubbleBodyShape shape, Vector2 size, float cornerRadius, int resolution)
        {
            var raw = new List<Vector2>();
            float halfW = size.x * 0.5f;
            float halfH = size.y * 0.5f;

            if (shape == BubbleBodyShape.Ellipse)
            {
                for (int i = 0; i < resolution; i++)
                {
                    float angle = Mathf.PI * 2f * i / resolution;
                    raw.Add(new Vector2(Mathf.Cos(angle) * halfW, Mathf.Sin(angle) * halfH));
                }
            }
            else
            {
                float radius = Mathf.Min(cornerRadius, halfW, halfH);
                var corners = new[]
                {
                    new Vector2(halfW - radius, halfH - radius),
                    new Vector2(-halfW + radius, halfH - radius),
                    new Vector2(-halfW + radius, -halfH + radius),
                    new Vector2(halfW - radius, -halfH + radius),
                };
                int arcSegments = Mathf.Max(resolution / 8, 2);
                for (int c = 0; c < corners.Length; c++)
                    for (int s = 0; s <= arcSegments; s++)
                    {
                        float angle = Mathf.PI * 0.5f * (c + (float)s / arcSegments);
                        raw.Add(corners[c] + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
                    }
            }

            // Even spacing keeps the wobble and pressure smooth on long straight edges.
            Resample(raw, resolution, result);
        }

        /// <summary>
        /// Body plus a tail toward the target, as one outline starting at the tail's far base point (B).
        /// Returns the index of the tail's near base point (A), or -1 when there is no tail; tailEnd is the last tail point.
        /// </summary>
        public static int BuildOutline(List<Vector2> body, Vector2 target, float baseWidth, float gap, float maxLength,
            float bend, bool mirrorBendOnRight, int segments, List<Vector2> outline, out int tailEnd)
        {
            outline.Clear();
            tailEnd = -1;

            if (target.sqrMagnitude < Mathf.Epsilon)
            {
                outline.AddRange(body);
                return -1;
            }

            int hitIndex = ClosestIndexToward(body, target.normalized);
            Vector2 hit = body[hitIndex];
            Vector2 toTarget = target - hit;
            float length = Mathf.Min(toTarget.magnitude - gap, maxLength);
            if (length <= MinTailLength || Vector2.Dot(toTarget, hit) <= 0f)
            {
                outline.AddRange(body);
                return -1;
            }

            float spacing = Perimeter(body) / body.Count;
            int halfSteps = Mathf.Clamp(Mathf.RoundToInt(baseWidth * 0.5f / spacing), 1, body.Count / 4);
            int indexA = Wrap(hitIndex - halfSteps, body.Count);
            int indexB = Wrap(hitIndex + halfSteps, body.Count);
            Vector2 a = body[indexA];
            Vector2 b = body[indexB];

            Vector2 direction = toTarget.normalized;
            Vector2 tip = hit + direction * length;
            Vector2 side = new(-direction.y, direction.x);
            float bendAmount = mirrorBendOnRight && target.x > 0f ? -bend : bend;
            Vector2 bendOffset = side * (bendAmount * length);

            // Body from B all the way around to A, skipping the tail's base.
            for (int i = indexB; ; i = Wrap(i + 1, body.Count))
            {
                outline.Add(body[i]);
                if (i == indexA) break;
            }

            int tailStart = outline.Count - 1;
            Vector2 controlOut = (a + tip) * 0.5f + bendOffset;
            Vector2 controlBack = (tip + b) * 0.5f + bendOffset;
            for (int s = 1; s <= segments; s++) outline.Add(Bezier(a, controlOut, tip, (float)s / segments));
            for (int s = 1; s < segments; s++) outline.Add(Bezier(tip, controlBack, b, (float)s / segments));
            tailEnd = outline.Count - 1;
            return tailStart;
        }

        public static void ApplyWobble(List<Vector2> outline, float wobble, float frequency, float seedOffset)
        {
            if (wobble <= 0f) return;

            var moved = new Vector2[outline.Count];
            float distance = 0f;
            for (int i = 0; i < outline.Count; i++)
            {
                if (i > 0) distance += Vector2.Distance(outline[i - 1], outline[i]);
                float noise = Mathf.PerlinNoise(distance * frequency + seedOffset, WobbleSeedOffset) * 2f - 1f;
                moved[i] = outline[i] + OutwardNormal(outline, i) * (noise * wobble);
            }
            outline.Clear();
            outline.AddRange(moved);
        }

        /// <summary>Changes with time when boiling, so the hand-drawn line redraws.</summary>
        public static float SeedOffset(int seed, float boilFps)
        {
            float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            float boilStep = boilFps > 0f ? Mathf.Floor(time * boilFps) : 0f;
            return (seed + boilStep) * BoilSeedStep;
        }

        /// <summary>Fan-triangulated fill: the body around its center, the tail around the middle of its base.</summary>
        public static void BuildFill(List<Vector2> outline, int tailStart, int tailEnd, Rect uvRect, Color color,
            List<Vector3> vertices, List<Color> colors, List<Vector2> uvs, List<int> triangles)
        {
            Rect bounds = Bounds(outline);

            int bodyCount = tailStart >= 0 ? tailStart + 1 : outline.Count;
            int center = AddFillVertex(Vector2.zero, bounds, uvRect, color, vertices, colors, uvs);
            int first = vertices.Count;
            for (int i = 0; i < bodyCount; i++) AddFillVertex(outline[i], bounds, uvRect, color, vertices, colors, uvs);
            for (int i = 0; i < bodyCount - 1; i++) AddTriangle(triangles, center, first + i, first + i + 1);
            AddTriangle(triangles, center, first + bodyCount - 1, first);

            if (tailStart < 0) return;

            Vector2 baseMiddle = (outline[tailStart] + outline[0]) * 0.5f;
            int tailCenter = AddFillVertex(baseMiddle, bounds, uvRect, color, vertices, colors, uvs);
            int tailFirst = vertices.Count;
            for (int i = tailStart; i <= tailEnd; i++) AddFillVertex(outline[i], bounds, uvRect, color, vertices, colors, uvs);
            AddFillVertex(outline[0], bounds, uvRect, color, vertices, colors, uvs); // B closes the tail
            int tailCount = vertices.Count - tailFirst;
            for (int i = 0; i < tailCount - 1; i++) AddTriangle(triangles, tailCenter, tailFirst + i, tailFirst + i + 1);
        }

        /// <summary>Closed stroke along the outline whose width varies like pen pressure.</summary>
        public static void BuildStroke(List<Vector2> outline, float width, float pressureVariation, float pressureFrequency,
            float seedOffset, int seed, Color color,
            List<Vector3> vertices, List<Color> colors, List<Vector2> uvs, List<int> triangles)
        {
            if (width <= 0f) return;

            int count = outline.Count;
            int start = vertices.Count;
            float distance = 0f;
            float pressureSeed = seedOffset + PressureSeedOffset;

            for (int i = 0; i < count; i++)
            {
                if (i > 0) distance += Vector2.Distance(outline[i - 1], outline[i]);
                float pressure = Mathf.PerlinNoise(distance * pressureFrequency + pressureSeed, seed) * 2f - 1f;
                float halfWidth = width * 0.5f * Mathf.Max(1f + pressureVariation * pressure, 0f);

                Vector2 normal = MiterNormal(outline, i);
                vertices.Add(outline[i] - normal * halfWidth);
                vertices.Add(outline[i] + normal * halfWidth);
                colors.Add(color);
                colors.Add(color);
                uvs.Add(Vector2.zero);
                uvs.Add(Vector2.zero);
            }

            for (int i = 0; i < count; i++)
            {
                int current = start + i * 2;
                int next = start + Wrap(i + 1, count) * 2;
                AddTriangle(triangles, current, current + 1, next + 1);
                AddTriangle(triangles, current, next + 1, next);
            }
        }

        /// <summary>The sprite's area inside its texture, in 0..1 UVs.</summary>
        public static Rect SpriteUvRect(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return new Rect(0f, 0f, 1f, 1f);
            Rect rect = sprite.textureRect;
            Texture texture = sprite.texture;
            return new Rect(rect.x / texture.width, rect.y / texture.height, rect.width / texture.width, rect.height / texture.height);
        }

        static int AddFillVertex(Vector2 point, Rect bounds, Rect uvRect, Color color,
            List<Vector3> vertices, List<Color> colors, List<Vector2> uvs)
        {
            vertices.Add(point);
            colors.Add(color);
            uvs.Add(new Vector2(
                Mathf.Lerp(uvRect.xMin, uvRect.xMax, Mathf.InverseLerp(bounds.xMin, bounds.xMax, point.x)),
                Mathf.Lerp(uvRect.yMin, uvRect.yMax, Mathf.InverseLerp(bounds.yMin, bounds.yMax, point.y))));
            return vertices.Count - 1;
        }

        static int ClosestIndexToward(List<Vector2> points, Vector2 direction)
        {
            int best = 0;
            float bestDot = float.NegativeInfinity;
            for (int i = 0; i < points.Count; i++)
            {
                float dot = Vector2.Dot(points[i].normalized, direction);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = i;
                }
            }
            return best;
        }

        static void AddTriangle(List<int> triangles, int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        static Vector2 Bezier(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * control + t * t * b;
        }

        /// <summary>Outward normal of a counter-clockwise closed polyline at point i.</summary>
        static Vector2 OutwardNormal(List<Vector2> points, int i)
        {
            Vector2 tangent = (points[Wrap(i + 1, points.Count)] - points[Wrap(i - 1, points.Count)]).normalized;
            return new Vector2(tangent.y, -tangent.x);
        }

        /// <summary>Normal scaled so the stroke keeps its width around corners (clamped at sharp tips).</summary>
        static Vector2 MiterNormal(List<Vector2> points, int i)
        {
            Vector2 previous = points[Wrap(i - 1, points.Count)];
            Vector2 current = points[i];
            Vector2 next = points[Wrap(i + 1, points.Count)];
            Vector2 inEdge = (current - previous).normalized;
            Vector2 outEdge = (next - current).normalized;
            Vector2 inNormal = new(inEdge.y, -inEdge.x);
            Vector2 outNormal = new(outEdge.y, -outEdge.x);

            Vector2 miter = (inNormal + outNormal).normalized;
            if (miter.sqrMagnitude < Mathf.Epsilon) return outNormal;
            float scale = 1f / Mathf.Max(Vector2.Dot(miter, outNormal), 1f / MiterLimit);
            return miter * scale;
        }

        static float Perimeter(List<Vector2> points)
        {
            float total = 0f;
            for (int i = 0; i < points.Count; i++) total += Vector2.Distance(points[i], points[Wrap(i + 1, points.Count)]);
            return total;
        }

        static void Resample(List<Vector2> source, int count, List<Vector2> result)
        {
            result.Clear();
            float step = Perimeter(source) / count;
            float carried = 0f;
            result.Add(source[0]);
            for (int i = 0; i < source.Count && result.Count < count; i++)
            {
                Vector2 a = source[i];
                Vector2 b = source[Wrap(i + 1, source.Count)];
                float segment = Vector2.Distance(a, b);
                float position = step - carried;
                while (position <= segment && result.Count < count)
                {
                    result.Add(Vector2.Lerp(a, b, position / segment));
                    position += step;
                }
                carried = segment - (position - step);
            }
        }

        static Rect Bounds(List<Vector2> points)
        {
            Vector2 min = points[0];
            Vector2 max = points[0];
            foreach (Vector2 point in points)
            {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static int Wrap(int index, int count) => ((index % count) + count) % count;
    }
}
