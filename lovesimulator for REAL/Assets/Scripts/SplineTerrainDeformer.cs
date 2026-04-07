using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class SplineTerrainDeformer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SplineSampler splineSampler;
    [SerializeField] private List<Terrain> targetTerrains = new List<Terrain>();

    [Header("Sampling")]
    [SerializeField] private int resolutionPerSpline = 100;

    [Header("Road Shape")]
    [SerializeField] private float roadHalfWidth = 4f;
    [SerializeField] private float shoulderWidth = 3f;
    [SerializeField] private float longitudinalBlend = 2f;
    [SerializeField] private float heightOffset = -0.05f;

    [Header("Blend")]
    [SerializeField, Range(0f, 1f)] private float strength = 1f;
    [SerializeField] private bool lowerOnly = false;
    [SerializeField] private bool raiseOnly = false;

    [Header("Orientation")]
    [SerializeField] private bool useSplineUpAsPlaneNormal = true;
    [SerializeField] private bool flattenToWorldUp = false;

    [Header("Post Process")]
    [SerializeField] private bool smoothAfterDeform = true;
    [SerializeField, Range(1, 8)] private int smoothIterations = 1;
    [SerializeField, Range(0f, 1f)] private float smoothStrength = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;
    [SerializeField] private Color innerColor = Color.green;
    [SerializeField] private Color outerColor = Color.yellow;
    [SerializeField] private float debugLineHeight = 0.1f;

    private class TerrainWorkData
    {
        public Terrain terrain;
        public TerrainData terrainData;
        public Vector3 terrainPos;
        public Vector3 terrainSize;
        public int heightmapResolution;
        public float[,] heights;
        public Bounds worldBounds;
    }

    [ContextMenu("Auto Fill Terrains From Scene")]
    public void AutoFillTerrainsFromScene()
    {
        targetTerrains.Clear();
        targetTerrains.AddRange(Terrain.activeTerrains);
    }

    [ContextMenu("Deform Terrain")]
    public void DeformTerrain()
    {
        if (splineSampler == null)
            return;

        List<TerrainWorkData> terrainDatas = BuildTerrainWorkData();
#if UNITY_EDITOR
        for (int i = 0; i < terrainDatas.Count; i++)
        {
            if (terrainDatas[i].terrainData != null)
            {
                UnityEditor.Undo.RegisterCompleteObjectUndo(
                    terrainDatas[i].terrainData,
                    "Spline Terrain Deform"
                );
            }
        }
#endif
        if (terrainDatas.Count == 0)
            return;

        int splineCount = splineSampler.NumSplines;
        if (splineCount <= 0)
            return;

        resolutionPerSpline = Mathf.Max(2, resolutionPerSpline);
        float safeLongitudinalBlend = Mathf.Max(0.01f, longitudinalBlend);
        float totalHalfWidth = Mathf.Max(0.01f, roadHalfWidth + shoulderWidth);

        for (int splineIndex = 0; splineIndex < splineCount; splineIndex++)
        {
            for (int i = 0; i <= resolutionPerSpline; i++)
            {
                float t = i / (float)resolutionPerSpline;

                if (!splineSampler.SampleFrame(splineIndex, t, out Vector3 center, out Vector3 forward, out Vector3 up, out Vector3 right))
                    continue;

                Vector3 planeNormal = GetPlaneNormal(forward, up, right);
                if (planeNormal.sqrMagnitude < 0.000001f)
                    planeNormal = Vector3.up;

                Bounds stampBounds = BuildStampBounds(center, right, forward, totalHalfWidth, safeLongitudinalBlend);

                for (int terrainIndex = 0; terrainIndex < terrainDatas.Count; terrainIndex++)
                {
                    TerrainWorkData twd = terrainDatas[terrainIndex];

                    if (!twd.worldBounds.Intersects(stampBounds))
                        continue;

                    StampRibbonSample(
                        twd,
                        center,
                        forward,
                        right,
                        planeNormal,
                        roadHalfWidth,
                        shoulderWidth,
                        safeLongitudinalBlend
                    );
                }
            }
        }

        if (smoothAfterDeform)
        {
            for (int terrainIndex = 0; terrainIndex < terrainDatas.Count; terrainIndex++)
            {
                for (int i = 0; i < smoothIterations; i++)
                    SmoothHeights(terrainDatas[terrainIndex].heights, smoothStrength);
            }
        }

        for (int terrainIndex = 0; terrainIndex < terrainDatas.Count; terrainIndex++)
        {
            TerrainWorkData twd = terrainDatas[terrainIndex];
            twd.terrainData.SetHeights(0, 0, twd.heights);
        }
    }

    private List<TerrainWorkData> BuildTerrainWorkData()
    {
        List<TerrainWorkData> results = new List<TerrainWorkData>();

        for (int i = 0; i < targetTerrains.Count; i++)
        {
            Terrain terrain = targetTerrains[i];
            if (terrain == null)
                continue;

            TerrainData terrainData = terrain.terrainData;
            if (terrainData == null)
                continue;

            int hmRes = terrainData.heightmapResolution;
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = terrainData.size;

            TerrainWorkData twd = new TerrainWorkData
            {
                terrain = terrain,
                terrainData = terrainData,
                terrainPos = terrainPos,
                terrainSize = terrainSize,
                heightmapResolution = hmRes,
                heights = terrainData.GetHeights(0, 0, hmRes, hmRes),
                worldBounds = new Bounds(
                    terrainPos + new Vector3(terrainSize.x * 0.5f, terrainSize.y * 0.5f, terrainSize.z * 0.5f),
                    terrainSize
                )
            };

            results.Add(twd);
        }

        return results;
    }

    private Bounds BuildStampBounds(Vector3 center, Vector3 right, Vector3 forward, float halfWidth, float halfLength)
    {
        Vector3 extents =
            new Vector3(
                Mathf.Abs(right.x) * halfWidth + Mathf.Abs(forward.x) * halfLength,
                10000f,
                Mathf.Abs(right.z) * halfWidth + Mathf.Abs(forward.z) * halfLength
            );

        return new Bounds(center, extents * 2f);
    }

    private Vector3 GetPlaneNormal(Vector3 forward, Vector3 up, Vector3 right)
    {
        if (flattenToWorldUp)
            return Vector3.up;

        if (useSplineUpAsPlaneNormal)
            return up.normalized;

        Vector3 normal = Vector3.Cross(right, forward).normalized;
        if (normal.sqrMagnitude < 0.000001f)
            normal = up.normalized;

        return normal;
    }

    private void StampRibbonSample(
        TerrainWorkData twd,
        Vector3 centerWorld,
        Vector3 forward,
        Vector3 right,
        Vector3 planeNormal,
        float innerHalfWidth,
        float shoulder,
        float longitudinalRange)
    {
        float totalHalfWidth = innerHalfWidth + shoulder;
        if (totalHalfWidth <= 0.0001f)
            return;

        float metersPerPixelX = twd.terrainSize.x / (twd.heightmapResolution - 1);
        float metersPerPixelZ = twd.terrainSize.z / (twd.heightmapResolution - 1);

        int radiusPixelsX = Mathf.CeilToInt((totalHalfWidth + longitudinalRange) / metersPerPixelX);
        int radiusPixelsZ = Mathf.CeilToInt((totalHalfWidth + longitudinalRange) / metersPerPixelZ);

        float normalizedX = Mathf.InverseLerp(twd.terrainPos.x, twd.terrainPos.x + twd.terrainSize.x, centerWorld.x);
        float normalizedZ = Mathf.InverseLerp(twd.terrainPos.z, twd.terrainPos.z + twd.terrainSize.z, centerWorld.z);

        int centerHX = Mathf.RoundToInt(normalizedX * (twd.heightmapResolution - 1));
        int centerHZ = Mathf.RoundToInt(normalizedZ * (twd.heightmapResolution - 1));

        int minX = Mathf.Max(0, centerHX - radiusPixelsX);
        int maxX = Mathf.Min(twd.heightmapResolution - 1, centerHX + radiusPixelsX);
        int minZ = Mathf.Max(0, centerHZ - radiusPixelsZ);
        int maxZ = Mathf.Min(twd.heightmapResolution - 1, centerHZ + radiusPixelsZ);

        for (int z = minZ; z <= maxZ; z++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float worldX = twd.terrainPos.x + (x / (float)(twd.heightmapResolution - 1)) * twd.terrainSize.x;
                float worldZ = twd.terrainPos.z + (z / (float)(twd.heightmapResolution - 1)) * twd.terrainSize.z;
                Vector3 worldPoint = new Vector3(worldX, centerWorld.y, worldZ);

                Vector3 toPoint = worldPoint - centerWorld;

                float lateral = Mathf.Abs(Vector3.Dot(toPoint, right));
                float longitudinal = Mathf.Abs(Vector3.Dot(toPoint, forward));

                if (lateral > totalHalfWidth)
                    continue;

                if (longitudinal > longitudinalRange)
                    continue;

                float lateralBlend = ComputeWidthBlend(lateral, innerHalfWidth, shoulder);
                float longitudinalBlendFactor = ComputeLongitudinalBlend(longitudinal, longitudinalRange);
                float blend = lateralBlend * longitudinalBlendFactor * strength;

                if (blend <= 0f)
                    continue;

                if (!TrySolvePlaneHeightAtXZ(centerWorld, planeNormal, worldX, worldZ, out float projectedY))
                    continue;

                projectedY += heightOffset;

                float currentNormalizedHeight = twd.heights[z, x];
                float currentWorldHeight = twd.terrainPos.y + currentNormalizedHeight * twd.terrainSize.y;
                float desiredWorldHeight = Mathf.Lerp(currentWorldHeight, projectedY, blend);

                if (lowerOnly)
                    desiredWorldHeight = Mathf.Min(currentWorldHeight, desiredWorldHeight);

                if (raiseOnly)
                    desiredWorldHeight = Mathf.Max(currentWorldHeight, desiredWorldHeight);

                float desiredNormalizedHeight = Mathf.InverseLerp(
                    twd.terrainPos.y,
                    twd.terrainPos.y + twd.terrainSize.y,
                    desiredWorldHeight
                );

                twd.heights[z, x] = Mathf.Clamp01(desiredNormalizedHeight);
            }
        }
    }

    private float ComputeWidthBlend(float lateralDistance, float innerHalfWidth, float shoulder)
    {
        if (lateralDistance <= innerHalfWidth)
            return 1f;

        if (shoulder <= 0.0001f)
            return 0f;

        float t = Mathf.InverseLerp(innerHalfWidth, innerHalfWidth + shoulder, lateralDistance);
        t = Mathf.Clamp01(t);

        return 1f - Smooth01(t);
    }

    private float ComputeLongitudinalBlend(float longitudinalDistance, float range)
    {
        if (range <= 0.0001f)
            return 1f;

        float t = Mathf.InverseLerp(0f, range, longitudinalDistance);
        t = Mathf.Clamp01(t);

        return 1f - Smooth01(t);
    }

    private float Smooth01(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private bool TrySolvePlaneHeightAtXZ(Vector3 pointOnPlane, Vector3 planeNormal, float x, float z, out float y)
    {
        y = pointOnPlane.y;

        if (Mathf.Abs(planeNormal.y) < 0.0001f)
            return false;

        float nx = planeNormal.x;
        float ny = planeNormal.y;
        float nz = planeNormal.z;

        y = pointOnPlane.y - ((nx * (x - pointOnPlane.x)) + (nz * (z - pointOnPlane.z))) / ny;
        return float.IsFinite(y);
    }

    private void SmoothHeights(float[,] heights, float blendStrength)
    {
        int h = heights.GetLength(0);
        int w = heights.GetLength(1);

        float[,] copy = (float[,])heights.Clone();

        for (int z = 1; z < h - 1; z++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                float sum =
                    copy[z, x] +
                    copy[z - 1, x] +
                    copy[z + 1, x] +
                    copy[z, x - 1] +
                    copy[z, x + 1] +
                    copy[z - 1, x - 1] +
                    copy[z - 1, x + 1] +
                    copy[z + 1, x - 1] +
                    copy[z + 1, x + 1];

                float average = sum / 9f;
                heights[z, x] = Mathf.Lerp(copy[z, x], average, blendStrength);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (!drawDebug || splineSampler == null)
            return;

        int splineCount = splineSampler.NumSplines;
        if (splineCount <= 0)
            return;

        int steps = Mathf.Max(2, Mathf.Min(48, resolutionPerSpline));

        for (int splineIndex = 0; splineIndex < splineCount; splineIndex++)
        {
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;

                if (!splineSampler.SampleFrame(splineIndex, t, out Vector3 center, out Vector3 forward, out Vector3 up, out Vector3 right))
                    continue;

                Vector3 leftInner = center - right * roadHalfWidth + Vector3.up * debugLineHeight;
                Vector3 rightInner = center + right * roadHalfWidth + Vector3.up * debugLineHeight;

                Vector3 leftOuter = center - right * (roadHalfWidth + shoulderWidth) + Vector3.up * debugLineHeight;
                Vector3 rightOuter = center + right * (roadHalfWidth + shoulderWidth) + Vector3.up * debugLineHeight;

                Gizmos.color = innerColor;
                Gizmos.DrawLine(leftInner, rightInner);

                Gizmos.color = outerColor;
                Gizmos.DrawLine(leftOuter, rightOuter);

                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(center + Vector3.up * debugLineHeight, center + Vector3.up * debugLineHeight + forward * 2f);

                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(center + Vector3.up * debugLineHeight, center + Vector3.up * debugLineHeight + up * 2f);
            }
        }
    }
}