using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ChainDashTargetGlow : MonoBehaviour
{
    [Header("Glow Material")]
    [Tooltip("Material using the Custom/TargetPreviewGlow shader.")]
    public Material glowMaterial;

    [Tooltip("Glow color shown when this target is previewed.")]
    public Color glowColor = Color.red;

    [Tooltip("Transparency of the glow shell.")]
    [Range(0f, 1f)] public float glowAlpha = 0.3f;

    [Tooltip("How strong/bright the glow appears.")]
    [Range(0f, 8f)] public float glowIntensity = 2.5f;

    [Tooltip("How edge-focused the fresnel is.")]
    [Range(0.1f, 8f)] public float fresnelPower = 1.8f;

    [Header("Optional Pulse")]
    public bool pulseGlow = true;
    public float pulseSpeed = 6f;
    public float pulseAmount = 0.12f;

    [Header("Renderer Search")]
    [Tooltip("If true, automatically uses all child renderers.")]
    public bool autoFindRenderers = true;

    [Tooltip("Optional explicit renderers to glow. Leave empty to auto-find.")]
    public Renderer[] renderersToGlow;

    private readonly List<GameObject> glowObjects = new List<GameObject>();
    private Material runtimeMaterial;
    private bool isHighlighted;

    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");
    private static readonly int FresnelPowerId = Shader.PropertyToID("_FresnelPower");

    private void Awake()
    {
        RebuildGlowObjects();
        SetHighlighted(false);
    }

    private void Update()
    {
        if (!isHighlighted || runtimeMaterial == null)
            return;

        float finalAlpha = glowAlpha;
        float finalIntensity = glowIntensity;

        if (pulseGlow)
        {
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) * 0.5f) + 0.5f;
            finalAlpha += pulse * pulseAmount;
            finalIntensity += pulse * pulseAmount * 2f;
        }

        runtimeMaterial.SetColor(GlowColorId, glowColor);
        runtimeMaterial.SetFloat(AlphaId, finalAlpha);
        runtimeMaterial.SetFloat(GlowIntensityId, finalIntensity);
        runtimeMaterial.SetFloat(FresnelPowerId, fresnelPower);
    }

    public void SetHighlighted(bool highlighted)
    {
        isHighlighted = highlighted;

        if (runtimeMaterial != null)
        {
            runtimeMaterial.SetColor(GlowColorId, glowColor);
            runtimeMaterial.SetFloat(AlphaId, glowAlpha);
            runtimeMaterial.SetFloat(GlowIntensityId, glowIntensity);
            runtimeMaterial.SetFloat(FresnelPowerId, fresnelPower);
        }

        for (int i = 0; i < glowObjects.Count; i++)
        {
            if (glowObjects[i] != null)
                glowObjects[i].SetActive(highlighted);
        }
    }

    public void RebuildGlowObjects()
    {
        ClearGlowObjects();
        EnsureRuntimeMaterial();

        Renderer[] sources = GetSourceRenderers();

        for (int i = 0; i < sources.Length; i++)
        {
            Renderer source = sources[i];
            if (source == null)
                continue;

            if (source.GetComponentInParent<ChainDashTargetGlowShell>() != null)
                continue;

            if (source is SkinnedMeshRenderer skinned)
            {
                CreateSkinnedGlow(skinned);
            }
            else
            {
                MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
                MeshRenderer sourceRenderer = source as MeshRenderer;

                if (sourceFilter != null && sourceRenderer != null && sourceFilter.sharedMesh != null)
                    CreateMeshGlow(sourceFilter, sourceRenderer);
            }
        }
    }

    private Renderer[] GetSourceRenderers()
    {
        if (renderersToGlow != null && renderersToGlow.Length > 0)
            return renderersToGlow;

        if (!autoFindRenderers)
            return new Renderer[0];

        return GetComponentsInChildren<Renderer>(true);
    }

    private void CreateMeshGlow(MeshFilter sourceFilter, MeshRenderer sourceRenderer)
    {
        GameObject glowObject = new GameObject(sourceRenderer.gameObject.name + "_GlowShell");
        glowObject.transform.SetParent(sourceRenderer.transform, false);
        glowObject.transform.localPosition = Vector3.zero;
        glowObject.transform.localRotation = Quaternion.identity;
        glowObject.transform.localScale = Vector3.one;

        glowObject.AddComponent<ChainDashTargetGlowShell>();

        MeshFilter glowFilter = glowObject.AddComponent<MeshFilter>();
        glowFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer glowRenderer = glowObject.AddComponent<MeshRenderer>();
        ApplyGlowMaterials(glowRenderer, sourceFilter.sharedMesh.subMeshCount);

        glowObject.SetActive(false);
        glowObjects.Add(glowObject);
    }

    private void CreateSkinnedGlow(SkinnedMeshRenderer sourceRenderer)
    {
        if (sourceRenderer.sharedMesh == null)
            return;

        GameObject glowObject = new GameObject(sourceRenderer.gameObject.name + "_GlowShell");
        glowObject.transform.SetParent(sourceRenderer.transform, false);
        glowObject.transform.localPosition = Vector3.zero;
        glowObject.transform.localRotation = Quaternion.identity;
        glowObject.transform.localScale = Vector3.one;

        glowObject.AddComponent<ChainDashTargetGlowShell>();

        SkinnedMeshRenderer glowRenderer = glowObject.AddComponent<SkinnedMeshRenderer>();
        glowRenderer.sharedMesh = sourceRenderer.sharedMesh;
        glowRenderer.bones = sourceRenderer.bones;
        glowRenderer.rootBone = sourceRenderer.rootBone;
        glowRenderer.updateWhenOffscreen = sourceRenderer.updateWhenOffscreen;
        glowRenderer.localBounds = sourceRenderer.localBounds;

        ApplyGlowMaterials(glowRenderer, sourceRenderer.sharedMesh.subMeshCount);

        glowObject.SetActive(false);
        glowObjects.Add(glowObject);
    }

    private void ApplyGlowMaterials(Renderer renderer, int subMeshCount)
    {
        if (runtimeMaterial == null)
            return;

        int count = Mathf.Max(1, subMeshCount);
        Material[] mats = new Material[count];

        for (int i = 0; i < count; i++)
            mats[i] = runtimeMaterial;

        renderer.sharedMaterials = mats;
    }

    private void EnsureRuntimeMaterial()
    {
        if (runtimeMaterial != null)
            return;

        if (glowMaterial == null)
            return;

        runtimeMaterial = new Material(glowMaterial);
        runtimeMaterial.name = glowMaterial.name + "_Runtime";
        runtimeMaterial.SetColor(GlowColorId, glowColor);
        runtimeMaterial.SetFloat(AlphaId, glowAlpha);
        runtimeMaterial.SetFloat(GlowIntensityId, glowIntensity);
        runtimeMaterial.SetFloat(FresnelPowerId, fresnelPower);
    }

    private void ClearGlowObjects()
    {
        for (int i = glowObjects.Count - 1; i >= 0; i--)
        {
            if (glowObjects[i] == null)
                continue;

            if (Application.isPlaying)
                Destroy(glowObjects[i]);
            else
                DestroyImmediate(glowObjects[i]);
        }

        glowObjects.Clear();
    }

    private void OnDestroy()
    {
        ClearGlowObjects();

        if (runtimeMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(runtimeMaterial);
            else
                DestroyImmediate(runtimeMaterial);
        }
    }
}

public class ChainDashTargetGlowShell : MonoBehaviour
{
}