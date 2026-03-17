using UnityEngine;

public class ZOnlyProxy : MonoBehaviour
{
    [SerializeField] Transform player;

    // X, Y는 고정값으로 설정 (Inspector에서 지정)
    float fixedX;
    float fixedY;

    void Start()
    {
        fixedX = transform.position.x; // 초기 X 고정
        fixedY = transform.position.y; // 초기 Y 고정
    }

    void LateUpdate()
    {
        transform.position = new Vector3(
            fixedX,               // X 고정
            fixedY,               // Y 고정
            player.position.z     // Z만 따라감
        );
    }
}
