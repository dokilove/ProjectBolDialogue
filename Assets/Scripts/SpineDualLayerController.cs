using UnityEngine;
using Spine.Unity;
using System.Collections.Generic;
using System.Collections;
using System;

public class SpineDualLayerController : MonoBehaviour
{
    [Header("Spine Components")]
    public SkeletonAnimation skeletonAnimation;

    [Header("Current Status")]
    [SerializeField] private string currentBodyAnim;
    [SerializeField] private string currentFaceAnim;
    [SerializeField] private string currentExtraAnim;
    [SerializeField] private string prevBodyAnim;
    [SerializeField] private string prevFaceAnim;
    [SerializeField] private string prevExtraAnim;

    private Spine.AnimationState animState;
    private Color originalColor;

    private Queue<(IEnumerator routine, Action onComplete)> moveQueue = new Queue<(IEnumerator, Action)>();
    private bool isProcessingQueue = false;
    private Coroutine queueCoroutine;
    private Coroutine activeMoveCoroutine; // 현재 실행 중인 MoveRoutine 자체를 추적

    private Vector3? lastQueuedTarget = null;
    private bool lastQueuedAutoFlip = true;
    private Vector3 originalScale;

    void Awake()
    {
        animState = skeletonAnimation.AnimationState;
        if (skeletonAnimation != null)
        {
            originalColor = skeletonAnimation.skeleton.GetColor();
            originalScale = skeletonAnimation.transform.localScale;
        }
    }
    public void SetFacing(bool faceRight)
    {
        if (skeletonAnimation == null) return;
        var scale = skeletonAnimation.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * (faceRight ? -1f : 1f); // 부호 반전
        skeletonAnimation.transform.localScale = scale;
    }
    private bool TryPlayAnimation(int trackIndex, string animName, string logTag, out Spine.TrackEntry entry)
    {
        entry = null;

        // 1. 이름 검증
        if (string.IsNullOrEmpty(animName))
        {
            Debug.LogWarning($"{logTag}: 애니메이션 이름이 비어있습니다.");
            return false;
        }

        // 2. animState 초기화 확인
        if (animState == null)
        {
            Debug.LogError($"{logTag}: animState가 null입니다!");
            return false;
        }

        // 애니메이션 설정 및 3. entry null 체크
        entry = animState.SetAnimation(trackIndex, animName, true);
        if (entry == null)
        {
            Debug.LogError($"{logTag} 실패: '{animName}'을 Spine 스켈레톤에서 찾을 수 없습니다.");
            return false;
        }

        entry.MixDuration = 0.1f;
        return true;
    }

    public void SetBodyAnimation(string animName)
    {
        if (currentBodyAnim == animName) return;

        // 공통 함수 호출 성공 시, 문자열 업데이트
        if (TryPlayAnimation(0, animName, "BodyAnimation", out var entry))
        {
            prevBodyAnim = currentBodyAnim;
            currentBodyAnim = animName;
        }
    }

    public void SetFaceAnimation(string animName)
    {
        if (currentFaceAnim == animName) return;

        // 공통 함수 호출 성공 시, 문자열 업데이트
        if (TryPlayAnimation(1, animName, "FaceAnimation", out var entry))
        {
            prevFaceAnim = currentFaceAnim;
            currentFaceAnim = animName;
        }
    }

    public void SetExtraAnimation(string animName)
    {
        if (currentExtraAnim == animName) return;

        if (TryPlayAnimation(2, animName, "ExtraAnimation", out var entry))
        {
            prevExtraAnim = currentExtraAnim;
            currentExtraAnim = animName;
        }
    }
    public void ClearFaceAndSetBodyAnimation(string animName)
    {
        animState.ClearTrack(0);
        animState.ClearTrack(1);
        currentFaceAnim = string.Empty;

        if (currentBodyAnim == animName) return;

        animState.SetAnimation(0, animName, true);
        currentBodyAnim = animName;
    }

    public void ClearFaceAnimation()
    {
        animState.ClearTrack(1);
        currentFaceAnim = string.Empty;
    }

    public void ClearExtraAnimation()
    {
        animState.ClearTrack(2);
        currentExtraAnim = string.Empty;
    }

    public void HoldExtraAnimation()
    {
        animState.ClearTrack(2);
        prevExtraAnim = currentExtraAnim;
        currentExtraAnim = string.Empty;
    }

    public void ReplayPrevFaceAnimation()
    {
        if (!string.IsNullOrEmpty(prevFaceAnim))
        {
            SetFaceAnimation(prevFaceAnim);
        }
    }

    public void ReplayPrevBodyAnimation()
    {
        if (!string.IsNullOrEmpty(prevBodyAnim))
        {
            SetBodyAnimation(prevBodyAnim);
        }
    }

    public void ReplayPrevExtraAnimation()
    {
        if (!string.IsNullOrEmpty(prevExtraAnim))
        {
            SetExtraAnimation(prevExtraAnim);
        }
    }

    public void SetColor(Color color)
    {
        if (skeletonAnimation != null)
        {
            skeletonAnimation.skeleton.SetColor(color);
        }
    }

    public void Unfocus()
    {
        SetColor(new Color(0.5f, 0.5f, 0.5f, 1f));
    }

    public void Focus()
    {
        SetColor(originalColor);
    }

    public void EnqueueMove(Vector3 target, float duration, float bounceHeight, float squash, bool autoFlip, Action onComplete = null)
    {
        lastQueuedTarget = target;
        lastQueuedAutoFlip = autoFlip;

        moveQueue.Enqueue((MoveRoutine(target, duration, bounceHeight, squash, autoFlip), onComplete));
        if (!isProcessingQueue)
        {
            queueCoroutine = StartCoroutine(ProcessMoveQueue());
        }
    }

    public void SkipAllMoves()
    {
        if (moveQueue.Count == 0 && !isProcessingQueue) return;

        // 이동 관련 코루틴만 정확히 정리 (다른 기능의 코루틴은 안 건드림)
        if (queueCoroutine != null) StopCoroutine(queueCoroutine);
        if (activeMoveCoroutine != null) StopCoroutine(activeMoveCoroutine);
        queueCoroutine = null;
        activeMoveCoroutine = null;

        moveQueue.Clear();
        isProcessingQueue = false;

        if (lastQueuedTarget.HasValue && skeletonAnimation != null)
        {
            Transform t = skeletonAnimation.transform;
            Vector3 target = lastQueuedTarget.Value;

            if (lastQueuedAutoFlip)
            {
                SetFacing(target.x > t.position.x);
            }

            t.position = target;

            float sign = Mathf.Sign(t.localScale.x);
            t.localScale = new Vector3(sign * Mathf.Abs(originalScale.x), Mathf.Abs(originalScale.y), originalScale.z);
        }

        lastQueuedTarget = null;
    }

    public void ClearMoveQueue()
    {
        SkipAllMoves(); // 기존 ClearMoveQueue도 동일 로직 재사용
    }

    IEnumerator ProcessMoveQueue()
    {
        isProcessingQueue = true;
        while (moveQueue.Count > 0)
        {
            var (routine, onComplete) = moveQueue.Dequeue();

            // StartCoroutine의 반환값(Coroutine 객체)을 저장해뒀다가 스킵 시 개별 정지
            activeMoveCoroutine = StartCoroutine(routine);
            yield return activeMoveCoroutine;

            activeMoveCoroutine = null;
            onComplete?.Invoke();
        }
        isProcessingQueue = false;
        lastQueuedTarget = null;
    }

    IEnumerator MoveRoutine(Vector3 target, float duration, float bounceHeight, float squash, bool autoFlip)
    {
        Transform t = skeletonAnimation.transform;
        Vector3 start = t.position;

        // flip을 먼저 적용
        if (autoFlip && Mathf.Abs(target.x - start.x) > 0.01f)
        {
            SetFacing(target.x > start.x);
        }

        // flip 적용 이후의 스케일을 baseScale로 캡처 (순서 변경 핵심)
        Vector3 baseScale = t.localScale;

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
                pos.y += bounce;

                if (squash > 0f)
                {
                    float stretchFactor = 1f + (bounce / bounceHeight) * squash;
                    float squashFactor = 1f - (bounce / bounceHeight) * squash * 0.6f;
                    t.localScale = new Vector3(baseScale.x * squashFactor, baseScale.y * stretchFactor, baseScale.z);
                }
            }

            t.position = pos;
            yield return null;
        }

        t.position = target;
        t.localScale = baseScale; // 이제 flip된 부호 그대로 복귀되므로 정상
    }
}

