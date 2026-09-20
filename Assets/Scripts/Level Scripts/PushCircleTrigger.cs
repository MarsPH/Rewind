using UnityEngine;

public class PushOnTrigger : MonoBehaviour
{
    [SerializeField] private Rigidbody2D target;  
    [SerializeField] private float pushForce = 10f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("Trigger entered by: " + other.name + " (tag: " + other.tag + ")");

        if (!other.CompareTag("Player")) return;

        Debug.Log("Player detected, pushing. circleRb = " + target);
        target.AddForce(Vector2.left * pushForce, ForceMode2D.Impulse);
    }
}