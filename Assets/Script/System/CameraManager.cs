using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [Header("카메라 설정")]
    [SerializeField] private Camera camera1;
    [SerializeField] private Camera camera2;
    [SerializeField] private Camera camera3;

    [Header("기본 활성 카메라 (0=1번, 1=2번, 2=3번)")]
    [SerializeField] private int defaultActiveIndex = 0;

    private Camera[] _cameras;

    private void Awake()
    {
        _cameras = new[] { camera1, camera2, camera3 };

        // 누락된 카메라 제거
        for (int i = _cameras.Length - 1; i >= 0; i--)
        {
            if (_cameras[i] == null)
                Debug.LogWarning($"[CameraManager] Camera {i + 1}이 할당되지 않았습니다.");
        }

        SetActiveCamera(defaultActiveIndex);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Keypad1) && camera1 != null)
            SetActiveCamera(0);
        else if (Input.GetKeyDown(KeyCode.Keypad2) && camera2 != null)
            SetActiveCamera(1);
        else if (Input.GetKeyDown(KeyCode.Keypad3) && camera3 != null)
            SetActiveCamera(2);
    }

    public void SetActiveCamera(int index)
    {
        if (index < 0 || index >= _cameras.Length) return;

        for (int i = 0; i < _cameras.Length; i++)
        {
            if (_cameras[i] != null)
                _cameras[i].enabled = (i == index);
        }
    }

    public void SetActiveCamera1() => SetActiveCamera(0);
    public void SetActiveCamera2() => SetActiveCamera(1);
    public void SetActiveCamera3() => SetActiveCamera(2);
}
