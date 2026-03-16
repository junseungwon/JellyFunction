using UnityEngine;
using UnityEngine.Splines;
using Unity.Cinemachine;

/// <summary>
/// Cinemachine Spline Dolly의 Speed를 기본 속도 × 커브(배율)로 조절합니다.
/// 커브 X=정규화 위치(0~1), Y=속도 배율(1=기본 속도, 0.5=절반, 2=2배).
/// 처음 지정한 시간(기본 2초) 동안은 Speed=0으로 정지합니다.
/// CinemachineSplineDolly가 붙은 가상 카메라와 같은 오브젝트에 붙이고, Automatic Dolly Method는 Fixed Speed로 두세요.
/// </summary>
[RequireComponent(typeof(CinemachineSplineDolly))]
public class CinemachineCameraSpeed : MonoBehaviour
{
    [Header("정지 시간")]
    [Tooltip("시작 후 이 시간(초) 동안은 Speed=0으로 정지합니다.")]
    [SerializeField] private float _zeroSpeedDuration = 2f;

    [Header("속도 (기본 속도 × 커브 배율)")]
    [Tooltip("기본 속도(정규화 단위/초). 최종 속도 = 기본 속도 × 커브(위치)")]
    [SerializeField] private float _baseSpeed = 0.2f;

    [Tooltip("X=정규화 위치(0~1), Y=속도 배율. 1=기본 속도 그대로, 0.5=절반, 2=2배")]
    [SerializeField] private AnimationCurve _speedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 1f);

    [Header("속도 전환")]
    [Tooltip("현재 속도가 목표 속도로 변하는 속도(초당 변화량). 클수록 커브 변화에 빠르게 반응.")]
    [SerializeField] private float _speedChangeRate = 2f;

    private CinemachineSplineDolly _dolly;
    private SplineAutoDolly.FixedSpeed _fixedSpeed;
    private float _elapsedTime;
    private float _currentSpeed;

    private void Awake()
    {
        _dolly = GetComponent<CinemachineSplineDolly>();
        if (_dolly != null && _dolly.AutomaticDolly.Enabled && _dolly.AutomaticDolly.Method is SplineAutoDolly.FixedSpeed fs)
            _fixedSpeed = fs;
    }

    private void OnEnable()
    {
        _elapsedTime = 0f;
        _currentSpeed = 0f;
    }

    /// <summary>정규화 위치에서 목표 속도 = 기본 속도 × 커브 배율.</summary>
    private float GetTargetSpeedForPosition(float normalizedPosition)
    {
        float multiplier = _speedCurve != null ? Mathf.Max(0f, _speedCurve.Evaluate(normalizedPosition)) : 1f;
        return _baseSpeed * multiplier;
    }

    private void Update()
    {
        if (_dolly == null || _fixedSpeed == null)
            return;
        if (_dolly.SplineSettings.Spline == null || _dolly.SplineSettings.Spline.Spline == null)
            return;

        _elapsedTime += Time.deltaTime;

        // 처음 _zeroSpeedDuration 초 동안은 Speed 0
        if (_elapsedTime < _zeroSpeedDuration)
        {
            _fixedSpeed.Speed = 0f;
            _currentSpeed = 0f;
            return;
        }

        var spline = _dolly.SplineSettings.Spline.Spline;
        float currentNorm = spline.ConvertIndexUnit(
            _dolly.CameraPosition,
            _dolly.PositionUnits,
            PathIndexUnit.Normalized);

        float targetSpeed = GetTargetSpeedForPosition(currentNorm);
        _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, _speedChangeRate * Time.deltaTime);
        _fixedSpeed.Speed = _currentSpeed;
    }
}
