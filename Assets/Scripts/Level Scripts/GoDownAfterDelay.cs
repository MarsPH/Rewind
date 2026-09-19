using System.Collections;
using UnityEngine;

public class GoDownAfterDelay : MonoBehaviour
{
    [Tooltip("Seconds to wait before the object starts going down")]
    public float delay = 5f;

    [Tooltip("Seconds to wait at the bottom before going back up")]
    public float waitAtBottom = 2f;

    [Tooltip("How fast the object moves (units per second)")]
    public float speed = 2f;

    [Tooltip("How far down the object travels")]
    public float distance = 3f;

    private Vector3 startPosition;
    private Vector3 downPosition;

    void Start()
    {
        startPosition = transform.position;
        downPosition = startPosition + Vector3.down * distance;
        StartCoroutine(MoveSequence());
    }

    IEnumerator MoveSequence()
    {
        yield return new WaitForSeconds(delay);          // wait 5 seconds
        yield return MoveTo(downPosition);               // go down
        yield return new WaitForSeconds(waitAtBottom);   // wait 2 seconds
        yield return MoveTo(startPosition);              // go back up
    }

    IEnumerator MoveTo(Vector3 target)
    {
        while (transform.position != target)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                speed * Time.deltaTime
            );
            yield return null;
        }
    }
}