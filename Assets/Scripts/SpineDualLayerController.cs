using UnityEngine;
using Spine.Unity;

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

    void Awake()
    {
        animState = skeletonAnimation.AnimationState;
        if (skeletonAnimation != null)
        {
            originalColor = skeletonAnimation.skeleton.GetColor();
        }
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
}

