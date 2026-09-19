using UnityEngine;

public class PushCircleTrigger : MonoBehaviour
{
    [SerializeField] private Rigidbody2D circleRb;   // drag the circle here
    [SerializeField] private float pushForce = 10f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        circleRb.AddForce(Vector2.left * pushForce, ForceMode2D.Impulse);
    }
}