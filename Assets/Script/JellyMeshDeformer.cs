using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// [배치 위치] 자식 오브젝트 JellyBody에 붙입니다.
/// MeshFilter, MeshRenderer와 같은 오브젝트에 있어야 합니다.
/// JellyPhysicsCore는 부모에서 자동으로 찾습니다.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class JellyMeshDeformer : MonoBehaviour
{
    [Header("스프링 포인트 설정")]
    public bool perVertexSprings = true;
    [Range(10f, 100f)] public float vertexSpringStrength = 20f;
    [Range(1f, 20f)] public float vertexDamping = 2f;

    [Header("외형 변형")]
    [Range(0f, 2f)] public float deformMultiplier = 1.5f;
    [Range(0.01f, 1f)] public float deformSmoothing = 0.08f;

    [Header("스쿼시 & 스트레치")]
    [Range(0f, 1f)] public float squashStretchRatio = 0.8f;
    public bool preserveVolume = true;

    [Header("표면 리플 효과")]
    public bool enableRipple = true;
    [Range(0f, 0.5f)] public float rippleStrength = 0.05f;
    public float rippleSpeed = 6f;
    public float rippleDecay = 3f;

    private Mesh originalMesh;
    private Mesh deformedMesh;
    private Vector3[] originalVertices;
    private Vector3[] deformedVertices;
    private Vector3[] vertexVelocities;
    private Vector3[] vertexOffsets;

    private struct RipplePoint
    {
        public Vector3 localPos;
        public float strength;
        public float time;
    }
    private List<RipplePoint> activeRipples = new List<RipplePoint>();

    // 부모에서 찾아옴
    private JellyPhysicsCore physicsCore;
    private MeshFilter meshFilter;
    private float time;

    private void Awake()
    {
        // 부모 계층에서 JellyPhysicsCore 탐색
        physicsCore = GetComponentInParent<JellyPhysicsCore>();
        meshFilter = GetComponent<MeshFilter>();

        if (physicsCore == null)
        {
            Debug.LogError($"[JellyMeshDeformer] 부모 오브젝트에 JellyPhysicsCore가 없습니다! ({gameObject.name})");
            enabled = false;
            return;
        }

        if (meshFilter.sharedMesh == null)
        {
            Debug.LogError($"[JellyMeshDeformer] MeshFilter에 Mesh가 할당되지 않았습니다! ({gameObject.name})");
            enabled = false;
            return;
        }

        InitializeMesh();

        physicsCore.OnImpact += OnImpact;
        physicsCore.OnSqueezeProgress += OnSqueezeProgress;
    }

    private void InitializeMesh()
    {
        originalMesh = meshFilter.sharedMesh;
        deformedMesh = Instantiate(originalMesh);
        deformedMesh.name = originalMesh.name + "_JellyDeformed";
        meshFilter.mesh = deformedMesh;

        originalVertices = originalMesh.vertices;
        int vCount = originalVertices.Length;

        deformedVertices = new Vector3[vCount];
        vertexVelocities = new Vector3[vCount];
        vertexOffsets = new Vector3[vCount];

        System.Array.Copy(originalVertices, deformedVertices, vCount);
    }

    private void Update()
    {
        time += Time.deltaTime;

        UpdateVertexSprings();
        ApplyCoreDeformation();
        UpdateRipples();
        ApplyDeformedMesh();
    }

    private void UpdateVertexSprings()
    {
        if (!perVertexSprings) return;

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 springForce = -vertexOffsets[i] * vertexSpringStrength;
            Vector3 damperForce = -vertexVelocities[i] * vertexDamping;

            vertexVelocities[i] += (springForce + damperForce) * Time.deltaTime;
            vertexOffsets[i] += vertexVelocities[i] * Time.deltaTime;
        }
    }

    private void ApplyCoreDeformation()
    {
        if (physicsCore.isSqueezing) return;

        Vector3 coreDeform = physicsCore.currentDeformation * deformMultiplier;
        Vector3 scaleDeform;

        if (preserveVolume)
        {
            float yScale = Mathf.Clamp(1f + coreDeform.y, physicsCore.maxCompression, physicsCore.maxExpansion);
            float xzScale = Mathf.Lerp(1f, Mathf.Sqrt(1f / Mathf.Max(yScale, 0.01f)), squashStretchRatio);
            scaleDeform = new Vector3(xzScale, yScale, xzScale);
        }
        else
        {
            scaleDeform = Vector3.one + coreDeform;
        }

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 targetPos = Vector3.Scale(originalVertices[i], scaleDeform) + vertexOffsets[i];
            deformedVertices[i] = Vector3.Lerp(deformedVertices[i], targetPos, deformSmoothing * 60f * Time.deltaTime);
        }
    }

    private void UpdateRipples()
    {
        if (!enableRipple || activeRipples.Count == 0) return;

        for (int ri = activeRipples.Count - 1; ri >= 0; ri--)
        {
            RipplePoint rp = activeRipples[ri];
            float elapsed = time - rp.time;
            float decayedStrength = rp.strength * Mathf.Exp(-rippleDecay * elapsed);

            if (decayedStrength < 0.001f) { activeRipples.RemoveAt(ri); continue; }

            for (int i = 0; i < originalVertices.Length; i++)
            {
                float dist = Vector3.Distance(originalVertices[i], rp.localPos);
                float wave = Mathf.Sin(dist * 8f - elapsed * rippleSpeed) * decayedStrength;
                deformedVertices[i] += originalVertices[i].normalized * wave * rippleStrength;
            }
        }
    }

    private void ApplyDeformedMesh()
    {
        deformedMesh.vertices = deformedVertices;
        deformedMesh.RecalculateNormals();
        deformedMesh.RecalculateBounds();
    }

    private void OnImpact(Vector3 direction, float strength)
    {
        if (enableRipple)
        {
            activeRipples.Add(new RipplePoint
            {
                localPos = transform.InverseTransformDirection(direction) * 0.5f,
                strength = strength,
                time = time
            });
        }

        Vector3 localDir = transform.InverseTransformDirection(direction);
        for (int i = 0; i < originalVertices.Length; i++)
        {
            float dot = Vector3.Dot(originalVertices[i].normalized, localDir);
            if (dot > 0.3f)
                vertexVelocities[i] += localDir * strength * 2f;
        }
    }

    private void OnSqueezeProgress(float progress) { }

    public void ApplySqueezeDeform(Vector3 localSqueezeAxis, float compressionAmount, float expansionAmount)
    {
        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 orig = originalVertices[i];
            float proj = Vector3.Dot(orig, localSqueezeAxis.normalized);
            Vector3 axisComp = localSqueezeAxis.normalized * proj;
            Vector3 perpComp = orig - axisComp;

            deformedVertices[i] = Vector3.Lerp(
                deformedVertices[i],
                axisComp * compressionAmount + perpComp * expansionAmount + vertexOffsets[i],
                0.3f
            );
        }
        ApplyDeformedMesh();
    }

    private void OnDestroy()
    {
        if (deformedMesh != null) Destroy(deformedMesh);
        if (physicsCore != null)
        {
            physicsCore.OnImpact -= OnImpact;
            physicsCore.OnSqueezeProgress -= OnSqueezeProgress;
        }
    }
}
