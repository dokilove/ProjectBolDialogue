using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class CharacterRootController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private SpineVisualContainerController visualContainerController;

    private class QueuedAction
    {
        public int id;
        public IEnumerator routine;
        public Action onComplete;
        public Vector3 targetPos;
        public float targetAngle;
        public Vector3? targetScale;
        public Vector2 pivot;
        public bool autoFlip;
        public bool isMoveOnly;
        public bool isRotateOnly;
    }

    private int actionIdCounter = 0;
    private QueuedAction currentRunningAction = null;
    private Queue<QueuedAction> actionQueue = new Queue<QueuedAction>();
    private bool isProcessingActionQueue = false;
    private Coroutine actionQueueCoroutine;
    private Coroutine activeFlipCoroutine;

    // --- Unified Action Queue for Movement & Rotation ---
    private Vector3? lastQueuedTarget = null;
    private bool lastQueuedAutoFlip = true;
    private (float angle, Vector2 pivot)? lastQueuedRotate = null;
    private Vector3? lastQueuedScale = null;
    private float spinFactor = 1f;

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

    public int EnqueueMove(Vector3 target, float duration, float bounceHeight, float squash, bool autoFlip, float flipDuration = 0f, Action onComplete = null)
    {
        lastQueuedTarget = target;
        lastQueuedAutoFlip = autoFlip;

        int actionId = ++actionIdCounter;
        var action = new QueuedAction
        {
            id = actionId,
            routine = MoveRoutine(target, duration, bounceHeight, squash, autoFlip, flipDuration),
            onComplete = onComplete,
            targetPos = target,
            targetAngle = lastQueuedRotate.HasValue ? lastQueuedRotate.Value.angle : TargetVisualTransform.eulerAngles.z,
            targetScale = lastQueuedScale,
            pivot = lastQueuedRotate.HasValue ? lastQueuedRotate.Value.pivot : new Vector2(0.5f, 0.5f),
            autoFlip = autoFlip,
            isMoveOnly = true,
            isRotateOnly = false
        };

        actionQueue.Enqueue(action);
        if (!isProcessingActionQueue)
        {
            actionQueueCoroutine = StartCoroutine(ProcessActionQueue());
        }
        return actionId;
    }

    public int EnqueueRotate(float targetAngle, float duration, Vector2 normalizedPivot, Action onComplete = null, EaseType easeType = EaseType.EaseInOut)
    {
        lastQueuedRotate = (targetAngle, normalizedPivot);

        int actionId = ++actionIdCounter;
        IEnumerator routine = (duration <= 0f)
            ? InstantRotateWrapper(targetAngle, normalizedPivot)
            : RotateRoutine(targetAngle, duration, normalizedPivot, easeType);

        var action = new QueuedAction
        {
            id = actionId,
            routine = routine,
            onComplete = onComplete,
            targetPos = lastQueuedTarget ?? transform.position,
            targetAngle = targetAngle,
            targetScale = lastQueuedScale,
            pivot = normalizedPivot,
            autoFlip = lastQueuedAutoFlip,
            isMoveOnly = false,
            isRotateOnly = true
        };

        actionQueue.Enqueue(action);
        if (!isProcessingActionQueue)
        {
            actionQueueCoroutine = StartCoroutine(ProcessActionQueue());
        }
        return actionId;
    }

    private IEnumerator InstantRotateWrapper(float targetAngle, Vector2 normalizedPivot)
    {
        ApplyRotationInstant(targetAngle, normalizedPivot);
        yield break;
    }

    public int EnqueueTransform(Vector3 targetPos, float targetAngle, Vector3? targetScale, float duration, Vector2 normalizedPivot, float bounceHeight = 0f, float squash = 0f, bool autoFlip = true, float flipDuration = 0f, EaseType easeType = EaseType.EaseInOut, Action onComplete = null)
    {
        lastQueuedTarget = targetPos;
        lastQueuedRotate = (targetAngle, normalizedPivot);
        lastQueuedAutoFlip = autoFlip;
        lastQueuedScale = targetScale;

        int actionId = ++actionIdCounter;
        IEnumerator routine = (duration <= 0f)
            ? InstantTransformWrapper(targetPos, targetAngle, targetScale, normalizedPivot, autoFlip)
            : TransformRoutine(targetPos, targetAngle, targetScale, duration, normalizedPivot, bounceHeight, squash, autoFlip, flipDuration, easeType);

        var action = new QueuedAction
        {
            id = actionId,
            routine = routine,
            onComplete = onComplete,
            targetPos = targetPos,
            targetAngle = targetAngle,
            targetScale = targetScale,
            pivot = normalizedPivot,
            autoFlip = autoFlip,
            isMoveOnly = false,
            isRotateOnly = false
        };

        actionQueue.Enqueue(action);
        if (!isProcessingActionQueue)
        {
            actionQueueCoroutine = StartCoroutine(ProcessActionQueue());
        }
        return actionId;
    }

    private IEnumerator InstantTransformWrapper(Vector3 targetPos, float targetAngle, Vector3? targetScale, Vector2 normalizedPivot, bool autoFlip)
    {
        SnapTo(targetPos, targetAngle, targetScale, normalizedPivot, autoFlip);
        yield break;
    }

    private IEnumerator ProcessActionQueue()
    {
        isProcessingActionQueue = true;
        while (actionQueue.Count > 0)
        {
            currentRunningAction = actionQueue.Dequeue();
            IEnumerator routine = currentRunningAction.routine;
            while (routine.MoveNext())
            {
                yield return routine.Current;
            }

            var completedAction = currentRunningAction;
            currentRunningAction = null;
            completedAction?.onComplete?.Invoke();
        }

        isProcessingActionQueue = false;
        actionQueueCoroutine = null;
        lastQueuedTarget = null;
        lastQueuedRotate = null;
        lastQueuedScale = null;
    }

    /// <summary>
    /// 특정 시퀀서 커맨드가 스킵 또는 조기 파괴되었을 때 호출됩니다.
    /// 해당 액션을 즉시 목표 상태로 스냅(Snap)하고, 큐에 대기 중인 다음 액션들은 안전하게 이어질 수 있도록 합니다.
    /// </summary>
    public void CancelOrSnapAction(int actionId)
    {
        // 1. 현재 실행 중인 액션인 경우
        if (currentRunningAction != null && currentRunningAction.id == actionId)
        {
            if (actionQueueCoroutine != null)
            {
                StopCoroutine(actionQueueCoroutine);
                actionQueueCoroutine = null;
            }

            var actionToSnap = currentRunningAction;
            currentRunningAction = null;

            if (actionToSnap.isMoveOnly)
            {
                SnapPosition(actionToSnap.targetPos, actionToSnap.autoFlip);
            }
            else if (actionToSnap.isRotateOnly)
            {
                ApplyRotationInstant(actionToSnap.targetAngle, actionToSnap.pivot);
            }
            else
            {
                SnapTo(actionToSnap.targetPos, actionToSnap.targetAngle, actionToSnap.targetScale, actionToSnap.pivot, actionToSnap.autoFlip);
            }

            actionToSnap.onComplete?.Invoke();

            if (actionQueue.Count > 0)
            {
                actionQueueCoroutine = StartCoroutine(ProcessActionQueue());
            }
            else
            {
                isProcessingActionQueue = false;
                lastQueuedTarget = null;
                lastQueuedRotate = null;
                lastQueuedScale = null;
            }
            return;
        }

        // 2. 큐에 대기 중인 액션인 경우 (실행 전에 파괴/스킵된 경우)
        List<QueuedAction> remaining = new List<QueuedAction>();
        QueuedAction matchedWaiting = null;
        while (actionQueue.Count > 0)
        {
            var item = actionQueue.Dequeue();
            if (item.id == actionId)
            {
                matchedWaiting = item;
            }
            else
            {
                remaining.Add(item);
            }
        }
        foreach (var item in remaining) actionQueue.Enqueue(item);

        if (matchedWaiting != null)
        {
            if (matchedWaiting.isMoveOnly)
            {
                SnapPosition(matchedWaiting.targetPos, matchedWaiting.autoFlip);
            }
            else if (matchedWaiting.isRotateOnly)
            {
                ApplyRotationInstant(matchedWaiting.targetAngle, matchedWaiting.pivot);
            }
            else
            {
                SnapTo(matchedWaiting.targetPos, matchedWaiting.targetAngle, matchedWaiting.targetScale, matchedWaiting.pivot, matchedWaiting.autoFlip);
            }
            matchedWaiting.onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 모든 액션을 즉시 중단하고 최종 큐 상태로 강제 스냅합니다.
    /// </summary>
    public void SkipAllActions()
    {
        if (actionQueue.Count == 0 && !isProcessingActionQueue && currentRunningAction == null) return;

        if (actionQueueCoroutine != null) StopCoroutine(actionQueueCoroutine);
        if (activeFlipCoroutine != null) StopCoroutine(activeFlipCoroutine);
        actionQueueCoroutine = null;
        activeFlipCoroutine = null;

        Vector3 finalPos = lastQueuedTarget ?? (currentRunningAction != null ? currentRunningAction.targetPos : transform.position);
        float finalAngle = lastQueuedRotate.HasValue ? lastQueuedRotate.Value.angle : (currentRunningAction != null ? currentRunningAction.targetAngle : TargetVisualTransform.eulerAngles.z);
        Vector2 finalPivot = lastQueuedRotate.HasValue ? lastQueuedRotate.Value.pivot : (currentRunningAction != null ? currentRunningAction.pivot : new Vector2(0.5f, 0.5f));
        Vector3? finalScale = lastQueuedScale ?? (currentRunningAction != null ? currentRunningAction.targetScale : (Vector3?)null);
        bool finalAutoFlip = lastQueuedAutoFlip;

        var running = currentRunningAction;
        currentRunningAction = null;
        running?.onComplete?.Invoke();

        while (actionQueue.Count > 0)
        {
            var item = actionQueue.Dequeue();
            finalPos = item.targetPos;
            if (!item.isMoveOnly)
            {
                finalAngle = item.targetAngle;
                finalPivot = item.pivot;
            }
            if (item.targetScale.HasValue) finalScale = item.targetScale;
            finalAutoFlip = item.autoFlip;
            item.onComplete?.Invoke();
        }

        isProcessingActionQueue = false;

        SnapTo(finalPos, finalAngle, finalScale, finalPivot, finalAutoFlip);

        lastQueuedTarget = null;
        lastQueuedRotate = null;
        lastQueuedScale = null;
    }

    public void SkipAllMoves() => SkipAllActions();
    public void ClearMoveQueue() => SkipAllActions();
    public void SkipAllRotations() => SkipAllActions();
    public void ClearRotateQueue() => SkipAllActions();
    public void StopRotation() => SkipAllActions();
    public void SkipAllTransforms() => SkipAllActions();

    // ===================== Snap Helpers =====================

    public void SnapPosition(Vector3 targetPos, bool autoFlip)
    {
        if (autoFlip && Mathf.Abs(targetPos.x - transform.position.x) > 0.01f)
        {
            SetFacing(targetPos.x > transform.position.x);
        }
        transform.position = targetPos;
        if (visualContainerController != null && visualContainerController.modelController != null)
        {
            visualContainerController.modelController.ResetSquashAndStretch();
        }
    }

    public void SnapTo(Vector3 targetPos, float targetAngle, Vector3? targetScale, Vector2 normalizedPivot, bool autoFlip)
    {
        SnapPosition(targetPos, autoFlip);

        if (targetScale.HasValue)
        {
            transform.localScale = targetScale.Value;
        }

        ApplyRotationInstant(targetAngle, normalizedPivot);
    }

    public void ApplyRotationInstant(float targetAngle, Vector2 normalizedPivot)
    {
        Transform t = TargetVisualTransform;

        // 1. 항상 기준 미회전 상태로 초기화하여 오차 누적 방지
        if (visualContainerController != null)
        {
            visualContainerController.ResetToDefaultLocalTransform();
        }
        else
        {
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
        }

        // 2. 목표 각도가 0도(또는 360도 배수)인 경우 이미 완벽한 기본 상태이므로 즉시 리턴
        if (Mathf.Abs(Mathf.DeltaAngle(0f, targetAngle)) < 0.001f)
        {
            return;
        }

        // 3. 깨끗한 기준 상태에서 피벗을 계산하여 정확한 회전 적용
        Vector3 pivotWorldPoint = GetPivotWorldPoint(normalizedPivot);
        Vector3 initialOffset = t.position - pivotWorldPoint;
        Vector3 rotatedOffset = Quaternion.Euler(0, 0, targetAngle) * initialOffset;

        t.position = pivotWorldPoint + rotatedOffset;
        t.rotation = Quaternion.Euler(0, 0, targetAngle);
    }

    // ===================== Routine Implementations =====================

    IEnumerator MoveRoutine(Vector3 target, float duration, float bounceHeight, float squash, bool autoFlip, float flipDuration)
    {
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
        MeshFilter meshFilter = GetComponentInChildren<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Bounds b = meshFilter.sharedMesh.bounds;
            float lx = Mathf.Lerp(b.min.x, b.max.x, normalizedPivot.x);
            float ly = Mathf.Lerp(b.min.y, b.max.y, normalizedPivot.y);
            Vector3 worldPt = meshFilter.transform.TransformPoint(new Vector3(lx, ly, 0f));
            worldPt.z = TargetVisualTransform.position.z;
            return worldPt;
        }

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

    public int RotateTo(float targetAngle, float duration, Vector2 normalizedPivot, Action onComplete = null, EaseType easeType = EaseType.EaseInOut)
    {
        SkipAllActions();
        return EnqueueRotate(targetAngle, duration, normalizedPivot, onComplete, easeType);
    }

    public int TransformTo(Vector3 targetPos, float targetAngle, Vector3? targetScale, float duration, Vector2 normalizedPivot, float bounceHeight = 0f, float squash = 0f, bool autoFlip = true, float flipDuration = 0f, EaseType easeType = EaseType.EaseInOut, Action onComplete = null)
    {
        SkipAllActions();
        return EnqueueTransform(targetPos, targetAngle, targetScale, duration, normalizedPivot, bounceHeight, squash, autoFlip, flipDuration, easeType, onComplete);
    }

    private IEnumerator TransformRoutine(Vector3 targetPos, float targetAngle, Vector3? targetScale, float duration, Vector2 normalizedPivot, float bounceHeight, float squash, bool autoFlip, float flipDuration, EaseType easeType)
    {
        Transform root = this.transform;
        Transform visual = TargetVisualTransform;

        Vector3 startRootPos = root.position;
        Vector3 startScale = root.localScale;
        Vector3 destScale = targetScale ?? startScale;
        bool doScale = targetScale.HasValue;

        if (autoFlip && Mathf.Abs(targetPos.x - startRootPos.x) > 0.01f)
        {
            bool faceRight = targetPos.x > startRootPos.x;
            if (flipDuration > 0f)
                SetFacingOverTime(faceRight, flipDuration);
            else
                SetFacing(faceRight);
        }

        float distance = Mathf.Abs(targetPos.x - startRootPos.x);
        int steps = Mathf.Max(2, Mathf.RoundToInt(distance / 1.2f));

        Vector3 initialPivot = GetPivotWorldPoint(normalizedPivot);
        Vector3 initialOffset = visual.position - initialPivot;
        float startAngle = visual.eulerAngles.z;
        float deltaAngle = (Mathf.Abs(targetAngle) >= 360f) ? (targetAngle - startAngle) : Mathf.DeltaAngle(startAngle, targetAngle);
        Quaternion startRotation = visual.rotation;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);
            float easedRatio = EvaluateEase(easeType, ratio);

            // 1. Move root
            Vector3 currentRootPos = Vector3.Lerp(startRootPos, targetPos, easedRatio);
            Vector3 rootDelta = currentRootPos - startRootPos;
            root.position = currentRootPos;

            // 2. Scale root
            float scaleMultiplier = 1f;
            if (doScale)
            {
                root.localScale = Vector3.Lerp(startScale, destScale, easedRatio);
                if (Mathf.Abs(startScale.x) > 0.0001f)
                {
                    scaleMultiplier = root.localScale.x / startScale.x;
                }
            }

            // 3. Rotate visual around moving pivot
            float currentDelta = deltaAngle * easedRatio;
            Vector3 rotatedOffset = Quaternion.Euler(0, 0, currentDelta) * (initialOffset * scaleMultiplier);
            Vector3 currentPivot = initialPivot + rootDelta;

            visual.position = currentPivot + rotatedOffset;
            visual.rotation = Quaternion.Euler(startRotation.eulerAngles.x, startRotation.eulerAngles.y, startAngle + currentDelta);

            // 4. Bounce & squash
            if (bounceHeight > 0f)
            {
                float cycle = (ratio * steps) % 1f;
                float bounce = Mathf.Sin(cycle * Mathf.PI) * bounceHeight;
                if (visualContainerController != null && visualContainerController.modelController != null)
                {
                    visualContainerController.modelController.ApplyBounceAndSquash(bounce, squash);
                }
            }

            yield return null;
        }

        // Final snap
        root.position = targetPos;
        if (doScale)
        {
            root.localScale = destScale;
        }

        ApplyRotationInstant(targetAngle, normalizedPivot);

        if (visualContainerController != null && visualContainerController.modelController != null)
        {
            visualContainerController.modelController.ResetSquashAndStretch();
        }
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

        ApplyRotationInstant(targetAngle, normalizedPivot);
    }

    public static float EvaluateEase(EaseType easeType, float t)
    {
        t = Mathf.Clamp01(t);
        switch (easeType)
        {
            case EaseType.Linear:
                return t;
            case EaseType.EaseIn:
                return t * t * t;
            case EaseType.EaseOut:
                float f = 1f - t;
                return 1f - f * f * f;
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