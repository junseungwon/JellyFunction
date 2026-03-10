using System.Collections.Generic;
using UnityEngine;

public class MeshConverterBridge : MonoBehaviour
{
    [SerializeField] private List<SkinnedMeshRenderer> targets = new();

    [Header("부모 (비어 있으면 Awake에서 자동 생성)")]
    [Tooltip("SkinnedMeshRenderer들이 붙을 부모. 비우면 'SkinnedMeshParent'가 이 오브젝트 자식으로 생성됩니다.")]
    [SerializeField] private Transform _skinnedMeshParent;
    [Tooltip("변환된 MeshFilter 오브젝트들이 붙을 부모. 비우면 'MeshFilterParent'가 이 오브젝트 자식으로 생성됩니다.")]
    [SerializeField] private Transform _meshFilterParent;

    [Header("Debug")]
    [SerializeField] private bool _showDebugLog = false;

    private readonly Dictionary<string, MeshEntry> _repository = new();

    private void Awake()
    {
        if (_skinnedMeshParent == null)
        {
            var go = new GameObject("SkinnedMeshParent");
            go.transform.SetParent(transform, true);
            _skinnedMeshParent = go.transform;
            if (_showDebugLog)
                Debug.Log("[MeshConverterBridge] SkinnedMeshParent 자동 생성", this);
        }

        if (_meshFilterParent == null)
        {
            var go = new GameObject("MeshFilterParent");
            go.transform.SetParent(transform, true);
            _meshFilterParent = go.transform;
            if (_showDebugLog)
                Debug.Log("[MeshConverterBridge] MeshFilterParent 자동 생성", this);
        }

        foreach (SkinnedMeshRenderer smr in targets)
        {
            if (smr == null) continue;

            smr.transform.SetParent(_skinnedMeshParent, worldPositionStays: true);

            GameObject staticObj = BuildStaticObject(smr);
            staticObj.SetActive(true);

            MeshEntry entry = new MeshEntry
            {
                skinnedMesh  = smr,
                staticObject = staticObj,
            };

            _repository[smr.name] = entry;

            if (_showDebugLog)
                Debug.Log($"[MeshConverterBridge] 등록 완료 key={smr.name} | skinned={smr.name} | static={staticObj.name}", this);
        }

        _skinnedMeshParent.gameObject.SetActive(true);
        _meshFilterParent.gameObject.SetActive(false);

        if (_showDebugLog)
            Debug.Log($"[MeshConverterBridge] 초기화 완료 | targets={_repository.Count}", this);
    }

    private GameObject BuildStaticObject(SkinnedMeshRenderer smr)
    {
        Transform src = smr.transform;

        Mesh bakedMesh = new Mesh();
        smr.BakeMesh(bakedMesh, useScale: true);

        GameObject obj = new GameObject($"Static_{smr.name}");

        obj.transform.SetPositionAndRotation(src.position, src.rotation);
        obj.transform.localScale = src.lossyScale;
        obj.transform.SetParent(_meshFilterParent, worldPositionStays: true);

        obj.AddComponent<MeshFilter>().sharedMesh        = bakedMesh;
        obj.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;

        return obj;
    }

    public bool TryGetEntry(string key, out MeshEntry entry)
        => _repository.TryGetValue(key, out entry);

    public MeshEntry GetEntry(string key)
        => _repository.TryGetValue(key, out MeshEntry e) ? e : null;

    public GameObject GetStaticObject(string key)
        => GetEntry(key)?.staticObject;

    public SkinnedMeshRenderer GetSkinnedMesh(string key)
        => GetEntry(key)?.skinnedMesh;

    /// <summary>브릿지에 등록된 모든 엔트리를 반환합니다.</summary>
    public IEnumerable<MeshEntry> GetAllEntries() => _repository.Values;

    public void ActivateStatic(string key)
    {
        if (_showDebugLog)
            Debug.Log($"[MeshConverterBridge] ActivateStatic(key={key}) → SwapAll(true)", this);
        SwapAll(useStatic: true);
    }

    public void ActivateSkinned(string key)
    {
        if (_showDebugLog)
            Debug.Log($"[MeshConverterBridge] ActivateSkinned(key={key}) → SwapAll(false)", this);
        SwapAll(useStatic: false);
    }

    /// <summary>전체를 Static(MeshFilter) 또는 SkinnedMesh 쪽으로 한 번에 전환합니다.</summary>
    public void SwapAll(bool useStatic)
    {
        _meshFilterParent.gameObject.SetActive(useStatic);
        _skinnedMeshParent.gameObject.SetActive(!useStatic);

        if (_showDebugLog)
            Debug.Log($"[MeshConverterBridge] SwapAll(useStatic={useStatic}) | meshFilterParent={_meshFilterParent.gameObject.activeSelf} skinnedParent={_skinnedMeshParent.gameObject.activeSelf}", this);
    }
}
