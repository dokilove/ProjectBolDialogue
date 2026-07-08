using UnityEngine;
using System.Collections.Generic;

public class SpineCharacterGroupManager : MonoBehaviour
{
    public static SpineCharacterGroupManager Instance { get; private set; }

    [Header("씬의 모든 캐릭터 등록")]
    public List<SpineDualLayerController> characters = new List<SpineDualLayerController>();

    void Awake()
    {
        Instance = this;
    }

    // target만 focus, 나머지는 전부 unfocus
    public void FocusOnly(SpineDualLayerController target)
    {
        foreach (var c in characters)
        {
            if (c == null) continue;
            if (c == target) c.Focus();
            else c.Unfocus();
        }
    }

    // 전부 unfocus
    public void UnfocusAll()
    {
        foreach (var c in characters)
        {
            if (c != null) c.Unfocus();
        }
    }

    // 전부 focus (원래 색으로 복귀)
    public void FocusAll()
    {
        foreach (var c in characters)
        {
            if (c != null) c.Focus();
        }
    }
}