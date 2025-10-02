using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector2 offset;

    private void LateUpdate()
    {
        if (target != null)
        {
            transform.rotation = Quaternion.identity;
        }
    }
}
