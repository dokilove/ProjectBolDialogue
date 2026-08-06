using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class CharacterRootController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private SpineVisualContainerController visualContainerController;

    // --- Unified Action Queue for Movement & Rotation ---
    private Vector3? lastQueuedTarget = null;
    private bool lastQueuedAutoFlip = true;
    private (float angle, Vector2 pivot)? lastQueuedRotate = null;
    private float spinFactor = 1f;

    private Queue<(IEnumerator routine, Action onComplete)> actionQueue = new Queue<(IEnumerator, Action)>();
    private bool isProcessingActionQueue = false;
    private Coroutine actionQueueCoroutine;
    private Coroutine activeActionCoroutine;
    private Coroutine activeFlipCoroutine;

    void Awake()
    {
        if (visualContainerController == null)
        {
            visualContainerController = GetComponentInChildren<SpineVisualContainerController>();
        }
    }

    // ===================== Flip Logic =====================

    public void SetFacing(bool faceRight)
    {
        if (activeFlipCoroutine != null)
        {
            StopCoroutine(activeFlipCoroutine);
            activeFlipCoroutine = null;
        }
        spinFactor = faceRight ? -1f : 1f;
        if (visualContainerController != null && visualContainerController.modelController != null)
        {
            visualContainerController.modelController.SetSpinFactor(spinFactor);
        }
    }

    public void SetFacingOverTime(bool faceRight, float duration)
    {
        if (activeFlipCoroutine != null) StopCoroutine(activeFlipCoroutine);
        activeFlipCoroutine = StartCoroutine(FlipRoutine(faceRight, duration));
    }

    private IEnumerator FlipRoutine(bool faceRight, float duration)
    {
        float targetSign = faceRight ? -1f : 1f;
        float startSign = spinFactor;

        if (duration <= 0f)
        {
            spinFactor = targetSign;
            if (visualContainerController != null && visualContainerController.modelController != null)
            {
                visualContainerController.modelController.SetSpinFactor(spinFactor);
            }
            activeFlipCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            spinFactor = Mathf.Lerp(startSign, targetSign, ratio);
            if (visualContainerController != null && visualContainerController.modelController != null)
            {
                visualContainerController.modelController.SetSpinFactor(spinFactor);
            }
            yield return null;
        }

        spinFactor = targetSign;
        if (visualContainerController != null && visualContainerController.modelController != null)
        {
            visualContainerController.modelController.SetSpinFactor(spinFactor);
        }
        activeFlipCoroutine = null;
    }

    // ===================== Unified Action Queue Logic =====================

    public void EnqueueMove(Vector3 target, float duration, float bounceHeight, float squash, bool autoFlip, float flipDuration = 0f, Action onComplete = null)
    {
        lastQueuedTarget = target;
        lastQueuedAutoFlip = autoFlip;

        actionQueue.Enqueue((MoveRoutine(target, duration, bounceHeight, squash, autoFlip, flipDuration), onComplete));
        if (!isProcessingActionQueue)
        {
            actionQueueCoroutine = StartCoroutine(ProcessActionQueue());
        }
    }

    public void EnqueueRotate(float targetAngle, float duration, Vector2 normalizedPivot, Action onComplete = null, EaseType easeType = EaseType.EaseInOut)
    {
        lastQueuedRotate = (targetAngle, normalizedPivot);

        if (duration <= 0f)
        {
            actionQueue.Enqueue((InstantRotateWrapper(targetAngle, normalizedPivot), onComplete));
        }
        else
        {
            actionQueue.Enqueue((RotateRoutine(targetAngle, duration, normalizedPivot, easeType), onComplete));
        }

        if (!isProcessingActionQueue)
        {
            actionQueueCoroutine = StartCoroutine(ProcessActionQueue());
        }
    }

    private IEnumerator InstantRotateWrapper(float targetAngle, Vector2 normalizedPivot)
    {
        ApplyRotationInstant(targetAngle, normalizedPivot);
        yield break;
    }

    private IEnumerator ProcessActionQueue()
    {
        isProcessingActionQueue = true;
        while (actionQueue.Count > 0)
        {
            var (routine, onComplete) = actionQueue.Dequeue();
            activeActionCoroutine = StartCoroutine(routine);
            yield return activeActionCoroutine;
            activeActionCoroutine = null;
            onComplete?.Invoke();
        }
        isProcessingActionQueue = false;
        lastQueuedTarget = null;
        lastQueuedRotate = null;
    }

    public void SkipAllActions()
    {
        if (actionQueue.Count == 0 && !isProcessingActionQueue) return;

        if (actionQueueCoroutine != null) StopCoroutine(actionQueueCoroutine);
        if (activeActionCoroutine != null) StopCoroutine(activeActionCoroutine);
        if (activeFlipCoroutine != null) StopCoroutine(activeFlipCoroutine);
        actionQueueCoroutine = null;
        activeActionCoroutine = null;
        activeFlipCoroutine = null;

        actionQueue.Clear();
        isProcessingActionQueue = false;

        if (lastQueuedTarget.HasValue)
        {
            if (lastQueuedAutoFlip)
            {
                SetFacing(lastQueuedTarget.Value.x > transform.position.x);
            }
            transform.position = lastQueuedTarget.Value;
            if (visualContainerController != null && visualContainerController.modelController != null)
            {
                visualContainerController.modelController.ResetSquashAndStretch();
            }
        }

        if (lastQueuedRotate.HasValue)
        {
            ApplyRotationInstant(lastQueuedRotate.Value.angle, lastQueuedRotate.Value.pivot);
        }

        lastQueuedTarget = null;
        lastQueuedRotate = null;
    }

    public void SkipAllMoves() => SkipAllActions();
    public void ClearMoveQueue() => SkipAllActions();
    public void SkipAllRotations() => SkipAllActions();
    public void ClearRotateQueue() => SkipAllActions();
    public void StopRotation() => SkipAllActions();

    IEnumerator MoveRoutine(Vector3 target, float duration, float bounceHeight, float squash, bool autoFlip, float flipDuration)
    {
        // This routine now moves the root transform (this.transform)
        Transform t = this.transform;
        Vector3 start = t.position;

        if (autoFlip && Mathf.Abs(target.x - start.x) > 0.01f)
        {
            bool faceRight = target.x > start.x;
            if (flipDuration > 0f)
                SetFacingOverTime(faceRight, flipDuration);
            else
                SetFacing(faceRight);
        }

        float distance = Mathf.Abs(target.x - start.x);
        int steps = Mathf.Max(2, Mathf.RoundToInt(distance / 1.2f));

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            float easedRatio = -(Mathf.Cos(Mathf.PI * ratio) - 1f) / 2f;
            Vector3 pos = Vector3.Lerp(start, target, easedRatio);

            if (bounceHeight > 0f)
            {
                float cycle = (ratio * steps) % 1f;
                float bounce = Mathf.Sin(cycle * Mathf.PI) * bounceHeight;
                if (visualContainerController != null && visualContainerController.modelController != null)
                {
                    visualContainerController.modelController.ApplyBounceAndSquash(bounce, squash);
                }
            }

            t.position = pos;
            yield return null;
        }

        t.position = target;
        if (visualContainerController != null && visualContainerController.modelController != null)
        {
            visualContainerController.modelController.ResetSquashAndStretch();
        }
    }

    // ===================== Rotation Helpers =====================

    private Transform TargetVisualTransform
    {
        get
        {
            if (visualContainerController != null)
                return visualContainerController.transform;
            return transform;
        }
    }

    public Vector3 GetPivotWorldPoint(Vector2 normalizedPivot)
    {
        MeshRenderer meshRenderer = GetComponentInChildren<MeshRenderer>();
        if (meshRenderer != null)
        {
            Bounds bounds = meshRenderer.bounds;
            float px = Mathf.Lerp(bounds.min.x, bounds.max.x, normalizedPivot.x);
            float py = Mathf.Lerp(bounds.min.y, bounds.max.y, normalizedPivot.y);
            return new Vector3(px, py, TargetVisualTransform.position.z);
        }
        return TargetVisualTransform.position;
    }

    public void RotateTo(float targetAngle, float duration, Vector2 normalizedPivot, Action onComplete = null, EaseType easeType = EaseType.EaseInOut)
    {
        SkipAllActions();
        EnqueueRotate(targetAngle, duration, normalizedPivot, onComplete, easeType);
    }

    private void ApplyRotationInstant(float targetAngle, Vector2 normalizedPivot)
    {
        Vector3 pivotWorldPoint = GetPivotWorldPoint(normalizedPivot);
        Transform t = TargetVisualTransform;

        float startAngle = t.eulerAngles.z;
        float deltaAngle = (Mathf.Abs(targetAngle) >= 360f) ? (targetAngle - startAngle) : Mathf.DeltaAngle(startAngle, targetAngle);

        Vector3 offset = t.position - pivotWorldPoint;
        Vector3 rotatedOffset = Quaternion.Euler(0, 0, deltaAngle) * offset;

        t.position = pivotWorldPoint + rotatedOffset;
        t.rotation = Quaternion.Euler(t.eulerAngles.x, t.eulerAngles.y, targetAngle);
    }

    private IEnumerator RotateRoutine(float targetAngle, float duration, Vector2 normalizedPivot, EaseType easeType = EaseType.EaseInOut)
    {
        Transform t = TargetVisualTransform;
        Vector3 pivotWorldPoint = GetPivotWorldPoint(normalizedPivot);

        float startAngle = t.eulerAngles.z;
        float deltaAngle = (Mathf.Abs(targetAngle) >= 360f) ? (targetAngle - startAngle) : Mathf.DeltaAngle(startAngle, targetAngle);
        Vector3 startPos = t.position;
        Vector3 startOffset = startPos - pivotWorldPoint;
        Quaternion startRotation = t.rotation;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            float easedRatio = EvaluateEase(easeType, ratio);

            float currentDelta = deltaAngle * easedRatio;
            Vector3 rotatedOffset = Quaternion.Euler(0, 0, currentDelta) * startOffset;

            t.position = pivotWorldPoint + rotatedOffset;
            t.rotation = Quaternion.Euler(startRotation.eulerAngles.x, startRotation.eulerAngles.y, startAngle + currentDelta);

            yield return null;
        }

        Vector3 finalOffset = Quaternion.Euler(0, 0, deltaAngle) * startOffset;
        t.position = pivotWorldPoint + finalOffset;
        t.rotation = Quaternion.Euler(startRotation.eulerAngles.x, startRotation.eulerAngles.y, targetAngle);
    }

    public static float EvaluateEase(EaseType easeType, float t)
    {
        t = Mathf.Clamp01(t);
        switch (easeType)
        {
            case EaseType.Linear:
                return t;
            case EaseType.EaseIn:
                return t * t * t; // Cubic EaseIn: Very slow start, fast snappy finish
            case EaseType.EaseOut:
                float f = 1f - t;
                return 1f - f * f * f; // Cubic EaseOut: Explosive fast start, smooth soft stop
            case EaseType.EaseInOut:
                return (t < 0.5f) ? (4f * t * t * t) : (1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f);
            case EaseType.EaseOutBack:
                float c1 = 1.70158f;
                float c3 = c1 + 1f;
                return 1f + c3 * Mathf.Pow(t - 1f, 3) + c1 * Mathf.Pow(t - 1f, 2);
            case EaseType.EaseInBack:
                float c2 = 1.70158f;
                return (c2 + 1f) * t * t * t - c2 * t * t;
            default:
                float f2 = 1f - t;
                return 1f - f2 * f2 * f2;
        }
    }
}

public enum EaseType
{
    Linear,
    EaseInOut,
    EaseIn,
    EaseOut,
    EaseOutBack,
    EaseInBack
}