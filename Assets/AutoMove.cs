using UnityEngine;

public class AutoMove : MonoBehaviour
{
    public enum MoveSpace { Local, World }

    [Header("이동 축 속도 (유닛/초)")]
    public float speedX = 0f;
    public float speedY = 0f;
    public float speedZ = 1f;

    [Header("설정")]
    [Tooltip("Local: 오브젝트 자신의 축 기준 / World: 월드 축 기준")]
    public MoveSpace moveSpace = MoveSpace.World;

    [Tooltip("켜면 이동을 멈춥니다.")]
    public bool paused = false;

    [Header("가속/감속")]
    [Tooltip("0이면 즉시 설정 속도로 이동. 0보다 크면 그 시간(초) 동안 서서히 목표 속도에 도달합니다.")]
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

        if (moveSpace == MoveSpace.Local)
            transform.position += transform.TransformDirection(delta);
        else
            transform.position += delta;
    }
}
