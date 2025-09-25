using UnityEngine;

public class CrystalBehaviour : MonoBehaviour
{
    private Animator animator;
    private bool isBroken = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void BreakCrystal()
    {
        if (!isBroken)
        {
            isBroken = true;
            animator.SetTrigger("break");
            
        }
    }
}
