using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TimeEcho
{
    public enum PersistentTriggerBehaviour
    {
        [Tooltip("Activates once and stays activated until Reset Persistent State is called or the scene reloads.")]
        LatchOnce,

        [Tooltip("Activates while the player is touching the trigger and deactivates when the last player collider leaves.")]
        WhileOccupied,

        [Tooltip("Each new player entry changes the trigger between activated and deactivated.")]
        ToggleOnEnter,

        [Tooltip("Ignores trigger contacts. Use Activate, Deactivate, or Toggle from another UnityEvent.")]
        ManualOnly
    }

    /// <summary>
    /// Player-operated pressure plate, button, or lever whose logical state is
    /// deliberately not registered with TimeEcho rewind.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PersistentTrigger2D : MonoBehaviour
    {
        [Header("Trigger Behaviour")]
        [SerializeField] private PersistentTriggerBehaviour behaviour = PersistentTriggerBehaviour.LatchOnce;

        [Tooltip("How long the player must remain on the trigger before it operates.")]
        [SerializeField, Min(0f)] private float requiredHoldSeconds;

        [Tooltip("Minimum Flowing-time delay before another activation is allowed.")]
        [SerializeField, Min(0f)] private float cooldownSeconds;

        [Tooltip("0 means unlimited. Latch Once is naturally limited to one until reset.")]
        [SerializeField, Min(0)] private int maximumActivations;

        [Tooltip("Recommended. Contacts and public activation calls are ignored during rewind and stasis.")]
        [SerializeField] private bool requireFlowingTime = true;

        [Tooltip("A dead PlayerMotor2D cannot operate this trigger.")]
        [SerializeField] private bool ignoreDeadPlayer = true;

        [Tooltip("Initial logical/visual state. It does not run the activation action list on scene load.")]
        [SerializeField] private bool startActivated;

        [Header("Actions")]
        [Tooltip("Optional no-code action list. Its activation/deactivation lists run automatically.")]
        [SerializeField] private PersistentActionSequence2D actionSequence;

        [Header("Visual Feedback (Optional)")]
        [SerializeField] private Transform movingVisual;
        [SerializeField] private Vector2 activatedLocalOffset = new Vector2(0f, -0.08f);
        [SerializeField] private float activatedRotationDegrees;
        [SerializeField] private SpriteRenderer stateRenderer;
        [SerializeField] private Color inactiveColor = new Color(0.55f, 0.58f, 0.66f, 1f);
        [SerializeField] private Color activeColor = new Color(0.25f, 0.9f, 0.55f, 1f);
        [SerializeField] private Animator animator;
        [SerializeField] private string animatorActiveBool = "Activated";

        [Header("Audio Feedback (Optional)")]
        [SerializeField] private AudioCue activateCue;
        [SerializeField] private AudioCue deactivateCue;

        [Header("Events")]
        [SerializeField] private UnityEvent onPlayerEntered = new UnityEvent();
        [SerializeField] private UnityEvent onPlayerExited = new UnityEvent();
        [SerializeField] private UnityEvent onActivated = new UnityEvent();
        [SerializeField] private UnityEvent onDeactivated = new UnityEvent();

        private readonly Dictionary<Collider2D, PlayerMotor2D> playerContacts =
            new Dictionary<Collider2D, PlayerMotor2D>();
        private readonly List<Collider2D> staleContacts = new List<Collider2D>();

        private Collider2D triggerCollider;
        private Vector3 visualBaseLocalPosition;
        private Quaternion visualBaseLocalRotation;
        private bool visualBaseCaptured;
        private bool isActivated;
        private int activationCount;
        private float holdElapsed;
        private float cooldownRemaining;
        private bool activationPending;
        private bool entryPending;
        private bool exitPending;
        private bool occupancyAnnounced;
        private bool deactivationPending;

        public bool IsActivated => isActivated;
        public int ActivationCount => activationCount;
        public bool IsPlayerInside => playerContacts.Count > 0;
        public bool HasRequiredReferences
        {
            get
            {
                Collider2D ownCollider = triggerCollider != null
                    ? triggerCollider
                    : GetComponent<Collider2D>();
                return ownCollider != null && ownCollider.isTrigger;
            }
        }

        private void Reset()
        {
            triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }

            actionSequence = GetComponent<PersistentActionSequence2D>();
        }

        private void Awake()
        {
            triggerCollider = GetComponent<Collider2D>();
            if (triggerCollider != null && !triggerCollider.isTrigger)
            {
                triggerCollider.isTrigger = true;
            }

            if (actionSequence == null)
            {
                actionSequence = GetComponent<PersistentActionSequence2D>();
            }

            CaptureVisualBase();
            isActivated = startActivated;
            activationCount = startActivated ? 1 : 0;
            ApplyVisualState();
        }

        private void OnEnable()
        {
            if (!visualBaseCaptured)
            {
                CaptureVisualBase();
            }

            ApplyVisualState();
        }

        private void OnDisable()
        {
            playerContacts.Clear();
            activationPending = false;
            entryPending = false;
            exitPending = false;
            occupancyAnnounced = false;
            deactivationPending = false;
            holdElapsed = 0f;
        }

        private void Update()
        {
            PruneDestroyedContacts();

            if (!IsInteractionTime())
            {
                return;
            }

            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            if (exitPending)
            {
                exitPending = false;
                onPlayerExited?.Invoke();
            }

            if (deactivationPending && playerContacts.Count == 0)
            {
                deactivationPending = false;
                TryDeactivateInternal();
            }

            if (entryPending && playerContacts.Count > 0)
            {
                AnnounceEntryAndArm();
            }

            if (!activationPending || !HasEligiblePlayerContact())
            {
                return;
            }

            holdElapsed += Time.deltaTime;
            if (holdElapsed >= requiredHoldSeconds)
            {
                AttemptOccupiedOperation();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            RegisterContact(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            // Handles a player that began overlapped, a collider re-enabled in place,
            // or a transform restored by rewind without a conventional enter callback.
            if (!playerContacts.ContainsKey(other))
            {
                RegisterContact(other);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!playerContacts.Remove(other) || playerContacts.Count > 0)
            {
                return;
            }

            HandleLastContactExited();
        }

        public void Activate()
        {
            TryActivateInternal();
        }

        public void Deactivate()
        {
            TryDeactivateInternal();
        }

        public void Toggle()
        {
            if (!IsInteractionTime())
            {
                return;
            }

            if (isActivated)
            {
                TryDeactivateInternal();
            }
            else
            {
                TryActivateInternal();
            }
        }

        /// <summary>
        /// Resets only this trigger's latch/counter. It intentionally does not undo
        /// world actions; use the deactivation action list when targets also need reset.
        /// </summary>
        public void ResetPersistentState()
        {
            activationCount = 0;
            cooldownRemaining = 0f;
            holdElapsed = 0f;
            activationPending = false;
            deactivationPending = false;
            isActivated = startActivated;
            activationCount = startActivated ? 1 : 0;
            ApplyVisualState();
        }

        public void Configure(
            PersistentTriggerBehaviour triggerBehaviour,
            PersistentActionSequence2D actions,
            Transform visual,
            SpriteRenderer renderer,
            Vector2 activeOffset,
            float activeRotation,
            Color offColor,
            Color onColor,
            AudioCue onCue,
            AudioCue offCue)
        {
            behaviour = triggerBehaviour;
            actionSequence = actions;
            movingVisual = visual;
            stateRenderer = renderer;
            activatedLocalOffset = activeOffset;
            activatedRotationDegrees = activeRotation;
            inactiveColor = offColor;
            activeColor = onColor;
            activateCue = onCue;
            deactivateCue = offCue;
            triggerCollider = GetComponent<Collider2D>();
        }

        private void RegisterContact(Collider2D other)
        {
            if (other == null || playerContacts.ContainsKey(other))
            {
                return;
            }

            PlayerMotor2D player = other.GetComponentInParent<PlayerMotor2D>();
            if (player == null)
            {
                return;
            }

            bool firstContact = playerContacts.Count == 0;
            playerContacts.Add(other, player);
            if (!firstContact)
            {
                return;
            }

            entryPending = true;
            if (IsInteractionTime())
            {
                AnnounceEntryAndArm();
            }
        }

        private void AnnounceEntryAndArm()
        {
            entryPending = false;
            occupancyAnnounced = true;
            onPlayerEntered?.Invoke();

            if (behaviour == PersistentTriggerBehaviour.ManualOnly)
            {
                return;
            }

            activationPending = true;
            holdElapsed = 0f;
            if (requiredHoldSeconds <= 0f && HasEligiblePlayerContact())
            {
                AttemptOccupiedOperation();
            }
        }

        private void AttemptOccupiedOperation()
        {
            if (!activationPending || !IsInteractionTime())
            {
                return;
            }

            if (cooldownRemaining > 0f)
            {
                return;
            }

            bool completed;
            switch (behaviour)
            {
                case PersistentTriggerBehaviour.LatchOnce:
                    if (activationCount > 0)
                    {
                        activationPending = false;
                        return;
                    }
                    completed = TryActivateInternal();
                    break;

                case PersistentTriggerBehaviour.WhileOccupied:
                    completed = isActivated || TryActivateInternal();
                    break;

                case PersistentTriggerBehaviour.ToggleOnEnter:
                    if (isActivated)
                    {
                        completed = TryDeactivateInternal();
                    }
                    else
                    {
                        completed = TryActivateInternal();
                    }
                    break;

                default:
                    completed = true;
                    break;
            }

            if (completed || HasReachedActivationLimit())
            {
                activationPending = false;
            }
        }

        private bool TryActivateInternal()
        {
            if (isActivated || !IsInteractionTime() || cooldownRemaining > 0f || HasReachedActivationLimit())
            {
                return false;
            }

            if (behaviour == PersistentTriggerBehaviour.LatchOnce && activationCount > 0)
            {
                return false;
            }

            isActivated = true;
            activationCount++;
            cooldownRemaining = cooldownSeconds;
            ApplyVisualState();
            actionSequence?.PlayActivation();
            PlayCue(activateCue);
            onActivated?.Invoke();
            return true;
        }

        private bool TryDeactivateInternal()
        {
            if (!isActivated || !IsInteractionTime())
            {
                return false;
            }

            isActivated = false;
            ApplyVisualState();
            actionSequence?.PlayDeactivation();
            PlayCue(deactivateCue);
            onDeactivated?.Invoke();
            return true;
        }

        private void HandleLastContactExited()
        {
            activationPending = false;
            entryPending = false;
            holdElapsed = 0f;

            if (occupancyAnnounced)
            {
                occupancyAnnounced = false;
                if (IsInteractionTime())
                {
                    onPlayerExited?.Invoke();
                }
                else
                {
                    exitPending = true;
                }
            }

            if (behaviour != PersistentTriggerBehaviour.WhileOccupied || !isActivated)
            {
                return;
            }

            if (IsInteractionTime())
            {
                TryDeactivateInternal();
            }
            else
            {
                deactivationPending = true;
            }
        }

        private bool HasEligiblePlayerContact()
        {
            foreach (KeyValuePair<Collider2D, PlayerMotor2D> contact in playerContacts)
            {
                if (contact.Key == null || contact.Value == null)
                {
                    continue;
                }

                if (!ignoreDeadPlayer || !contact.Value.IsDead)
                {
                    return true;
                }
            }

            return false;
        }

        private void PruneDestroyedContacts()
        {
            if (playerContacts.Count == 0)
            {
                return;
            }

            staleContacts.Clear();
            foreach (KeyValuePair<Collider2D, PlayerMotor2D> contact in playerContacts)
            {
                if (contact.Key == null || contact.Value == null)
                {
                    staleContacts.Add(contact.Key);
                }
            }

            if (staleContacts.Count == 0)
            {
                return;
            }

            for (int i = 0; i < staleContacts.Count; i++)
            {
                playerContacts.Remove(staleContacts[i]);
            }

            if (playerContacts.Count == 0)
            {
                HandleLastContactExited();
            }
        }

        private bool HasReachedActivationLimit()
        {
            return maximumActivations > 0 && activationCount >= maximumActivations;
        }

        private bool IsInteractionTime()
        {
            return !requireFlowingTime ||
                   TimeDirector.Instance == null ||
                   TimeDirector.Instance.Mode == TimeMode.Flowing;
        }

        private void CaptureVisualBase()
        {
            if (movingVisual == null || movingVisual == transform)
            {
                visualBaseCaptured = false;
                return;
            }

            visualBaseLocalPosition = movingVisual.localPosition;
            visualBaseLocalRotation = movingVisual.localRotation;
            visualBaseCaptured = true;
        }

        private void ApplyVisualState()
        {
            if (visualBaseCaptured && movingVisual != null)
            {
                movingVisual.localPosition = visualBaseLocalPosition +
                    (isActivated ? (Vector3)activatedLocalOffset : Vector3.zero);
                movingVisual.localRotation = visualBaseLocalRotation *
                    Quaternion.Euler(0f, 0f, isActivated ? activatedRotationDegrees : 0f);
            }

            if (stateRenderer != null)
            {
                stateRenderer.color = isActivated ? activeColor : inactiveColor;
            }

            if (animator != null && HasAnimatorBool(animator, animatorActiveBool))
            {
                animator.SetBool(animatorActiveBool, isActivated);
            }
        }

        private void PlayCue(AudioCue cue)
        {
            if (cue != null && AudioService.Instance != null)
            {
                AudioService.Instance.Play(cue, transform.position);
            }
        }

        private static bool HasAnimatorBool(Animator targetAnimator, string parameterName)
        {
            if (targetAnimator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = targetAnimator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Bool &&
                    parameters[i].name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            requiredHoldSeconds = Mathf.Max(0f, requiredHoldSeconds);
            cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            maximumActivations = Mathf.Max(0, maximumActivations);

            Collider2D ownCollider = GetComponent<Collider2D>();
            if (ownCollider != null)
            {
                ownCollider.isTrigger = true;
            }
        }
    }
}
