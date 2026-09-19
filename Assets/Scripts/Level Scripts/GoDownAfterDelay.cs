using UnityEngine;

public class GoDownAfterDelay : MonoBehaviour
{
    [Tooltip("Seconds to wait at the top before going down")]
    public float delay = 5f;

    [Tooltip("Seconds to wait at the bottom before going back up")]
    public float waitAtBottom = 2f;

    [Tooltip("How fast the object moves (units per second)")]
    public float speed = 2f;

    [Tooltip("How far down the object travels")]
    public float distance = 3f;

    private enum State { WaitingAtTop, MovingDown, WaitingAtBottom, MovingUp }

    private State state = State.WaitingAtTop;
    private float timer;
    private Vector3 startPosition;
    private Vector3 downPosition;

    void Start()
    {
        startPosition = transform.position;
        downPosition = startPosition + Vector3.down * distance;
    }

    void Update()
    {
        switch (state)
        {
            case State.WaitingAtTop:
                timer += Time.deltaTime;
                if (timer >= delay)
                {
                    timer = 0f;
                    state = State.MovingDown;
                }
                break;

            case State.MovingDown:
                transform.position = Vector3.MoveTowards(transform.position, downPosition, speed * Time.deltaTime);
                if (transform.position == downPosition)
                {
                    state = State.WaitingAtBottom;
                }
                break;

            case State.WaitingAtBottom:
                timer += Time.deltaTime;
                if (timer >= waitAtBottom)
                {
                    timer = 0f;
                    state = State.MovingUp;
                }
                break;

            case State.MovingUp:
                transform.position = Vector3.MoveTowards(transform.position, startPosition, speed * Time.deltaTime);
                if (transform.position == startPosition)
                {
                    state = State.WaitingAtTop;
                }
                break;
        }
    }
}