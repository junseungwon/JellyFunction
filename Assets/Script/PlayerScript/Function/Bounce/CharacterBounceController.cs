using UnityEngine;

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

    [Header("렌더러 설정")]
    [SerializeField] private Renderer targetRenderer;

    [Header("WPO Intensity 설정")]
    [SerializeField] private string wpoPropertyName = "_WPOIntensity";

    [Header("상태별 Intensity 값")]
    [SerializeField] private float defaultIntensity = 0.1f;
    [SerializeField] private float excitedIntensity = 0.2f;

    [Header("Intensity 증감 설정")]
    [Tooltip("IncreaseIntensity / DecreaseIntensity 한 번 호출 시 변화량")]
    [SerializeField] private float intensityStep = 0.05f;

    [Header("현재 상태 (읽기 전용)")]
    [SerializeField, HideInInspector] private BounceState currentState = BounceState.Default;

    #endregion

    #region Private Fields

    private float _wpoIntensity;
    private MaterialPropertyBlock _propertyBlock;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        SetDefaultState();
    }

    #endregion

    #region Public API

    public void SetDefaultState()
    {
        currentState = BounceState.Default;
        _wpoIntensity = defaultIntensity;
        ApplyWPOIntensity();
    }

    public void SetExcitedState()
    {
        currentState = BounceState.Excited;
        _wpoIntensity = excitedIntensity;
        ApplyWPOIntensity();
    }

    private void ApplyWPOIntensity()
    {
        if (targetRenderer == null) return;

        targetRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(wpoPropertyName, _wpoIntensity);
        targetRenderer.SetPropertyBlock(_propertyBlock);
    }

    public void IncreaseIntensity() => WPOIntensity = Mathf.Clamp(WPOIntensity + intensityStep, 0f, 1f);
    public void DecreaseIntensity() => WPOIntensity = Mathf.Clamp(WPOIntensity - intensityStep, 0f, 1f);

    public BounceState CurrentState => currentState;

    public float WPOIntensity
    {
        get => _wpoIntensity;
        set
        {
            _wpoIntensity = value;
            ApplyWPOIntensity();
        }
    }

    #endregion

#if UNITY_EDITOR
    #region Editor

    private void OnValidate()
    {
        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();

        if (targetRenderer != null)
            ApplyWPOIntensity();
    }

    #endregion
#endif
}
