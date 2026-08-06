using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

public class CinemachineZoomController : MonoBehaviour
{
    private static CinemachineZoomController instance;
    public static CinemachineZoomController Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("[CinemachineZoomController]");
                instance = go.AddComponent<CinemachineZoomController>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    private class ZoomTracker
    {
        public CinemachineCamera vcam;
        public float startSize;
    }

    private class ZoomTask
    {
        public float targetSize;
        public float duration;
        public string vcamName;
        public EaseType easeType;
        public Action onComplete;
    }

    private Queue<ZoomTask> zoomQueue = new Queue<ZoomTask>();
    private bool isProcessingQueue = false;
    private Coroutine activeZoomCoroutine;
    private (float targetSize, string vcamName)? lastQueuedZoom = null;

    public void EnqueueZoom(float targetSize, float duration, string vcamName, EaseType easeType, Action onComplete)
    {
        lastQueuedZoom = (targetSize, vcamName);

        zoomQueue.Enqueue(new ZoomTask
        {
            targetSize = targetSize,
            duration = duration,
            vcamName = vcamName,
            easeType = easeType,
            onComplete = onComplete
        });

        if (!isProcessingQueue)
        {
            StartCoroutine(ProcessZoomQueue());
        }
    }

    private IEnumerator ProcessZoomQueue()
    {
        isProcessingQueue = true;

        while (zoomQueue.Count > 0)
        {
            var task = zoomQueue.Dequeue();
            activeZoomCoroutine = StartCoroutine(ZoomRoutine(task.targetSize, task.duration, task.vcamName, task.easeType));
            yield return activeZoomCoroutine;
            activeZoomCoroutine = null;
            task.onComplete?.Invoke();
        }

        isProcessingQueue = false;
        lastQueuedZoom = null;
    }

    private IEnumerator ZoomRoutine(float targetSize, float duration, string vcamName, EaseType easeType)
    {
        List<ZoomTracker> targets = GetTargetCameras(vcamName);

        if (targets.Count == 0)
        {
            // Fallback for main camera
            if (Camera.main != null && Camera.main.orthographic)
            {
                if (duration <= 0f)
                {
                    Camera.main.orthographicSize = targetSize;
                }
                else
                {
                    float startSize = Camera.main.orthographicSize;
                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float ratio = Mathf.Clamp01(elapsed / duration);
                        float easedRatio = CharacterRootController.EvaluateEase(easeType, ratio);
                        Camera.main.orthographicSize = Mathf.Lerp(startSize, targetSize, easedRatio);
                        yield return null;
                    }
                    Camera.main.orthographicSize = targetSize;
                }
            }
            yield break;
        }

        if (duration <= 0f)
        {
            foreach (var t in targets)
            {
                ApplyZoom(t.vcam, targetSize);
            }
            yield break;
        }

        float timeElapsed = 0f;
        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(timeElapsed / duration);
            float easedRatio = CharacterRootController.EvaluateEase(easeType, ratio);

            foreach (var t in targets)
            {
                if (t.vcam != null)
                {
                    float currentSize = Mathf.Lerp(t.startSize, targetSize, easedRatio);
                    ApplyZoom(t.vcam, currentSize);
                }
            }

            yield return null;
        }

        foreach (var t in targets)
        {
            if (t.vcam != null)
            {
                ApplyZoom(t.vcam, targetSize);
            }
        }
    }

    public void SkipAllZooms()
    {
        if (zoomQueue.Count == 0 && !isProcessingQueue) return;

        StopAllCoroutines();
        activeZoomCoroutine = null;
        zoomQueue.Clear();
        isProcessingQueue = false;

        if (lastQueuedZoom.HasValue)
        {
            var targets = GetTargetCameras(lastQueuedZoom.Value.vcamName);
            if (targets.Count > 0)
            {
                foreach (var t in targets)
                {
                    if (t.vcam != null) ApplyZoom(t.vcam, lastQueuedZoom.Value.targetSize);
                }
            }
            else if (Camera.main != null && Camera.main.orthographic)
            {
                Camera.main.orthographicSize = lastQueuedZoom.Value.targetSize;
            }
        }

        lastQueuedZoom = null;
    }

    private List<ZoomTracker> GetTargetCameras(string vcamName)
    {
        List<ZoomTracker> targets = new List<ZoomTracker>();
        var allVcams = GameObject.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var vcam in allVcams)
        {
            if (vcam == null) continue;
            bool isMatch = string.IsNullOrEmpty(vcamName) || vcam.gameObject.name.Equals(vcamName, StringComparison.OrdinalIgnoreCase);
            if (isMatch)
            {
                targets.Add(new ZoomTracker
                {
                    vcam = vcam,
                    startSize = vcam.Lens.OrthographicSize
                });
            }
        }
        return targets;
    }

    private void ApplyZoom(CinemachineCamera vcam, float size)
    {
        var lens = vcam.Lens;
        lens.OrthographicSize = size;
        vcam.Lens = lens;
    }
}
