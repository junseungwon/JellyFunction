using UnityEngine;

/// <summary>
/// WPO Intensity를 Renderer에 실제 적용하는 순수 실행 엔진.
/// 수치 파라미터, 상태 관리, preset은 소유하지 않습니다.
/// Controller가 모든 수치/상태를 관리하고, 이 컴포넌트의 ApplyIntensity를 호출합니다.
/// </summary>
public class CharacterBounce : MonoBehaviour
{
    #region Inspector

    [Header("렌더러 설정")]
    [SerializeField] private Renderer _targetRenderer = null;

    #endregion

    #region Private Fields

    private float _wpoIntensity;
    private MaterialPropertyBlock _propertyBlock = null;

    #endregion

    #region Properties

    /// <summary>현재 적용 중인 WPO Intensity 값 (읽기 전용)</summary>
    public float CurrentIntensity => _wpoIntensity;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();

        if (_targetRenderer == null)
            _targetRenderer = GetComponent<Renderer>();
    }

    #endregion

    #region Public API - 실행

    /// <summary>주어진 intensity 값을 지정된 shader property로 Renderer에 적용합니다.</summary>
    public void ApplyIntensity(float intensity, string propertyName)
    {
        _wpoIntensity = intensity;

        if (_targetRenderer == null) return;

        _targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(propertyName, _wpoIntensity);
        _targetRenderer.SetPropertyBlock(_propertyBlock);
    }

    #endregion
}
