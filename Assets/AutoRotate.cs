using UnityEngine;

public class AutoRotate : MonoBehaviour
{
    public enum RotationSpace { Local, World }

    [Header("회전 축 속도 (도/초)")]
    public float speedX = 0f;
    public float speedY = 90f;
    public float speedZ = 0f;

    [Header("설정")]
    [Tooltip("Local: 오브젝트 자신의 축 기준 / World: 월드 축 기준")]
    public RotationSpace rotationSpace = RotationSpace.Local;

    [Tooltip("켜면 회전을 멈춥니다.")]
    public bool paused = false;

    [Header("딜레이")]
    [Tooltip("켜면 아래 delayTime(초) 이후에 회전을 시작합니다.")]
    public bool useDelay = false;
    [Tooltip("회전 시작까지 대기할 시간 (초). useDelay가 켜져 있을 때만 적용됩니다.")]
    public float delayTime = 1f;

    [Header("가속/감속")]
    [Tooltip("켜면 아래 smoothTime 동안 서서히 목표 속도에 도달합니다.")]
    public bool useAcceleration = false;
    [Tooltip("목표 속도에 도달하는 데 걸리는 시간 (초). useAcceleration이 켜져 있을 때만 적용됩니다.")]
    public float smoothTime = 1f;

    Vector3 currentSpeed;
    float _elapsedTime;

    void OnEnable()
    {
        _elapsedTime = 0f;
        currentSpeed = Vector3.zero;
    }

    void OnDisable()
    {
        transform.localRotation = Quaternion.identity;
        currentSpeed = Vector3.zero;
    }

    void Update()
    {
        if (paused) return;

        _elapsedTime += Time.deltaTime;

        if (useDelay && _elapsedTime < delayTime)
            return;

        Vector3 targetSpeed = new Vector3(speedX, speedY, speedZ);

        if (useAcceleration && smoothTime > 0f)
        {
            currentSpeed = Vector3.Lerp(currentSpeed, targetSpeed, Time.deltaTime / smoothTime);
        }
        else
        {
            currentSpeed = targetSpeed;
        }

        Vector3 delta = currentSpeed * Time.deltaTime;
        Space space = rotationSpace == RotationSpace.Local ? Space.Self : Space.World;
        transform.Rotate(delta, space);
    }
}
