using UnityEngine;

/// <summary>
/// CharacterBounce(Feature)의 모든 수치와 상태를 소유하는 컨트롤러.
/// 상태 전환(Default/Excited), Intensity 증감 등 고수준 API를 제공하며,
/// Feature의 ApplyIntensity()를 호출하여 실제 적용을 위임합니다.
/// </summary>
public class CharacterBounceController : MonoBehaviour
{
    #region Types

    public enum BounceState
    {
        Default,
        Excited
    }

    #endregion

    #region Inspector

    [Header("참조")]
    [Tooltip("바운스 기능을 담당하는 CharacterBounce 컴포넌트")]
    [SerializeField] private CharacterBounce _bounce = null;

    [Header("WPO 프로퍼티")]
    [SerializeField] private string _wpoPropertyName = "_WPOIntensity";

    [Header("상태별 Intensity 값")]
    [SerializeField] private float _defaultIntensity = 0.1f;
    [SerializeField] private float _excitedIntensity = 0.2f;

    [Header("Intensity 증감 설정")]
    [Tooltip("IncreaseIntensity / DecreaseIntensity 한 번 호출 시 변화량")]
    [SerializeField] private float _intensityStep = 0.05f;

    [Header("Debug")]
    [Tooltip("켜면 상태 전환·수치 적용 시 콘솔에 로그 출력")]
    [SerializeField] private bool _showDebugLog = false;

    #endregion

    #region Private Fields

    private BounceState _currentState = BounceState.Default;

    #endregion

    #region Properties

    public BounceState CurrentState => _currentState;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (_bounce == null)
            _bounce = GetComponent<CharacterBounce>();

        SetDefaultState();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying || _bounce == null) return;

        float intensity = _currentState == BounceState.Excited ? _excitedIntensity : _defaultIntensity;
        _bounce.ApplyIntensity(intensity, _wpoPropertyName);
    }

    private void Reset()
    {
        _bounce = GetComponent<CharacterBounce>();
    }

    #endregion

    #region Public API - 상태 전환

    public void SetDefaultState()
    {
        _currentState = BounceState.Default;
        if (_bounce != null) _bounce.ApplyIntensity(_defaultIntensity, _wpoPropertyName);

        if (_showDebugLog)
            Debug.Log($"[CharacterBounceController] Default 상태 | Intensity: {_defaultIntensity:F3}");
    }

    public void SetExcitedState()
    {
        _currentState = BounceState.Excited;
        if (_bounce != null) _bounce.ApplyIntensity(_excitedIntensity, _wpoPropertyName);

        if (_showDebugLog)
            Debug.Log($"[CharacterBounceController] Excited 상태 | Intensity: {_excitedIntensity:F3}");
    }

    public void IncreaseIntensity()
    {
        if (_bounce == null) return;
        float next = Mathf.Clamp(_bounce.CurrentIntensity + _intensityStep, 0f, 1f);
        _bounce.ApplyIntensity(next, _wpoPropertyName);
    }

    public void DecreaseIntensity()
    {
        if (_bounce == null) return;
        float next = Mathf.Clamp(_bounce.CurrentIntensity - _intensityStep, 0f, 1f);
        _bounce.ApplyIntensity(next, _wpoPropertyName);
    }

    #endregion

    #region Public API - 수치 조정

    public void SetDefaultIntensity(float value)
    {
        _defaultIntensity = value;
    }

    public void SetExcitedIntensity(float value)
    {
        _excitedIntensity = value;
    }

    public void SetIntensityStep(float value)
    {
        _intensityStep = Mathf.Max(0f, value);
    }

    public void SetWPOPropertyName(string name)
    {
        _wpoPropertyName = name;
    }

    /// <summary>임의의 intensity 값을 직접 적용합니다 (상태 변경 없음).</summary>
    public float WPOIntensity
    {
        get => _bounce != null ? _bounce.CurrentIntensity : 0f;
        set { if (_bounce != null) _bounce.ApplyIntensity(value, _wpoPropertyName); }
    }

    #endregion
}
