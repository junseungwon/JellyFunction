using UnityEngine;

/// <summary>
/// 쿼터뷰 카메라를 제어합니다. 타깃 추적, 마우스 드래그 회전, 스크롤 줌 지원.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Target")]
    /// <summary>추적할 타깃 Transform (예: 플레이어)</summary>
    [SerializeField] private Transform _targetTransform;
    /// <summary>카메라 주시점 높이 오프셋</summary>
    [SerializeField] private Vector3 _targetOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Rotation")]
    [SerializeField] private float _rotationSpeed    = 3f;
    /// <summary>쿼터뷰 고정 수직각</summary>
    [SerializeField] private float _verticalAngle    = 45f;

    [Header("Zoom")]
    [SerializeField] private float _distance         = 8f;
    [SerializeField] private float _minDistance      = 3f;
    [SerializeField] private float _maxDistance      = 15f;
    [SerializeField] private float _zoomSpeed        = 3f;
    [SerializeField] private float _zoomSmoothSpeed  = 8f;

    [Header("Follow")]
    [SerializeField] private float _followSmoothSpeed = 10f;

    private float _currentYAngle = 0f;
    private float _targetDistance = 0f;

    private void Start()
    {
        _targetDistance = _distance;

        if (_targetTransform == null)
            Debug.LogWarning("CameraController: target을 할당해주세요.");
    }

    private void LateUpdate() // NOTE: Update 이후 처리로 떨림 방지
    {
        if (_targetTransform == null) return;

        HandleRotation();
        HandleZoom();
        UpdateCameraPosition();
    }

    /// <summary>마우스 좌클릭 드래그로 수평 회전을 처리합니다.</summary>
    private void HandleRotation()
    {
        // 마우스 좌클릭 홀드 중에만 회전
        if (!Input.GetMouseButton(0)) return;

        float mouseX = Input.GetAxis("Mouse X");
        _currentYAngle += mouseX * _rotationSpeed;
    }

    /// <summary>스크롤 휠로 줌 거리를 조절합니다.</summary>
    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        _targetDistance -= scroll * _zoomSpeed;
        _targetDistance  = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);

        // 줌 부드럽게 보간
        _distance = Mathf.Lerp(_distance, _targetDistance, _zoomSmoothSpeed * Time.deltaTime);
    }

    /// <summary>타깃을 추적하여 카메라 위치를 보간합니다.</summary>
    private void UpdateCameraPosition()
    {
        // 수직각(고정) + 수평각(드래그) 합산 회전
        Quaternion rotation    = Quaternion.Euler(_verticalAngle, _currentYAngle, 0f);
        Vector3 desiredPos     = _targetTransform.position + _targetOffset
                                 + rotation * (Vector3.back * _distance);

        // 플레이어 추적 보간
        transform.position = Vector3.Lerp(
            transform.position, desiredPos, _followSmoothSpeed * Time.deltaTime);

        transform.LookAt(_targetTransform.position + _targetOffset);
    }
}
