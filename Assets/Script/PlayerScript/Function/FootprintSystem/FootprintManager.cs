using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class FootprintManager : MonoBehaviour
{
    #region Singleton

    public static FootprintManager Instance { get; private set; }

    #endregion

    #region Inspector

    [Header("Surface Settings")]
    [SerializeField] private SurfaceFootprintData[] _surfaceDataList;
    [SerializeField] private SurfaceFootprintData _defaultSurfaceData; // 태그 미매칭 시 폴백

    [Header("Pool Settings")]
    [SerializeField] private int _defaultPoolSize = 20;
    [SerializeField] private int _maxPoolSize = 40;

    #endregion

    #region Private Fields

    // 지면 태그 → 풀 딕셔너리
    private Dictionary<string, ObjectPool<FootprintDecal>> _pools;
    private Dictionary<string, SurfaceFootprintData> _surfaceDataMap;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        InitializePools();
    }

    #endregion

    #region Pool Setup

    private void InitializePools()
    {
        _pools = new Dictionary<string, ObjectPool<FootprintDecal>>();
        _surfaceDataMap = new Dictionary<string, SurfaceFootprintData>();

        foreach (var data in _surfaceDataList)
        {
            _surfaceDataMap[data.surfaceTag] = data;
            _pools[data.surfaceTag] = CreatePool(data);
        }

        // 폴백용 기본 지면 풀 (리스트에 없을 수 있음)
        if (_defaultSurfaceData != null && !_pools.ContainsKey(_defaultSurfaceData.surfaceTag))
        {
            _surfaceDataMap[_defaultSurfaceData.surfaceTag] = _defaultSurfaceData;
            _pools[_defaultSurfaceData.surfaceTag] = CreatePool(_defaultSurfaceData);
        }
    }

    private ObjectPool<FootprintDecal> CreatePool(SurfaceFootprintData data)
    {
        return new ObjectPool<FootprintDecal>(
            createFunc: () =>
            {
                var obj = Instantiate(data.footprintPrefab, transform); // Manager 하위에 생성
                return obj.GetComponent<FootprintDecal>();
            },
            actionOnGet: decal => decal.gameObject.SetActive(true),
            actionOnRelease: decal => decal.gameObject.SetActive(false),
            actionOnDestroy: decal => Destroy(decal.gameObject),
            defaultCapacity: _defaultPoolSize,
            maxSize: _maxPoolSize
        );
    }

    #endregion

    #region Public API

    // FootprintDetector에서 호출
    public void SpawnFootprint(Vector3 position, Quaternion rotation, string surfaceTag)
    {
        if (!_surfaceDataMap.TryGetValue(surfaceTag, out var data))
            data = _defaultSurfaceData;

        if (data == null) return;
        if (!_pools.TryGetValue(data.surfaceTag, out var pool)) return;

        var decal = pool.Get();
        decal.transform.SetPositionAndRotation(position, rotation);
        decal.Activate(data.lifetime, data.fadeOutDuration, d => pool.Release(d));
    }

    #endregion
}
