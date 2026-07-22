using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class CharacterRootController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private SpineVisualContainerController visualContainerController;

    // --- Fields for movement and flip ---
    private Vector3? lastQueuedTarget = null;
    private bool lastQueuedAutoFlip = true;
    private float spinFactor = 1f;

    private Queue<(IEnumerator routine, Action onComplete)> moveQueue = new Queue<(IEnumerator, Action)>();
    private bool isProcessingQueue = false;
    private Coroutine queueCoroutine;
    private Coroutine activeMoveCoroutine;
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

    // ===================== Movement Queue Logic =====================

    public void EnqueueMove(Vector3 target, float duration, float bounceHeight, float squash, bool autoFlip, float flipDuration = 0f, Action onComplete = null)
    {
        lastQueuedTarget = target;
        lastQueuedAutoFlip = autoFlip;

        moveQueue.Enqueue((MoveRoutine(target, duration, bounceHeight, squash, autoFlip, flipDuration), onComplete));
        if (!isProcessingQueue)
        {
            queueCoroutine = StartCoroutine(ProcessMoveQueue());
        }
    }

    public void SkipAllMoves()
    {
        if (moveQueue.Count == 0 && !isProcessingQueue) return;

        if (queueCoroutine != null) StopCoroutine(queueCoroutine);
        if (activeMoveCoroutine != null) StopCoroutine(activeMoveCoroutine);
        if (activeFlipCoroutine != null) StopCoroutine(activeFlipCoroutine);
        queueCoroutine = null;
        activeMoveCoroutine = null;
        activeFlipCoroutine = null;

        moveQueue.Clear();
        isProcessingQueue = false;

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

        lastQueuedTarget = null;
    }

    public void ClearMoveQueue() => SkipAllMoves();

    IEnumerator ProcessMoveQueue()
    {
        isProcessingQueue = true;
        while (moveQueue.Count > 0)
        {
            var (routine, onComplete) = moveQueue.Dequeue();
            activeMoveCoroutine = StartCoroutine(routine);
            yield return activeMoveCoroutine;
            activeMoveCoroutine = null;
            onComplete?.Invoke();
        }
        isProcessingQueue = false;
        lastQueuedTarget = null;
    }

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
}