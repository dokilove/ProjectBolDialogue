using UnityEngine;
using Unity.Cinemachine;

public class CameraEnumManager : MonoBehaviour
{
    public enum CameraTarget
    {
        Fafnir = 0,
        Chona = 1,
        Janghwa = 2,
        Vargr = 3,
        Default = 4
    }

    [Header("카메라 배열 (Enum 순서와 일치: Fafnir, Chona, Janghwa, Vargr, Default)")]
    public CinemachineCamera[] vCams;

    [Header("우선순위 설정")]
    public int activePriority = 20;
    public int inactivePriority = 10;

    // 내부 공용 로직
    private void ApplySwitch(CameraTarget target)
    {
        int index = (int)target;

        if (index < 0 || index >= vCams.Length)
        {
            Debug.LogWarning($"카메라 인덱스 {index}가 배열 범위를 벗어났습니다.");
            return;
        }

        for (int i = 0; i < vCams.Length; i++)
        {
            if (vCams[i] != null)
            {
                vCams[i].Priority = (i == index) ? activePriority : inactivePriority;
            }
        }
    }

    // --- Scene Event에서 바로 보일 전용 함수들 ---

    public void SwitchToFafnir() => ApplySwitch(CameraTarget.Fafnir);
    public void SwitchToChona() => ApplySwitch(CameraTarget.Chona);
    public void SwitchToJanghwa() => ApplySwitch(CameraTarget.Janghwa);
    public void SwitchToVargr() => ApplySwitch(CameraTarget.Vargr);
    public void SwitchToDefault() => ApplySwitch(CameraTarget.Default);

    // 혹시 몰라 남겨두는 인덱스 방식
    public void SwitchByIndex(int index) => ApplySwitch((CameraTarget)index);
}