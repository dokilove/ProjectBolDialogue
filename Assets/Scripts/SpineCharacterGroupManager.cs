using UnityEngine;
using System.Collections.Generic;
using System.Linq; // For .Where()

public class SpineCharacterGroupManager : MonoBehaviour
{
    public static SpineCharacterGroupManager Instance { get; private set; }

    [Header("관리할 캐릭터 목록")]
    public List<SpineVisualContainerController> characters = new List<SpineVisualContainerController>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    // target을 focus (색상, sortingOrder), 나머지는 unfocus (색상, sortingOrder)
    public void FocusOnly(SpineVisualContainerController target, int focusedSortingOrder = 100, int unfocusedSortingOrder = 0)
    {
        foreach (var c in characters)
        {
            if (c == null) continue;

            if (c == target)
            {
                if (c.modelController != null) c.modelController.Focus();
                c.SetDepth(1f, 0f, focusedSortingOrder, 0f, null); // Scale 1, Offset 0, Instant
            }
            else
            {
                if (c.modelController != null) c.modelController.Unfocus();
                c.SetDepth(1f, 0f, unfocusedSortingOrder, 0f, null); // Scale 1, Offset 0, Instant
            }
        }
    }

    // 모든 캐릭터 unfocus (색상, sortingOrder)
    public void UnfocusAll(int unfocusedSortingOrder = 0)
    {
        foreach (var c in characters)
        {
            if (c == null) continue;
            if (c.modelController != null) c.modelController.Unfocus();
            c.SetDepth(1f, 0f, unfocusedSortingOrder, 0f, null); // Scale 1, Offset 0, Instant
        }
    }

    // 모든 캐릭터 focus (색상, sortingOrder)
    public void FocusAll(int focusedSortingOrder = 100)
    {
        foreach (var c in characters)
        {
            if (c == null) continue;
            if (c.modelController != null) c.modelController.Focus();
            c.SetDepth(1f, 0f, focusedSortingOrder, 0f, null); // Scale 1, Offset 0, Instant
        }
    }
}