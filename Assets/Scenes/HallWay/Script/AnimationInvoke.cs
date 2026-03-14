using UnityEngine;

public class AnimationInvoke : MonoBehaviour
{
    private Animator animator;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void InvokeAnimationIdle()
    {
        animator.SetTrigger("Idle");
    }
    public void InvokeAnimationWalk(bool Rotate)
    {
        animator.SetTrigger("Walk");
        if (Rotate)
        {
            RotateCharacter(0,0,0);
        }
    }
    public void InvokeAnimationTurn()
    {
        animator.SetTrigger("Turn");
    }

    public void RotateCharacter(float x, float y, float z)
    {
        transform.localRotation = Quaternion.Euler(x, y, z);
    }
}
