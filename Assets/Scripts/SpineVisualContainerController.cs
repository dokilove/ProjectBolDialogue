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

    public float ExternalOffsetY { get; set; } = 0f; // Y offset applied by external scripts like Drag2DObject
    public float CurrentDepthScale { get { return depthScaleFactor; } }
    public float CurrentTargetOffsetY { get; private set; } = 0f; // The targetOffsetY set by SetDepth command

    void Awake()
    {
        if (modelController == null)
        {
            modelController = GetComponentInChildren<SpineDualLayerController>();
        }
        originalY = transform.localPosition.y; // Store original LOCAL Y of this container
    }

    // Public method to set the depth (scale and Y offset)
    public void SetDepth(float targetDepthScale, float targetOffsetY, int targetSortingOrder, float duration, Action onComplete = null)
    {
        if (activeDepthCoroutine != null) StopCoroutine(activeDepthCoroutine);
        CurrentTargetOffsetY = targetOffsetY; // Store the targetOffsetY
        activeDepthCoroutine = StartCoroutine(DepthRoutine(targetDepthScale, targetOffsetY, targetSortingOrder, duration, onComplete));
    }

    private IEnumerator DepthRoutine(float targetDepthScale, float targetOffsetY, int targetSortingOrder, float duration, Action onComplete)
    {
        // This script controls its own transform's scale and Y position
        Transform t = this.transform;
        
        // Get SkeletonRenderer from the child modelController for sortingOrder
        Renderer renderer = modelController != null ? modelController.skeletonAnimation.GetComponent<Renderer>() : null;

        if (renderer == null)
        {
            Debug.LogError("Renderer component not found on child SpineDualLayerController's skeletonAnimation object!");
            onComplete?.Invoke();
            yield break;
        }

        float startDepthScale = depthScaleFactor;
        float startPosY = t.localPosition.y; // Use localPosition
        int startSortingOrder = renderer.sortingOrder;
        float targetPosY = originalY + targetOffsetY + ExternalOffsetY; // Combine internal and external Y offsets

        if (duration <= 0f)
        {
            depthScaleFactor = targetDepthScale;
            t.localScale = new Vector3(targetDepthScale, targetDepthScale, t.localScale.z); // Apply uniform scale
            t.localPosition = new Vector3(t.localPosition.x, targetPosY, t.localPosition.z); // Use localPosition
            renderer.sortingOrder = targetSortingOrder;
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

            renderer.sortingOrder = Mathf.RoundToInt(Mathf.Lerp(startSortingOrder, targetSortingOrder, ratio));

            if (modelController != null) modelController.ApplyScale(); // Re-apply model's internal scale
            yield return null;
        }

        depthScaleFactor = targetDepthScale;
        t.localScale = new Vector3(depthScaleFactor, depthScaleFactor, t.localScale.z); // Apply uniform scale
        t.localPosition = new Vector3(t.localPosition.x, targetPosY, t.localPosition.z); // Use localPosition
        renderer.sortingOrder = targetSortingOrder;
        if (modelController != null) modelController.ApplyScale(); // Re-apply model's internal scale
        onComplete?.Invoke();
        activeDepthCoroutine = null;
    }
}
