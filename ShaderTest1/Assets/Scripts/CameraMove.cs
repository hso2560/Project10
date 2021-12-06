
using UnityEngine;

public class CameraMove : MonoBehaviour
{
    public Transform target = null;
    public float speed = 7f;

    public void SetInit(Transform target)
    {
        this.target = target;
    }

    private void LateUpdate()
    {
        if (target != null)
        {
            transform.position = Vector3.Lerp(transform.position, target.position, speed * Time.deltaTime);
            transform.position = new Vector3(transform.position.x, transform.position.y, -10);
        }
    }
}
