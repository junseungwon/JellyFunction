using UnityEngine;

public class CharacterBounceController : MonoBehaviour
{
    public enum BounceState
    {
        Default,
        Excited
    }

    [Header("렌더러 설정")]
    [SerializeField] private Renderer targetRenderer;

    [Header("WPO Intensity 설정")]
    [SerializeField] private string wpoPropertyName = "_WPOIntensity";

    [Header("상태별 Intensity 값")]
    [SerializeField] private float defaultIntensity = 0.1f;
    [SerializeField] private float excitedIntensity = 0.2f;

    [Header("현재 상태 (읽기 전용)")]
    [SerializeField, HideInInspector] private BounceState currentState = BounceState.Default;

    private float _wpoIntensity;
    private MaterialPropertyBlock _propertyBlock;

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();

        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        SetDefaultState();
    }

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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_propertyBlock == null)
            _propertyBlock = new MaterialPropertyBlock();

        if (targetRenderer != null)
            ApplyWPOIntensity();
    }
#endif
}
