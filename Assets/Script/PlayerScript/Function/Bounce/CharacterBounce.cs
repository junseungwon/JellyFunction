using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WPO Intensity를 브릿지에 등록된 모든 SkinnedMesh(또는 현재 활성 Static Mesh)의 Renderer에 적용합니다.
/// </summary>
public class CharacterBounce : MonoBehaviour
{
    #region Inspector

    [Header("Bridge - Renderer 소스")]
    [Tooltip("비어 있으면 씬에서 MeshConverterBridge를 자동 탐색합니다.")]
    [SerializeField] private MeshConverterBridge _bridge;

    #endregion

    #region Private Fields

    private float _wpoIntensity;
    private MaterialPropertyBlock _propertyBlock;
    private bool _initialized;

    #endregion

    #region Properties

    public float CurrentIntensity => _wpoIntensity;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        _propertyBlock = new MaterialPropertyBlock();

        if (_bridge == null)
            _bridge = FindFirstObjectByType<MeshConverterBridge>();

        if (_bridge == null)
        {
            Debug.LogWarning("[CharacterBounce] MeshConverterBridge를 찾을 수 없습니다.", this);
            enabled = false;
            return;
        }

        _initialized = true;
    }

    #endregion

    #region Public API - 실행

    /// <summary>주어진 intensity를 지정된 shader property로 브릿지의 모든 엔트리(현재 활성 Renderer)에 적용합니다.</summary>
    public void ApplyIntensity(float intensity, string propertyName)
    {
        _wpoIntensity = intensity;

        if (!_initialized || _bridge == null) return;

        foreach (MeshEntry entry in _bridge.GetAllEntries())
        {
            Renderer target = GetActiveRenderer(entry);
            if (target == null) continue;

            target.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetFloat(propertyName, _wpoIntensity);
            target.SetPropertyBlock(_propertyBlock);
        }
    }

    private static Renderer GetActiveRenderer(MeshEntry entry)
    {
        if (entry?.skinnedMesh != null && entry.skinnedMesh.gameObject.activeSelf)
            return entry.skinnedMesh;
        if (entry?.staticObject != null && entry.staticObject.activeSelf)
            return entry.staticObject.GetComponent<MeshRenderer>();
        return null;
    }

    #endregion
}
