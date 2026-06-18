using UnityEngine;
using Spine.Unity;

public class SpineDualLayerController : MonoBehaviour
{
    [Header("Spine Components")]
    public SkeletonAnimation skeletonAnimation;

    [Header("Current Status")]
    [SerializeField] private string currentBodyAnim;
    [SerializeField] private string currentFaceAnim;
    [SerializeField] private string prevBodyAnim;
    [SerializeField] private string prevFaceAnim;

    private Spine.AnimationState animState;

    void Awake()
    {
        animState = skeletonAnimation.AnimationState;
    }

    /// <summary>
    /// ���̽��� �Ǵ� �� ������ �����մϴ� (Track 0)
    /// </summary>
    public void SetBodyAnimation(string animName)
    {
        // 1. 애니메이션 이름 자체 검증
        if (string.IsNullOrEmpty(animName))
        {
            Debug.LogWarning("BodyAnimation: 애니메이션 이름이 비어있습니다.");
            return;
        }

        if (currentBodyAnim == animName) return;

        // 2. animState 초기화 여부 확인
        if (animState == null)
        {
            Debug.LogError("BodyAnimation: animState(AnimationState)가 null입니다!");
            return;
        }

        // 애니메이션 설정
        var entry = animState.SetAnimation(0, animName, true);

        // 3. entry가 null인지 체크 (존재하지 않는 애니메이션 이름일 때 발생)
        if (entry == null)
        {
            Debug.LogError($"BodyAnimation 실패: '{animName}' 이름의 애니메이션을 Spine 스켈레톤에서 찾을 수 없습니다.");
            return;
        }

        // 여기까지 무사히 와야 에러 없이 실행됨
        entry.MixDuration = 0.1f;

        prevBodyAnim = currentBodyAnim; // Save previous animation
        currentBodyAnim = animName;
    }

    public void SetFaceAnimation(string animName)
    {
        // 1. 애니메이션 이름 자체 검증
        if (string.IsNullOrEmpty(animName))
        {
            Debug.LogWarning("FaceAnimation: 애니메이션 이름이 비어있습니다.");
            return;
        }

        if (currentFaceAnim == animName) return;

        // 2. animState 초기화 여부 확인
        if (animState == null)
        {
            Debug.LogError("FaceAnimation: animState(AnimationState)가 null입니다!");
            return;
        }

        // 애니메이션 설정
        var entry = animState.SetAnimation(1, animName, true);

        // 3. entry가 null인지 체크 (존재하지 않는 애니메이션 이름일 때 발생)
        if (entry == null)
        {
            Debug.LogError($"FaceAnimation 실패: '{animName}' 이름의 애니메이션을 Spine 스켈레톤에서 찾을 수 없습니다.");
            return;
        }

        // 여기까지 무사히 와야 에러 없이 실행됨
        entry.MixDuration = 0.1f;

        prevFaceAnim = currentFaceAnim;
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
    /// ǥ�� �ִϸ��̼��� �����ϰ� ���̽� ������ ǥ������ ���ư��ϴ�.
    /// </summary>
    public void ClearFaceAnimation()
    {
        animState.ClearTrack(1);
        currentFaceAnim = string.Empty;
    }

    /// <summary>
    /// 이전 얼굴 애니메이션을 재생합니다.
    /// </summary>
    public void ReplayPrevFaceAnimation()
    {
        if (!string.IsNullOrEmpty(prevFaceAnim))
        {
            SetFaceAnimation(prevFaceAnim);
        }
    }

    /// <summary>
    /// 이전 몸 애니메이션을 재생합니다.
    /// </summary>
    public void ReplayPrevBodyAnimation()
    {
        if (!string.IsNullOrEmpty(prevBodyAnim))
        {
            SetBodyAnimation(prevBodyAnim);
        }
    }

}