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

    [Header("가속/감속")]
    [Tooltip("0이면 즉시 설정 속도로 회전. 0보다 크면 그 시간(초) 동안 서서히 목표 속도에 도달합니다.")]
    public float smoothTime = 0f;

    Vector3 currentSpeed;

    void Start()
    {
        currentSpeed = new Vector3(speedX, speedY, speedZ);
    }

    void Update()
    {
        if (paused) return;

        Vector3 targetSpeed = new Vector3(speedX, speedY, speedZ);

        if (smoothTime > 0f)
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
