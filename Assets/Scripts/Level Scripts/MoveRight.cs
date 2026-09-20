using UnityEngine;

public class MoveRight : MonoBehaviour
{
    public float speed = 5f;
    public float startDelay = 2f; // seconds to wait before moving

    private float timer = 0f;

    void Update()
    {
        if (timer < startDelay)
        {
            timer += Time.deltaTime;
            return;
        }

        transform.Translate(Vector3.right * speed * Time.deltaTime);
    }
}