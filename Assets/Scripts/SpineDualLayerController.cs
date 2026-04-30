using UnityEngine;
using Spine.Unity;

public class SpineDualLayerController : MonoBehaviour
{
    [Header("Spine Components")]
    public SkeletonAnimation skeletonAnimation;

    [Header("Current Status")]
    [SerializeField] private string currentBodyAnim;
    [SerializeField] private string currentFaceAnim;

    private Spine.AnimationState animState;

    void Awake()
    {
        animState = skeletonAnimation.AnimationState;
    }

    /// <summary>
    /// 베이스가 되는 몸 동작을 변경합니다 (Track 0)
    /// </summary>
    public void SetBodyAnimation(string animName)
    {
        if (currentBodyAnim == animName) return;

        animState.SetAnimation(0, animName, true);
        currentBodyAnim = animName;
    }

    /// <summary>
    /// 위에 덮어씌울 표정 애니메이션을 변경합니다 (Track 1)
    /// </summary>
    public void SetFaceAnimation(string animName)
    {
        if (currentFaceAnim == animName) return;

        // Track 1에 애니메이션을 재생하면 Track 0의 애니메이션 중 
        // 표정과 관련된 키프레임만 실시간으로 덮어씌웁니다.
        var entry = animState.SetAnimation(1, animName, true);

        // 표정의 경우 믹싱(부드러운 전환) 시간을 짧게 주는 것이 표정 변화가 빠릿합니다.
        entry.MixDuration = 0.1f;

        currentFaceAnim = animName;
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

    /// <summary>
    /// 표정 애니메이션을 제거하고 베이스 동작의 표정으로 돌아갑니다.
    /// </summary>
    public void ClearFaceAnimation()
    {
        animState.ClearTrack(1);
        currentFaceAnim = string.Empty;
    }

}