using UnityEngine;
using Unity.Cinemachine;

public class dsadsa : MonoBehaviour
{
    [Header("Impulse 설정")]
    [Tooltip("Impulse를 발생시킬 CinemachineImpulseSource")]
    [SerializeField] private CinemachineImpulseSource _impulseSource;

    [Tooltip("Impulse 신호의 속도(방향 및 세기)")]
    [SerializeField] private Vector3 _impulseVelocity = new Vector3(0f, -1f, 0f);

    [Header("애니메이션 Wave 파라미터")]
    [Tooltip("Wave 커브가 들어있는 Animator")]
    [SerializeField] private Animator _animator;

    [Tooltip("애니메이션 커브가 바인딩된 float 파라미터 이름 (예: Wave)")]
    [SerializeField] private string _waveParameterName = "Wave";

    [Tooltip("Impulse가 발동되는 Wave 최솟값 임계치 (이 값을 상향 돌파할 때 1회 발동)")]
    [SerializeField] private float _threshold = 0.95f;

    private float _prevWaveValue;

    private void Start()
    {
        _prevWaveValue = GetWaveValue();
    }

    private void Update()
    {
        float currentValue = GetWaveValue();

        // 이전 프레임에서 threshold 이하였다가 이번 프레임에 초과할 때만 1회 발동 (상향 돌파)
        if (_prevWaveValue <= _threshold && currentValue > _threshold)
        {
            Debug.Log($"[dsadsa] Wave 임계치 돌파 — 현재값: {currentValue:F3} > threshold: {_threshold:F3} | Impulse 발동", this);
            FireImpulse();
        }

        _prevWaveValue = currentValue;
    }

    private float GetWaveValue()
    {
        if (_animator == null || string.IsNullOrEmpty(_waveParameterName))
            return 0f;

        return _animator.GetFloat(_waveParameterName);
    }

    public void FireImpulse()
    {
        if (_impulseSource == null)
        {
            Debug.LogWarning("[dsadsa] ImpulseSource가 할당되지 않았습니다.", this);
            return;
        }

        _impulseSource.GenerateImpulse(_impulseVelocity);
    }
}
