using UnityEngine;

public class JellyRotateBehind : MonoBehaviour
{
    [SerializeField] Animator animator;

    void Start()
    {
    }

    void Update()
    {
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log("OnTriggerEnter");
        if (animator == null) return;
        animator.SetBool("Idle", true);
        animator.SetTrigger("Detecter");

        //그다음 해당 캐릭터의 localRotion값을 0,-35,0으로 변경
        other.transform.localRotation = Quaternion.Euler(0, -35, 0);
    }
}
