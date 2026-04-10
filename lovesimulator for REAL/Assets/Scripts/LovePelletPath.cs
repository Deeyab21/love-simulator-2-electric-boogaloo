using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class LovePelletPath : MonoBehaviour
{
    [Header("Pellet Prefab")]
    public GameObject pelletPrefab;

    [Header("Placement")]
    public float spacing = 1.5f;
    public float verticalOffset = 1.0f;
    public bool orientToPath = false;

    [Header("Curve Sampling")]
    [Tooltip("Higher = smoother/more accurate curve sampling")]
    public int samplesPerSegment = 20;

    [Header("Point Settings")]
    public string pointPrefix = "Point_";
    public float newPointForwardDistance = 5f;

    [Header("Generated Root")]
    public string generatedRootName = "__GeneratedPellets";

    [Header("Debug")]
    public bool drawCurve = true;
    public bool drawPointLines = true;
    public Color curveColor = Color.magenta;
    public Color pointColor = Color.yellow;
    public Color lineColor = Color.cyan;
    public float pointGizmoRadius = 0.2f;

    private Transform generatedRoot;

    public List<Transform> GetOrderedPoints()
    {
        List<Transform> points = new List<Transform>();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (child.name == generatedRootName)
                continue;

            points.Add(child);
        }

        points.Sort((a, b) => a.GetSiblingIndex().CompareTo(b.GetSiblingIndex()));
        return points;
    }

    public void AddPoint()
    {
        GameObject point = new GameObject();
        point.name = pointPrefix + GetNextPointIndex().ToString("00");
        point.transform.SetParent(transform, false);

        List<Transform> points = GetOrderedPoints();

        if (points.Count <= 1)
        {
            point.transform.localPosition = Vector3.zero;
        }
        else if (points.Count == 2)
        {
            Transform first = points[0];
            point.transform.localPosition = first.localPosition + Vector3.forward * newPointForwardDistance;
        }
        else
        {
            Transform last = points[points.Count - 2];
            Transform prev = points[points.Count - 3];

            Vector3 dir = (last.localPosition - prev.localPosition).normalized;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector3.forward;

            point.transform.localPosition = last.localPosition + dir * newPointForwardDistance;
        }

        point.transform.localRotation = Quaternion.identity;
        point.transform.localScale = Vector3.one;
    }

    public void RebuildPellets()
    {
        EnsureGeneratedRoot();
        ClearGeneratedPelletsImmediate();

        if (pelletPrefab == null)
        {
            Debug.LogWarning("LovePelletPath: No pellet prefab assigned.", this);
            return;
        }

        List<Transform> points = GetOrderedPoints();

        if (points.Count < 2)
        {
            Debug.LogWarning("LovePelletPath: Need at least 2 point transforms.", this);
            return;
        }

        int safeSamples = Mathf.Max(4, samplesPerSegment);
        float safeSpacing = Mathf.Max(0.05f, spacing);

        List<CurveSample> curveSamples = BuildCurveSamples(points, safeSamples);

        if (curveSamples.Count == 0)
            return;

        SpawnPellet(curveSamples[0].position, curveSamples[0].tangent, 0);

        float accumulatedDistance = 0f;
        Vector3 lastPlacedPosition = curveSamples[0].position;
        int pelletIndex = 1;

        for (int i = 1; i < curveSamples.Count; i++)
        {
            accumulatedDistance += Vector3.Distance(curveSamples[i - 1].position, curveSamples[i].position);

            if (accumulatedDistance >= safeSpacing)
            {
                SpawnPellet(curveSamples[i].position, curveSamples[i].tangent, pelletIndex);
                lastPlacedPosition = curveSamples[i].position;
                pelletIndex++;
                accumulatedDistance = 0f;
            }
        }

        CurveSample finalSample = curveSamples[curveSamples.Count - 1];
        if (Vector3.Distance(lastPlacedPosition, finalSample.position) > safeSpacing * 0.35f)
        {
            SpawnPellet(finalSample.position, finalSample.tangent, pelletIndex);
        }
    }

    private struct CurveSample
    {
        public Vector3 position;
        public Vector3 tangent;
    }

    private List<CurveSample> BuildCurveSamples(List<Transform> points, int stepsPerSegment)
    {
        List<CurveSample> samples = new List<CurveSample>();

        if (points.Count < 2)
            return samples;

        for (int seg = 0; seg < points.Count - 1; seg++)
        {
            Vector3 p0 = GetPointPosition(points, seg - 1);
            Vector3 p1 = GetPointPosition(points, seg);
            Vector3 p2 = GetPointPosition(points, seg + 1);
            Vector3 p3 = GetPointPosition(points, seg + 2);

            for (int step = 0; step <= stepsPerSegment; step++)
            {
                if (seg > 0 && step == 0)
                    continue;

                float t = step / (float)stepsPerSegment;

                Vector3 localPos = CatmullRom(p0, p1, p2, p3, t);
                Vector3 localTangent = CatmullRomTangent(p0, p1, p2, p3, t);

                if (localTangent.sqrMagnitude < 0.0001f)
                    localTangent = Vector3.forward;

                localTangent.Normalize();

                samples.Add(new CurveSample
                {
                    position = localPos + Vector3.up * verticalOffset,
                    tangent = localTangent
                });
            }
        }

        return samples;
    }

    private Vector3 GetPointPosition(List<Transform> points, int index)
    {
        if (index < 0)
            return points[0].localPosition;

        if (index >= points.Count)
            return points[points.Count - 1].localPosition;

        return points[index].localPosition;
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;

        return 0.5f * (
            (-p0 + p2) +
            2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t +
            3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2
        );
    }

    private void SpawnPellet(Vector3 localPos, Vector3 tangent, int pelletIndex)
    {
        GameObject pellet;

        if (Application.isPlaying)
        {
            pellet = Instantiate(pelletPrefab, transform);
        }
        else
        {
#if UNITY_EDITOR
            pellet = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(pelletPrefab, transform);
#else
            pellet = Instantiate(pelletPrefab, transform);
#endif
        }

        pellet.name = $"Pellet_{pelletIndex:000}";
        pellet.transform.SetParent(generatedRoot, false);
        pellet.transform.localPosition = localPos;

        if (orientToPath)
        {
            Vector3 flatForward = tangent;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude > 0.0001f)
            {
                pellet.transform.localRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            }
            else
            {
                pellet.transform.localRotation = Quaternion.identity;
            }
        }
        else
        {
            pellet.transform.localRotation = Quaternion.identity;
        }

        pellet.transform.localScale = pelletPrefab.transform.localScale;
    }

    public void ClearGeneratedPellets()
    {
        EnsureGeneratedRoot();

        if (Application.isPlaying)
            ClearGeneratedPelletsRuntime();
        else
            ClearGeneratedPelletsImmediate();
    }

    private void EnsureGeneratedRoot()
    {
        Transform existing = transform.Find(generatedRootName);
        if (existing != null)
        {
            generatedRoot = existing;
            return;
        }

        GameObject root = new GameObject(generatedRootName);
        root.transform.SetParent(transform, false);
        generatedRoot = root.transform;
        generatedRoot.SetSiblingIndex(transform.childCount - 1);
    }

    private void ClearGeneratedPelletsRuntime()
    {
        EnsureGeneratedRoot();

        List<GameObject> toDestroy = new List<GameObject>();
        for (int i = 0; i < generatedRoot.childCount; i++)
        {
            toDestroy.Add(generatedRoot.GetChild(i).gameObject);
        }

        for (int i = 0; i < toDestroy.Count; i++)
        {
            Destroy(toDestroy[i]);
        }
    }

    private void ClearGeneratedPelletsImmediate()
    {
        EnsureGeneratedRoot();

        List<GameObject> toDestroy = new List<GameObject>();
        for (int i = 0; i < generatedRoot.childCount; i++)
        {
            toDestroy.Add(generatedRoot.GetChild(i).gameObject);
        }

        for (int i = 0; i < toDestroy.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(toDestroy[i]);
            else
                Destroy(toDestroy[i]);
#else
            Destroy(toDestroy[i]);
#endif
        }
    }

    private int GetNextPointIndex()
    {
        int highest = -1;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (child.name == generatedRootName)
                continue;

            if (!child.name.StartsWith(pointPrefix))
                continue;

            string suffix = child.name.Substring(pointPrefix.Length);
            if (int.TryParse(suffix, out int parsed))
            {
                if (parsed > highest)
                    highest = parsed;
            }
        }

        return highest + 1;
    }

    private void OnDrawGizmos()
    {
        if (!drawCurve)
            return;

        List<Transform> points = GetOrderedPoints();
        if (points.Count == 0)
            return;

        Gizmos.matrix = transform.localToWorldMatrix;

        if (drawPointLines)
        {
            Gizmos.color = lineColor;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Gizmos.DrawLine(points[i].localPosition, points[i + 1].localPosition);
            }
        }

        Gizmos.color = pointColor;
        for (int i = 0; i < points.Count; i++)
        {
            Gizmos.DrawSphere(points[i].localPosition, pointGizmoRadius);
        }

        if (points.Count < 2)
            return;

        Gizmos.color = curveColor;

        int safeSamples = Mathf.Max(4, samplesPerSegment);

        for (int seg = 0; seg < points.Count - 1; seg++)
        {
            Vector3 p0 = GetPointPosition(points, seg - 1);
            Vector3 p1 = GetPointPosition(points, seg);
            Vector3 p2 = GetPointPosition(points, seg + 1);
            Vector3 p3 = GetPointPosition(points, seg + 2);

            Vector3 prev = CatmullRom(p0, p1, p2, p3, 0f);

            for (int step = 1; step <= safeSamples; step++)
            {
                float t = step / (float)safeSamples;
                Vector3 cur = CatmullRom(p0, p1, p2, p3, t);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }
    }

    [ContextMenu("Add Point")]
    private void ContextAddPoint()
    {
        AddPoint();
    }

    [ContextMenu("Rebuild Pellets")]
    private void ContextRebuild()
    {
        RebuildPellets();
    }

    [ContextMenu("Clear Generated Pellets")]
    private void ContextClear()
    {
        ClearGeneratedPellets();
    }
}