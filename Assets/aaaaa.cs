using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class aaaaa : MonoBehaviour
{
    [SerializeField] private GameObject aa;
    [SerializeField] private GameObject aa1;

    [SerializeField] private float speed = 0.01f;

    [Header("스케일 감소")]
    [Tooltip("스케일을 줄일 대상. 비우면 이 스크립트가 붙은 오브젝트")]
    [SerializeField] private Transform scaleTarget = null;
    [Tooltip("줄어드는 축 (X/Y/Z). 해당 축만 매 프레임 감소")]
    [SerializeField] private bool scaleDownX = false;
    [SerializeField] private bool scaleDownY = true;
    [SerializeField] private bool scaleDownZ = false;
    [Tooltip("매 초당 줄어드는 양 (양수 입력)")]
    [SerializeField] private float scaleDownSpeed = 0.1f;
    [Tooltip("이 값 아래로는 안 줄어듦")]
    [SerializeField] private float scaleMin = 0.1f;

    void Start()
    {
        if (scaleTarget == null)
            scaleTarget = transform;
    }

    void Update()
    {
        // y값 pos를 천천히 내려감
        transform.position += new Vector3(0, -0.01f * Time.deltaTime, 0);

        // 스케일 천천히 감소
        if (scaleTarget != null)
        {
            Vector3 s = scaleTarget.localScale;
            float delta = scaleDownSpeed * Time.deltaTime;

            if (scaleDownX) s.x = Mathf.Max(scaleMin, s.x - delta);
            if (scaleDownY) s.y = Mathf.Max(scaleMin, s.y - delta);
            if (scaleDownZ) s.z = Mathf.Max(scaleMin, s.z - delta);

            scaleTarget.localScale = s;
        }
    }
}
