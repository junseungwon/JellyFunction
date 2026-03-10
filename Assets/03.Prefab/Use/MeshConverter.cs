using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class MeshEntry
{
    public SkinnedMeshRenderer skinnedMesh;
    public GameObject staticObject;
}

public class MeshConverterBridge : MonoBehaviour
{
    [SerializeField] private List<SkinnedMeshRenderer> targets = new();

    private readonly Dictionary<string, MeshEntry> _repository = new();

    private void Awake()
    {
        foreach (SkinnedMeshRenderer smr in targets)
        {
            if (smr == null) continue;

            GameObject staticObj = BuildStaticObject(smr);
            staticObj.SetActive(false);

            MeshEntry entry = new MeshEntry
            {
                skinnedMesh  = smr,
                staticObject = staticObj,
            };

            _repository[smr.name] = entry;
        }
    }

    // ─── Build ───────────────────────────────────────────────────────────────

    private GameObject BuildStaticObject(SkinnedMeshRenderer smr)
    {
        Transform src = smr.transform;

        Mesh bakedMesh = new Mesh();
        smr.BakeMesh(bakedMesh, useScale: true);

        GameObject obj = new GameObject($"Static_{smr.name}");
        obj.transform.SetParent(src.parent, worldPositionStays: false);
        obj.transform.localPosition = src.localPosition;
        obj.transform.localRotation = src.localRotation;
        obj.transform.localScale    = src.localScale;
        obj.transform.SetSiblingIndex(src.GetSiblingIndex());

        obj.AddComponent<MeshFilter>().sharedMesh       = bakedMesh;
        obj.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;

        return obj;
    }

    // ─── Query API ────────────────────────────────────────────────────────────

    public bool TryGetEntry(string key, out MeshEntry entry)
        => _repository.TryGetValue(key, out entry);

    public MeshEntry GetEntry(string key)
        => _repository.TryGetValue(key, out MeshEntry e) ? e : null;

    public GameObject GetStaticObject(string key)
        => GetEntry(key)?.staticObject;

    public SkinnedMeshRenderer GetSkinnedMesh(string key)
        => GetEntry(key)?.skinnedMesh;

    // ─── Swap API ─────────────────────────────────────────────────────────────

    public void ActivateStatic(string key)
    {
        if (!_repository.TryGetValue(key, out MeshEntry entry)) return;
        entry.staticObject.SetActive(true);
        entry.skinnedMesh.gameObject.SetActive(false);
    }

    public void ActivateSkinned(string key)
    {
        if (!_repository.TryGetValue(key, out MeshEntry entry)) return;
        entry.skinnedMesh.gameObject.SetActive(true);
        entry.staticObject.SetActive(false);
    }

    public void SwapAll(bool useStatic)
    {
        foreach (MeshEntry entry in _repository.Values)
        {
            entry.staticObject.SetActive(useStatic);
            entry.skinnedMesh.gameObject.SetActive(!useStatic);
        }
    }
}
