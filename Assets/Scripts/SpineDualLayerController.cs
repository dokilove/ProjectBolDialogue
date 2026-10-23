using UnityEngine;
using Spine.Unity;
using System;
using System.Collections;

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
    private Vector3 originalScale;

    // 스테이지 데이터 추출/스냅샷용 현재 애니메이션 조회
    public string CurrentBodyAnimation => currentBodyAnim;
    public string CurrentFaceAnimation => currentFaceAnim;
    public string CurrentExtraAnimation => currentExtraAnim;

    // ---- 스케일 합성 관련 ----
    private float spinFactor = 1f;
    private float squashXFactor = 1f;
    private float stretchYFactor = 1f;

    void Awake()
    {
        animState = skeletonAnimation.AnimationState;
        if (skeletonAnimation != null)
        {
            originalColor = skeletonAnimation.skeleton.GetColor();
            originalScale = skeletonAnimation.transform.localScale;
        }
    }

    // ===================== Animation Logic =====================
    private bool TryPlayAnimation(int trackIndex, string animName, string logTag, out Spine.TrackEntry entry)
    {
        entry = null;
        if (string.IsNullOrEmpty(animName))
        {
            Debug.LogWarning($"{logTag}: 애니메이션 이름이 비어있습니다.");
            return false;
        }
        if (animState == null)
        {
            Debug.LogError($"{logTag}: animState가 null입니다!");
            return false;
        }
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
        if (TryPlayAnimation(0, animName, "BodyAnimation", out var entry))
        {
            prevBodyAnim = currentBodyAnim;
            currentBodyAnim = animName;
        }
    }

    public void SetFaceAnimation(string animName)
    {
        if (currentFaceAnim == animName) return;
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
        animState.ClearTrack(1);
        currentFaceAnim = string.Empty;

        // 같은 body 애니메이션이면 트랙을 지우지 않고 그대로 유지한다.
        if (currentBodyAnim == animName) return;

        animState.ClearTrack(0);
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
        if (!string.IsNullOrEmpty(prevFaceAnim)) SetFaceAnimation(prevFaceAnim);
    }

    public void ReplayPrevBodyAnimation()
    {
        if (!string.IsNullOrEmpty(prevBodyAnim)) SetBodyAnimation(prevBodyAnim);
    }

    public void ReplayPrevExtraAnimation()
    {
        if (!string.IsNullOrEmpty(prevExtraAnim)) SetExtraAnimation(prevExtraAnim);
    }

    // ===================== Color & Focus Logic =====================
    public void SetColor(Color color)
    {
        if (skeletonAnimation != null) skeletonAnimation.skeleton.SetColor(color);
    }

    public void Unfocus()
    {
        SetColor(new Color(0.5f, 0.5f, 0.5f, 1f));
    }

    public void Focus()
    {
        SetColor(originalColor);
    }

    // ===================== Scale & Depth Logic =====================
    public void ApplyScale()
    {
        if (skeletonAnimation == null) return;
        var t = skeletonAnimation.transform;
        float x = Mathf.Abs(originalScale.x) * spinFactor * squashXFactor;
        float y = Mathf.Abs(originalScale.y) * stretchYFactor;
        t.localScale = new Vector3(x, y, originalScale.z);
    }

    public void SetSpinFactor(float newSpinFactor)
    {
        spinFactor = newSpinFactor;
        ApplyScale();
    }

    public void ApplyBounceAndSquash(float bounce, float squashAmount)
    {
        if (squashAmount > 0f)
        {
            stretchYFactor = 1f + bounce * squashAmount * 5;
            squashXFactor = 1f - bounce * squashAmount * 3;
        }
        ApplyScale();
    }

    public void ResetSquashAndStretch()
    {
        squashXFactor = 1f;
        stretchYFactor = 1f;
        ApplyScale();
    }

    public struct VisualState
    {
        public float SpinFactor;
        public float SquashXFactor;
        public float StretchYFactor;
        public string BodyAnimation;
        public string FaceAnimation;
        public string ExtraAnimation;
        public string PrevBodyAnimation;
        public string PrevFaceAnimation;
        public string PrevExtraAnimation;
        public Color Color;
    }

    public VisualState CaptureState()
    {
        Color color = Color.white;
        if (skeletonAnimation != null && skeletonAnimation.skeleton != null)
        {
            color = skeletonAnimation.skeleton.GetColor();
        }

        return new VisualState
        {
            SpinFactor = spinFactor,
            SquashXFactor = squashXFactor,
            StretchYFactor = stretchYFactor,
            BodyAnimation = currentBodyAnim,
            FaceAnimation = currentFaceAnim,
            ExtraAnimation = currentExtraAnim,
            PrevBodyAnimation = prevBodyAnim,
            PrevFaceAnimation = prevFaceAnim,
            PrevExtraAnimation = prevExtraAnim,
            Color = color
        };
    }

    public void RestoreState(VisualState state)
    {
        spinFactor = state.SpinFactor;
        squashXFactor = state.SquashXFactor;
        stretchYFactor = state.StretchYFactor;
        currentBodyAnim = state.BodyAnimation;
        currentFaceAnim = state.FaceAnimation;
        currentExtraAnim = state.ExtraAnimation;
        prevBodyAnim = state.PrevBodyAnimation;
        prevFaceAnim = state.PrevFaceAnimation;
        prevExtraAnim = state.PrevExtraAnimation;

        if (animState != null)
        {
            RestoreTrack(0, state.BodyAnimation);
            RestoreTrack(1, state.FaceAnimation);
            RestoreTrack(2, state.ExtraAnimation);
        }

        ApplyScale();

        if (skeletonAnimation != null && skeletonAnimation.skeleton != null)
        {
            skeletonAnimation.skeleton.SetColor(state.Color);
        }
    }

    private void RestoreTrack(int trackIndex, string animName)
    {
        if (animState == null) return;

        if (string.IsNullOrEmpty(animName))
        {
            animState.ClearTrack(trackIndex);
            return;
        }

        var entry = animState.SetAnimation(trackIndex, animName, true);
        if (entry != null)
        {
            entry.MixDuration = 0.1f;
        }
    }
}
