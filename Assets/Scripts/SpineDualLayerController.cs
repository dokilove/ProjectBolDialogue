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
        if (currentBodyAnim == animName) return;

        prevBodyAnim = currentBodyAnim; // Save previous animation
        animState.SetAnimation(0, animName, true);
        currentBodyAnim = animName;
    }

    /// <summary>
    /// ���� ����� ǥ�� �ִϸ��̼��� �����մϴ� (Track 1)
    /// </summary>
    public void SetFaceAnimation(string animName)
    {
        if (currentFaceAnim == animName) return;

        // Track 1�� �ִϸ��̼��� ����ϸ� Track 0�� �ִϸ��̼� �� 
        // ǥ���� ���õ� Ű�����Ӹ� �ǽð����� �����ϴ�.
        var entry = animState.SetAnimation(1, animName, true);

        // ǥ���� ��� �ͽ�(�ε巯�� ��ȯ) �ð��� ª�� �ִ� ���� ǥ�� ��ȭ�� �����մϴ�.
        entry.MixDuration = 0.1f;

        prevFaceAnim = currentFaceAnim; // Save previous animation
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