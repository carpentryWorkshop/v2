using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates path points for different shapes to be cut by the CNC machine in Auto mode.
/// All paths are returned as List<Vector3> where X/Z define the horizontal position
/// and Y defines the cutting depth.
///
/// This is a static utility class - no MonoBehaviour required.
/// </summary>
public static class ShapePathGenerator
{
    /// <summary>
    /// Available shape types for auto-cutting.
    /// </summary>
    public enum ShapeType
    {
        Rectangle,
        Circle,
        Triangle,
        Star
    }

    /// <summary>
    /// Generates a path for the specified shape type.
    /// </summary>
    /// <param name="shapeType">The shape to generate.</param>
    /// <param name="size">Size of the shape (width/height for rectangle, diameter for others).</param>
    /// <param name="center">Center position in XZ plane.</param>
    /// <param name="cutDepth">Y position for cutting (negative = down into wood).</param>
    /// <param name="idleHeight">Y position when not cutting (above wood).</param>
    /// <returns>List of Vector3 points defining the path.</returns>
    public static List<Vector3> GeneratePath(
        ShapeType shapeType,
        float size,
        Vector2 center,
        float cutDepth,
        float idleHeight)
    {
        return shapeType switch
        {
            ShapeType.Rectangle => GenerateRectangle(size, size, center, cutDepth, idleHeight),
            ShapeType.Circle => GenerateCircle(size / 2f, 32, center, cutDepth, idleHeight),
            ShapeType.Triangle => GenerateTriangle(size, center, cutDepth, idleHeight),
            ShapeType.Star => GenerateStar(size / 2f, size / 4f, 5, center, cutDepth, idleHeight),
            _ => new List<Vector3>()
        };
    }

    /// <summary>
    /// Generates a rectangular path.
    /// </summary>
    public static List<Vector3> GenerateRectangle(
        float width,
        float height,
        Vector2 center,
        float cutDepth,
        float idleHeight)
    {
        var path = new List<Vector3>();

        float halfW = width / 2f;
        float halfH = height / 2f;

        // Corner points
        Vector2 topLeft = center + new Vector2(-halfW, halfH);
        Vector2 topRight = center + new Vector2(halfW, halfH);
        Vector2 bottomRight = center + new Vector2(halfW, -halfH);
        Vector2 bottomLeft = center + new Vector2(-halfW, -halfH);

        // Move to start position (above, then plunge)
        path.Add(new Vector3(topLeft.x, idleHeight, topLeft.y));
        path.Add(new Vector3(topLeft.x, cutDepth, topLeft.y));

        // Cut the rectangle
        path.Add(new Vector3(topRight.x, cutDepth, topRight.y));
        path.Add(new Vector3(bottomRight.x, cutDepth, bottomRight.y));
        path.Add(new Vector3(bottomLeft.x, cutDepth, bottomLeft.y));
        path.Add(new Vector3(topLeft.x, cutDepth, topLeft.y)); // Close the shape

        // Retract
        path.Add(new Vector3(topLeft.x, idleHeight, topLeft.y));

        return path;
    }

    /// <summary>
    /// Generates a circular path.
    /// </summary>
    public static List<Vector3> GenerateCircle(
        float radius,
        int segments,
        Vector2 center,
        float cutDepth,
        float idleHeight)
    {
        var path = new List<Vector3>();

        if (segments < 3) segments = 3;

        // Calculate first point
        float startAngle = 0f;
        Vector2 startPoint = center + new Vector2(
            Mathf.Cos(startAngle) * radius,
            Mathf.Sin(startAngle) * radius
        );

        // Move to start and plunge
        path.Add(new Vector3(startPoint.x, idleHeight, startPoint.y));
        path.Add(new Vector3(startPoint.x, cutDepth, startPoint.y));

        // Generate circle points
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector2 point = center + new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );
            path.Add(new Vector3(point.x, cutDepth, point.y));
        }

        // Retract
        path.Add(new Vector3(startPoint.x, idleHeight, startPoint.y));

        return path;
    }

    /// <summary>
    /// Generates an equilateral triangle path.
    /// </summary>
    public static List<Vector3> GenerateTriangle(
        float size,
        Vector2 center,
        float cutDepth,
        float idleHeight)
    {
        var path = new List<Vector3>();

        // Equilateral triangle vertices
        float radius = size / Mathf.Sqrt(3f); // Circumradius

        Vector2[] vertices = new Vector2[3];
        for (int i = 0; i < 3; i++)
        {
            float angle = (i / 3f) * Mathf.PI * 2f - Mathf.PI / 2f; // Start from top
            vertices[i] = center + new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );
        }

        // Move to start and plunge
        path.Add(new Vector3(vertices[0].x, idleHeight, vertices[0].y));
        path.Add(new Vector3(vertices[0].x, cutDepth, vertices[0].y));

        // Cut the triangle
        path.Add(new Vector3(vertices[1].x, cutDepth, vertices[1].y));
        path.Add(new Vector3(vertices[2].x, cutDepth, vertices[2].y));
        path.Add(new Vector3(vertices[0].x, cutDepth, vertices[0].y)); // Close

        // Retract
        path.Add(new Vector3(vertices[0].x, idleHeight, vertices[0].y));

        return path;
    }

    /// <summary>
    /// Generates a star path.
    /// </summary>
    public static List<Vector3> GenerateStar(
        float outerRadius,
        float innerRadius,
        int points,
        Vector2 center,
        float cutDepth,
        float idleHeight)
    {
        var path = new List<Vector3>();

        if (points < 3) points = 3;

        int totalVertices = points * 2;
        Vector2[] vertices = new Vector2[totalVertices];

        for (int i = 0; i < totalVertices; i++)
        {
            float angle = (i / (float)totalVertices) * Mathf.PI * 2f - Mathf.PI / 2f;
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            vertices[i] = center + new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );
        }

        // Move to start and plunge
        path.Add(new Vector3(vertices[0].x, idleHeight, vertices[0].y));
        path.Add(new Vector3(vertices[0].x, cutDepth, vertices[0].y));

        // Cut the star
        for (int i = 1; i < totalVertices; i++)
        {
            path.Add(new Vector3(vertices[i].x, cutDepth, vertices[i].y));
        }
        path.Add(new Vector3(vertices[0].x, cutDepth, vertices[0].y)); // Close

        // Retract
        path.Add(new Vector3(vertices[0].x, idleHeight, vertices[0].y));

        return path;
    }

    /// <summary>
    /// Gets a preview of the shape path for display purposes (XZ only, normalized 0-1).
    /// </summary>
    public static List<Vector2> GetShapePreview(ShapeType shapeType, int resolution = 32)
    {
        var preview = new List<Vector2>();
        var path = GeneratePath(shapeType, 0.8f, Vector2.one * 0.5f, 0f, 0f);

        foreach (var point in path)
        {
            preview.Add(new Vector2(point.x, point.z));
        }

        return preview;
    }
}
