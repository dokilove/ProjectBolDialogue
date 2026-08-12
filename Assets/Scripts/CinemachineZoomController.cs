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
        public float startHalfWidth;
    }

    private class ZoomTask
    {
        public float targetHalfWidth;
        public float duration;
        public string vcamName;
        public EaseType easeType;
        public Action onComplete;
    }

    private Queue<ZoomTask> zoomQueue = new Queue<ZoomTask>();
    private bool isProcessingQueue = false;
    private Coroutine activeZoomCoroutine;
    private (float targetHalfWidth, string vcamName)? lastQueuedZoom = null;

    private Dictionary<CinemachineCamera, float> cameraTargetHalfWidths = new Dictionary<CinemachineCamera, float>();
    private float? mainCameraTargetHalfWidth = null;

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializeCameraTracking();
    }

    private void Update()
    {
        CheckScreenResolutionChange();
    }

    private void InitializeCameraTracking()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        float aspect = GetCurrentAspectRatio();

        // Remove destroyed camera references
        var keysToRemove = new List<CinemachineCamera>();
        foreach (var key in cameraTargetHalfWidths.Keys)
        {
            if (key == null) keysToRemove.Add(key);
        }
        foreach (var key in keysToRemove)
        {
            cameraTargetHalfWidths.Remove(key);
        }

        var allVcams = GameObject.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var vcam in allVcams)
        {
            if (vcam == null) continue;
            if (!cameraTargetHalfWidths.ContainsKey(vcam))
            {
                cameraTargetHalfWidths[vcam] = vcam.Lens.OrthographicSize * aspect;
            }
        }

        if (Camera.main != null && Camera.main.orthographic && !mainCameraTargetHalfWidth.HasValue)
        {
            mainCameraTargetHalfWidth = Camera.main.orthographicSize * aspect;
        }
    }

    private void CheckScreenResolutionChange()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            ReapplyCurrentZoom();
        }
    }

    public static float GetCurrentAspectRatio()
    {
        float height = Screen.height;
        if (height <= 0f) return 16f / 9f;
        return (float)Screen.width / height;
    }

    public void EnqueueZoom(float targetHalfWidth, float duration, string vcamName, EaseType easeType, Action onComplete)
    {
        lastQueuedZoom = (targetHalfWidth, vcamName);

        zoomQueue.Enqueue(new ZoomTask
        {
            targetHalfWidth = targetHalfWidth,
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
            activeZoomCoroutine = StartCoroutine(ZoomRoutine(task.targetHalfWidth, task.duration, task.vcamName, task.easeType));
            yield return activeZoomCoroutine;
            activeZoomCoroutine = null;
            task.onComplete?.Invoke();
        }

        isProcessingQueue = false;
        lastQueuedZoom = null;
    }

    private IEnumerator ZoomRoutine(float targetHalfWidth, float duration, string vcamName, EaseType easeType)
    {
        List<ZoomTracker> targets = GetTargetCameras(vcamName);

        if (targets.Count == 0)
        {
            // Fallback for main camera
            if (Camera.main != null && Camera.main.orthographic)
            {
                mainCameraTargetHalfWidth = targetHalfWidth;
                if (duration <= 0f)
                {
                    ApplyZoomToMainCamera(targetHalfWidth);
                }
                else
                {
                    float aspect = GetCurrentAspectRatio();
                    float startHalfWidth = Camera.main.orthographicSize * aspect;
                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.deltaTime;
                        float ratio = Mathf.Clamp01(elapsed / duration);
                        float easedRatio = CharacterRootController.EvaluateEase(easeType, ratio);
                        float currentHalfWidth = Mathf.Lerp(startHalfWidth, targetHalfWidth, easedRatio);
                        ApplyZoomToMainCamera(currentHalfWidth);
                        yield return null;
                    }
                    ApplyZoomToMainCamera(targetHalfWidth);
                }
            }
            yield break;
        }

        foreach (var t in targets)
        {
            if (t.vcam != null)
            {
                cameraTargetHalfWidths[t.vcam] = targetHalfWidth;
            }
        }

        if (duration <= 0f)
        {
            foreach (var t in targets)
            {
                if (t.vcam != null)
                {
                    ApplyZoom(t.vcam, targetHalfWidth);
                }
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
                    float currentHalfWidth = Mathf.Lerp(t.startHalfWidth, targetHalfWidth, easedRatio);
                    ApplyZoom(t.vcam, currentHalfWidth);
                }
            }

            yield return null;
        }

        foreach (var t in targets)
        {
            if (t.vcam != null)
            {
                ApplyZoom(t.vcam, targetHalfWidth);
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
                    if (t.vcam != null)
                    {
                        cameraTargetHalfWidths[t.vcam] = lastQueuedZoom.Value.targetHalfWidth;
                        ApplyZoom(t.vcam, lastQueuedZoom.Value.targetHalfWidth);
                    }
                }
            }
            else if (Camera.main != null && Camera.main.orthographic)
            {
                mainCameraTargetHalfWidth = lastQueuedZoom.Value.targetHalfWidth;
                ApplyZoomToMainCamera(lastQueuedZoom.Value.targetHalfWidth);
            }
        }

        lastQueuedZoom = null;
    }

    private List<ZoomTracker> GetTargetCameras(string vcamName)
    {
        List<ZoomTracker> targets = new List<ZoomTracker>();
        float aspect = GetCurrentAspectRatio();
        var allVcams = GameObject.FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var vcam in allVcams)
        {
            if (vcam == null) continue;
            bool isMatch = string.IsNullOrEmpty(vcamName) || vcam.gameObject.name.Equals(vcamName, StringComparison.OrdinalIgnoreCase);
            if (isMatch)
            {
                float startHalfWidth = cameraTargetHalfWidths.TryGetValue(vcam, out float storedHalfWidth)
                    ? storedHalfWidth
                    : vcam.Lens.OrthographicSize * aspect;

                targets.Add(new ZoomTracker
                {
                    vcam = vcam,
                    startHalfWidth = startHalfWidth
                });
            }
        }
        return targets;
    }

    private void ApplyZoom(CinemachineCamera vcam, float targetHalfWidth)
    {
        if (vcam == null) return;
        float aspect = GetCurrentAspectRatio();
        var lens = vcam.Lens;
        lens.OrthographicSize = targetHalfWidth / aspect;
        vcam.Lens = lens;
    }

    private void ApplyZoomToMainCamera(float targetHalfWidth)
    {
        if (Camera.main != null && Camera.main.orthographic)
        {
            float aspect = GetCurrentAspectRatio();
            Camera.main.orthographicSize = targetHalfWidth / aspect;
        }
    }

    private void ReapplyCurrentZoom()
    {
        InitializeCameraTracking();

        foreach (var kvp in cameraTargetHalfWidths)
        {
            if (kvp.Key != null)
            {
                ApplyZoom(kvp.Key, kvp.Value);
            }
        }

        if (mainCameraTargetHalfWidth.HasValue)
        {
            ApplyZoomToMainCamera(mainCameraTargetHalfWidth.Value);
        }
    }
}
