using UnityEngine;
using System;
using System.Collections;
using Spine.Unity;

public class SpineVisualContainerController : MonoBehaviour
{
    [Header("Components")]
    public SpineDualLayerController modelController; // Reference to the actual Spine model

    // Fields for depth scale and Y offset
    private float depthScaleFactor = 1f;
    private float originalY; // Original Y position of this container (local)
    private Coroutine activeDepthCoroutine;
    // DepthRoutine이 보간 중일 때 최종 목표값 (스냅샷 정착용)
    private float pendingDepthScale = 1f;
    private int pendingSortingOrder;

    public float ExternalOffsetY { get; set; } = 0f; // Y offset applied by external scripts like Drag2DObject
    public float CurrentDepthScale { get { return depthScaleFactor; } }
    public float CurrentTargetOffsetY { get; private set; } = 0f; // The targetOffsetY set by SetDepth command

    private bool isInitialized = false;

    private void EnsureInitialized()
    {
        if (!isInitialized)
        {
            if (modelController == null)
            {
                modelController = GetComponentInChildren<SpineDualLayerController>();
            }
            originalY = transform.localPosition.y;
            pendingDepthScale = depthScaleFactor;
            var renderer = (modelController != null) ? modelController.GetComponent<MeshRenderer>() : null;
            pendingSortingOrder = renderer != null ? renderer.sortingOrder : 0;
            isInitialized = true;
        }
    }

    public float TargetPosY
    {
        get
        {
            EnsureInitialized();
            return originalY + CurrentTargetOffsetY + ExternalOffsetY;
        }
    }

    public void ResetToDefaultLocalTransform()
    {
        EnsureInitialized();
        transform.localPosition = new Vector3(0f, TargetPosY, transform.localPosition.z);
        transform.localRotation = Quaternion.identity;
    }

    public struct VisualState
    {
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
        public float DepthScaleFactor;
        public float TargetOffsetY;
        public float ExternalOffsetY;
        public int SortingOrder;
    }

    public VisualState CaptureState()
    {
        EnsureInitialized();
        var meshRenderer = (modelController != null) ? modelController.GetComponent<MeshRenderer>() : null;
        return new VisualState
        {
            LocalPosition = transform.localPosition,
            LocalRotation = transform.localRotation,
            LocalScale = transform.localScale,
            DepthScaleFactor = depthScaleFactor,
            TargetOffsetY = CurrentTargetOffsetY,
            ExternalOffsetY = ExternalOffsetY,
            SortingOrder = meshRenderer != null ? meshRenderer.sortingOrder : 0
        };
    }

    public void StopDepthAnimation()
    {
        if (activeDepthCoroutine != null)
        {
            StopCoroutine(activeDepthCoroutine);
            activeDepthCoroutine = null;
        }
    }

    /// <summary>
    /// 진행 중인 깊이/정렬 보간을 최종 목표값으로 즉시 정착시킵니다.
    /// 대사가 넘어가는 순간 스냅샷을 찍기 전에 호출하여, 보간 도중의 중간값이
    /// 백로그 상태로 저장되지 않게 합니다.
    /// </summary>
    public void FinishDepthAnimation()
    {
        if (activeDepthCoroutine == null) return;

        StopCoroutine(activeDepthCoroutine);
        activeDepthCoroutine = null;

        EnsureInitialized();
        depthScaleFactor = pendingDepthScale;
        transform.localScale = new Vector3(depthScaleFactor, depthScaleFactor, transform.localScale.z);
        transform.localPosition = new Vector3(transform.localPosition.x, TargetPosY, transform.localPosition.z);

        var meshRenderer = (modelController != null) ? modelController.GetComponent<MeshRenderer>() : null;
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = pendingSortingOrder;
        }

        if (modelController != null)
        {
            modelController.ApplyScale();
        }
    }

    public void RestoreState(VisualState state)
    {
        StopDepthAnimation();

        EnsureInitialized();
        transform.localPosition = state.LocalPosition;
        transform.localRotation = state.LocalRotation;
        transform.localScale = state.LocalScale;
        depthScaleFactor = state.DepthScaleFactor;
        CurrentTargetOffsetY = state.TargetOffsetY;
        ExternalOffsetY = state.ExternalOffsetY;
        pendingDepthScale = state.DepthScaleFactor;
        pendingSortingOrder = state.SortingOrder;

        var meshRenderer = (modelController != null) ? modelController.GetComponent<MeshRenderer>() : null;
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = state.SortingOrder;
        }

        if (modelController != null)
        {
            modelController.ApplyScale();
        }
    }

    void Awake()
    {
        EnsureInitialized();
    }

    public int CurrentSortingOrder
    {
        get
        {
            EnsureInitialized();
            var meshRenderer = (modelController != null) ? modelController.GetComponent<MeshRenderer>() : null;
            return meshRenderer != null ? meshRenderer.sortingOrder : 0;
        }
    }

    // Public method to set only the sorting order (preserving scale and Y offset)
    public void SetSortingOrder(int targetSortingOrder, float duration = 0f, Action onComplete = null)
    {
        EnsureInitialized();
        SetDepth(CurrentDepthScale, CurrentTargetOffsetY, targetSortingOrder, duration, onComplete);
    }

    public void SnapSortingOrder(int targetSortingOrder)
    {
        EnsureInitialized();
        if (activeDepthCoroutine != null)
        {
            StopCoroutine(activeDepthCoroutine);
            activeDepthCoroutine = null;
        }
        pendingSortingOrder = targetSortingOrder;
        var meshRenderer = (modelController != null) ? modelController.GetComponent<MeshRenderer>() : null;
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = targetSortingOrder;
        }
    }

    // Public method to set the depth (scale and Y offset)
    public void SetDepth(float targetDepthScale, float targetOffsetY, int targetSortingOrder, float duration, Action onComplete = null)
    {
        if (activeDepthCoroutine != null) StopCoroutine(activeDepthCoroutine);
        CurrentTargetOffsetY = targetOffsetY; // Store the targetOffsetY
        pendingDepthScale = targetDepthScale;
        pendingSortingOrder = targetSortingOrder;
        activeDepthCoroutine = StartCoroutine(DepthRoutine(targetDepthScale, targetOffsetY, targetSortingOrder, duration, onComplete));
    }

    private IEnumerator DepthRoutine(float targetDepthScale, float targetOffsetY, int targetSortingOrder, float duration, Action onComplete)
    {
        // This script controls its own transform's scale and Y position
        Transform t = this.transform;

        // Get the MeshRenderer from the modelController's GameObject, which controls the sorting order.
        var meshRenderer = (modelController != null)
            ? modelController.GetComponent<MeshRenderer>()
            : null;

        if (meshRenderer == null)
        {
            Debug.LogError("MeshRenderer component not found on the SpineDualLayerController's GameObject!");
            onComplete?.Invoke();
            yield break;
        }

        float startDepthScale = depthScaleFactor;
        float startPosY = t.localPosition.y; // Use localPosition
        int startSortingOrder = meshRenderer.sortingOrder;
        float targetPosY = originalY + targetOffsetY + ExternalOffsetY; // Combine internal and external Y offsets

        if (duration <= 0f)
        {
            depthScaleFactor = targetDepthScale;
            t.localScale = new Vector3(targetDepthScale, targetDepthScale, t.localScale.z); // Apply uniform scale
            t.localPosition = new Vector3(t.localPosition.x, targetPosY, t.localPosition.z); // Use localPosition
            meshRenderer.sortingOrder = targetSortingOrder;
            if (modelController != null) modelController.ApplyScale(); // Re-apply model's internal scale
            onComplete?.Invoke();
            activeDepthCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / duration);

            depthScaleFactor = Mathf.Lerp(startDepthScale, targetDepthScale, ratio);
            t.localScale = new Vector3(depthScaleFactor, depthScaleFactor, t.localScale.z); // Apply uniform scale

            float newPosY = Mathf.Lerp(startPosY, targetPosY, ratio);
            t.localPosition = new Vector3(t.localPosition.x, newPosY, t.localPosition.z); // Use localPosition

            meshRenderer.sortingOrder = Mathf.RoundToInt(Mathf.Lerp(startSortingOrder, targetSortingOrder, ratio));

            if (modelController != null) modelController.ApplyScale(); // Re-apply model's internal scale
            yield return null;
        }

        depthScaleFactor = targetDepthScale;
        t.localScale = new Vector3(depthScaleFactor, depthScaleFactor, t.localScale.z); // Apply uniform scale
        t.localPosition = new Vector3(t.localPosition.x, targetPosY, t.localPosition.z); // Use localPosition
        meshRenderer.sortingOrder = targetSortingOrder;
        if (modelController != null) modelController.ApplyScale(); // Re-apply model's internal scale
        onComplete?.Invoke();
        activeDepthCoroutine = null;
    }
}
