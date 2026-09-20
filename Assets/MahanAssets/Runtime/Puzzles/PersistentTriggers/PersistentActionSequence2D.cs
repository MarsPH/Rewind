using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace TimeEcho
{
    public enum PersistentActionKind
    {
        SetObjectActive,
        SetBehaviourEnabled,
        SetColliderEnabled,
        SetRendererVisible,
        SetAnimatorTrigger,
        SetAnimatorBool,
        ReleaseRigidbody,
        FreezeRigidbody,
        SetRigidbodySimulated,
        SetGravityScale,
        AddImpulse,
        SetVelocity,
        MoveToDestination,
        PlayAudioCue,
        InvokeCustomEvent
    }

    [Serializable]
    public sealed class PersistentActionStep2D
    {
        [Tooltip("Optional note shown in the Inspector so this step is easy to recognize.")]
        public string label;

        [Tooltip("Seconds to wait after the previous step before this step runs.")]
        [Min(0f)] public float delay;

        public PersistentActionKind action = PersistentActionKind.SetObjectActive;

        public GameObject targetObject;
        public Behaviour targetBehaviour;
        public Collider2D targetCollider;
        public Renderer targetRenderer;
        public Animator targetAnimator;
        public Rigidbody2D targetBody;
        public Transform targetTransform;
        public Transform destination;

        [Tooltip("Used by on/off actions and by Move To Destination to decide whether rotation is copied.")]
        public bool boolValue = true;

        [Tooltip("Animator parameter used by Animator Trigger and Animator Bool actions.")]
        public string parameterName = "Activated";

        [Tooltip("Used as impulse or velocity, depending on the selected action.")]
        public Vector2 vectorValue = Vector2.right;

        [Tooltip("Used as gravity scale by Release Rigidbody and Set Gravity Scale.")]
        public float floatValue = 1f;

        [Tooltip("Constraints applied after Release Rigidbody. Freeze Rotation is useful for falling bridges.")]
        public RigidbodyConstraints2D releaseConstraints = RigidbodyConstraints2D.None;

        public AudioCue audioCue;
        public UnityEvent customEvent = new UnityEvent();
    }

    /// <summary>
    /// Runs an editable list of ordinary world actions. This component deliberately
    /// does not implement IRewindable, so its execution state is not restored by rewind.
    /// Individual targets still rewind only when they explicitly have a rewind component.
    /// </summary>
    public sealed class PersistentActionSequence2D : MonoBehaviour
    {
        [Header("Sequence Rules")]
        [Tooltip("Recommended. Delays pause during rewind and stasis, and each step waits for Flowing time before running.")]
        [SerializeField] private bool pauseOutsideFlowingTime = true;

        [Tooltip("When called again while running, restart with the new requested sequence. Disable to ignore repeated calls until the current sequence finishes.")]
        [SerializeField] private bool restartWhenPlayedAgain = true;

        [Tooltip("Log a warning when a step is missing the target it needs.")]
        [SerializeField] private bool warnAboutMissingTargets = true;

        [Header("Actions When Activated")]
        [SerializeField] private List<PersistentActionStep2D> activationActions = new List<PersistentActionStep2D>();

        [Header("Actions When Deactivated")]
        [SerializeField] private List<PersistentActionStep2D> deactivationActions = new List<PersistentActionStep2D>();

        [Header("Sequence Events")]
        [SerializeField] private UnityEvent onActivationSequenceCompleted = new UnityEvent();
        [SerializeField] private UnityEvent onDeactivationSequenceCompleted = new UnityEvent();

        private int runVersion;
        private bool isRunning;

        public bool IsRunning => isRunning;
        public IReadOnlyList<PersistentActionStep2D> ActivationActions => activationActions;
        public IReadOnlyList<PersistentActionStep2D> DeactivationActions => deactivationActions;

        private void OnDisable()
        {
            Cancel();
        }

        public void PlayActivation()
        {
            StartSequence(activationActions, onActivationSequenceCompleted);
        }

        public void PlayDeactivation()
        {
            StartSequence(deactivationActions, onDeactivationSequenceCompleted);
        }

        public void Cancel()
        {
            runVersion++;
            isRunning = false;
        }

        public void ConfigureSteps(
            List<PersistentActionStep2D> activate,
            List<PersistentActionStep2D> deactivate = null)
        {
            activationActions = activate ?? new List<PersistentActionStep2D>();
            deactivationActions = deactivate ?? new List<PersistentActionStep2D>();
        }

        private void StartSequence(List<PersistentActionStep2D> steps, UnityEvent completed)
        {
            if (!isActiveAndEnabled)
            {
                Warn("The action sequence cannot start while its component or GameObject is disabled.");
                return;
            }

            if (isRunning && !restartWhenPlayedAgain)
            {
                return;
            }

            runVersion++;
            int version = runVersion;
            isRunning = true;
            StartCoroutine(RunSequence(steps, completed, version));
        }

        private IEnumerator RunSequence(
            List<PersistentActionStep2D> steps,
            UnityEvent completed,
            int version)
        {
            if (steps != null)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    PersistentActionStep2D step = steps[i];
                    if (step == null)
                    {
                        continue;
                    }

                    float remaining = Mathf.Max(0f, step.delay);
                    while (remaining > 0f)
                    {
                        if (version != runVersion)
                        {
                            yield break;
                        }

                        if (!pauseOutsideFlowingTime || IsFlowingTime())
                        {
                            remaining -= pauseOutsideFlowingTime
                                ? Time.deltaTime
                                : Time.unscaledDeltaTime;
                        }

                        yield return null;
                    }

                    while (pauseOutsideFlowingTime && !IsFlowingTime())
                    {
                        if (version != runVersion)
                        {
                            yield break;
                        }

                        yield return null;
                    }

                    if (version != runVersion)
                    {
                        yield break;
                    }

                    Execute(step, i);
                }
            }

            if (version != runVersion)
            {
                yield break;
            }

            isRunning = false;
            completed?.Invoke();
        }

        private void Execute(PersistentActionStep2D step, int index)
        {
            switch (step.action)
            {
                case PersistentActionKind.SetObjectActive:
                    if (step.targetObject == null)
                    {
                        WarnMissing(step, index, "Target Object");
                        return;
                    }
                    step.targetObject.SetActive(step.boolValue);
                    break;

                case PersistentActionKind.SetBehaviourEnabled:
                    if (step.targetBehaviour == null)
                    {
                        WarnMissing(step, index, "Target Behaviour");
                        return;
                    }
                    step.targetBehaviour.enabled = step.boolValue;
                    break;

                case PersistentActionKind.SetColliderEnabled:
                    if (step.targetCollider == null)
                    {
                        WarnMissing(step, index, "Target Collider");
                        return;
                    }
                    step.targetCollider.enabled = step.boolValue;
                    break;

                case PersistentActionKind.SetRendererVisible:
                    if (step.targetRenderer == null)
                    {
                        WarnMissing(step, index, "Target Renderer");
                        return;
                    }
                    step.targetRenderer.enabled = step.boolValue;
                    break;

                case PersistentActionKind.SetAnimatorTrigger:
                    if (!HasAnimatorParameter(
                            step,
                            index,
                            AnimatorControllerParameterType.Trigger))
                    {
                        return;
                    }
                    step.targetAnimator.SetTrigger(step.parameterName);
                    break;

                case PersistentActionKind.SetAnimatorBool:
                    if (!HasAnimatorParameter(
                            step,
                            index,
                            AnimatorControllerParameterType.Bool))
                    {
                        return;
                    }
                    step.targetAnimator.SetBool(step.parameterName, step.boolValue);
                    break;

                case PersistentActionKind.ReleaseRigidbody:
                    if (!HasBody(step, index))
                    {
                        return;
                    }
                    step.targetBody.simulated = true;
                    step.targetBody.bodyType = RigidbodyType2D.Dynamic;
                    step.targetBody.gravityScale = step.floatValue;
                    step.targetBody.constraints = step.releaseConstraints;
                    step.targetBody.WakeUp();
                    break;

                case PersistentActionKind.FreezeRigidbody:
                    if (!HasBody(step, index))
                    {
                        return;
                    }
                    step.targetBody.linearVelocity = Vector2.zero;
                    step.targetBody.angularVelocity = 0f;
                    step.targetBody.simulated = true;
                    step.targetBody.bodyType = RigidbodyType2D.Kinematic;
                    step.targetBody.constraints = RigidbodyConstraints2D.FreezeAll;
                    break;

                case PersistentActionKind.SetRigidbodySimulated:
                    if (!HasBody(step, index))
                    {
                        return;
                    }
                    step.targetBody.simulated = step.boolValue;
                    break;

                case PersistentActionKind.SetGravityScale:
                    if (!HasBody(step, index))
                    {
                        return;
                    }
                    step.targetBody.gravityScale = step.floatValue;
                    break;

                case PersistentActionKind.AddImpulse:
                    if (!HasBody(step, index))
                    {
                        return;
                    }
                    step.targetBody.simulated = true;
                    step.targetBody.AddForce(step.vectorValue, ForceMode2D.Impulse);
                    break;

                case PersistentActionKind.SetVelocity:
                    if (!HasBody(step, index))
                    {
                        return;
                    }
                    step.targetBody.simulated = true;
                    step.targetBody.linearVelocity = step.vectorValue;
                    break;

                case PersistentActionKind.MoveToDestination:
                    if (step.targetTransform == null)
                    {
                        WarnMissing(step, index, "Target Transform");
                        return;
                    }
                    if (step.destination == null)
                    {
                        WarnMissing(step, index, "Destination");
                        return;
                    }
                    step.targetTransform.position = step.destination.position;
                    if (step.boolValue)
                    {
                        step.targetTransform.rotation = step.destination.rotation;
                    }
                    Physics2D.SyncTransforms();
                    break;

                case PersistentActionKind.PlayAudioCue:
                    if (step.audioCue == null)
                    {
                        WarnMissing(step, index, "Audio Cue");
                        return;
                    }
                    if (AudioService.Instance == null)
                    {
                        Warn("Step " + (index + 1) + " cannot play audio because no AudioService exists in the scene.");
                        return;
                    }
                    Transform audioOrigin = step.targetTransform != null ? step.targetTransform : transform;
                    AudioService.Instance.Play(step.audioCue, audioOrigin.position);
                    break;

                case PersistentActionKind.InvokeCustomEvent:
                    step.customEvent?.Invoke();
                    break;
            }
        }

        private bool HasBody(PersistentActionStep2D step, int index)
        {
            if (step.targetBody != null)
            {
                return true;
            }

            WarnMissing(step, index, "Target Rigidbody 2D");
            return false;
        }

        private bool HasAnimatorParameter(
            PersistentActionStep2D step,
            int index,
            AnimatorControllerParameterType expectedType)
        {
            if (step.targetAnimator == null)
            {
                WarnMissing(step, index, "Target Animator");
                return false;
            }

            if (string.IsNullOrWhiteSpace(step.parameterName))
            {
                WarnMissing(step, index, "Parameter Name");
                return false;
            }

            AnimatorControllerParameter[] parameters = step.targetAnimator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == step.parameterName &&
                    parameters[i].type == expectedType)
                {
                    return true;
                }
            }

            Warn(
                "Persistent action step " + (index + 1) +
                " could not find Animator parameter '" + step.parameterName +
                "' with type " + expectedType + ".");
            return false;
        }

        private void WarnMissing(PersistentActionStep2D step, int index, string field)
        {
            string stepName = string.IsNullOrWhiteSpace(step.label)
                ? "step " + (index + 1)
                : "'" + step.label + "'";
            Warn("Persistent action " + stepName + " is missing " + field + ".");
        }

        private void Warn(string message)
        {
            if (warnAboutMissingTargets)
            {
                Debug.LogWarning(message, this);
            }
        }

        private static bool IsFlowingTime()
        {
            return TimeDirector.Instance == null || TimeDirector.Instance.Mode == TimeMode.Flowing;
        }
    }
}
