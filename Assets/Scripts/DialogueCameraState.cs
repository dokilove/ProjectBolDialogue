using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// 대사 엔트리가 시작되기 직전의 Cinemachine 카메라 상태 스냅샷.
/// 백로그로 되감을 때 캐릭터 복원 후 이 상태로 카메라를 되돌리고,
/// Dialogue System이 해당 대사의 카메라 시퀀스를 처음부터 다시 재생합니다.
/// </summary>
public sealed class DialogueCameraState
{
    private struct CameraEntry
    {
        public CinemachineCamera Camera;
        public bool ActiveSelf;
        public bool Enabled;
        public Vector3 Position;
        public Quaternion Rotation;
        public LensSettings Lens;
        public PrioritySettings Priority;
        public CameraTarget Target;
        public CinemachineCamera.StandbyUpdateMode StandbyUpdate;
    }

    private struct BrainEntry
    {
        public CinemachineBrain Brain;
        public CinemachineBlendDefinition DefaultBlend;
    }

    private readonly List<CameraEntry> cameras = new List<CameraEntry>();
    private readonly List<BrainEntry> brains = new List<BrainEntry>();

    private Camera outputCamera;
    private Vector3 outputPosition;
    private Quaternion outputRotation;
    private bool outputOrthographic;
    private float outputOrthographicSize;
    private float outputFieldOfView;

    private CinemachineCamera activeCamera;

    private static int rewindGeneration;

    /// <summary>
    /// 백로그 되감기를 시작할 때 호출합니다.
    /// 이전 시퀀스의 카메라 커맨드가 OnDestroy에서 현재 카메라 값을 덮어쓰지 않도록
    /// 세대 번호를 올리고, 진행 중인 줌 큐를 취소합니다.
    /// </summary>
    public static void BeginRewindCleanup()
    {
        rewindGeneration++;

        var zoom = CinemachineZoomController.InstanceOrNull;
        if (zoom != null)
        {
            zoom.CancelAllZooms();
        }
    }

    public static int RewindGeneration => rewindGeneration;

    public static DialogueCameraState Capture()
    {
        var result = new DialogueCameraState();

        // 비활성 카메라도 대상 대사에서 다시 활성화될 수 있으므로 함께 저장한다.
        var allCameras = Object.FindObjectsByType<CinemachineCamera>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < allCameras.Length; i++)
        {
            var camera = allCameras[i];
            if (camera == null) continue;

            result.cameras.Add(new CameraEntry
            {
                Camera = camera,
                ActiveSelf = camera.gameObject.activeSelf,
                Enabled = camera.enabled,
                Position = camera.transform.position,
                Rotation = camera.transform.rotation,
                Lens = camera.Lens,
                Priority = camera.Priority,
                Target = camera.Target,
                StandbyUpdate = camera.StandbyUpdate
            });
        }

        var allBrains = Object.FindObjectsByType<CinemachineBrain>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < allBrains.Length; i++)
        {
            var brain = allBrains[i];
            if (brain == null) continue;

            result.brains.Add(new BrainEntry
            {
                Brain = brain,
                DefaultBlend = brain.DefaultBlend
            });
        }

        var mainBrain = FindMainBrain();
        if (mainBrain != null &&
            mainBrain.ActiveVirtualCamera is CinemachineCamera active)
        {
            result.activeCamera = active;
        }

        result.outputCamera = (mainBrain != null) ? mainBrain.OutputCamera : Camera.main;
        if (result.outputCamera == null)
        {
            result.outputCamera = Camera.main;
        }

        if (result.outputCamera != null)
        {
            result.outputPosition = result.outputCamera.transform.position;
            result.outputRotation = result.outputCamera.transform.rotation;
            result.outputOrthographic = result.outputCamera.orthographic;
            result.outputOrthographicSize = result.outputCamera.orthographicSize;
            result.outputFieldOfView = result.outputCamera.fieldOfView;
        }

        return result;
    }

    public void Restore()
    {
        // 현재 대사의 줌 큐가 남아 있으면 먼저 중지한다.
        var zoom = CinemachineZoomController.InstanceOrNull;
        if (zoom != null)
        {
            zoom.CancelAllZooms();
        }

        for (int i = 0; i < cameras.Count; i++)
        {
            var entry = cameras[i];
            var camera = entry.Camera;
            if (camera == null) continue;

            if (entry.ActiveSelf && !camera.gameObject.activeSelf)
            {
                camera.gameObject.SetActive(true);
            }

            camera.transform.SetPositionAndRotation(entry.Position, entry.Rotation);
            camera.Lens = entry.Lens;
            camera.Target = entry.Target;
            camera.Priority = entry.Priority;
            camera.StandbyUpdate = entry.StandbyUpdate;
            camera.enabled = entry.Enabled;
            camera.PreviousStateIsValid = false;

            if (!entry.ActiveSelf)
            {
                camera.gameObject.SetActive(false);
            }
        }

        for (int i = 0; i < brains.Count; i++)
        {
            var entry = brains[i];
            if (entry.Brain != null)
            {
                entry.Brain.DefaultBlend = entry.DefaultBlend;
            }
        }

        var brain = FindMainBrain();
        var active = (activeCamera != null && activeCamera.isActiveAndEnabled)
            ? activeCamera
            : FindHighestPriorityCamera();

        if (active != null)
        {
            active.Prioritize();
            active.UpdateCameraState(Vector3.up, -1f);
        }

        if (brain != null)
        {
            brain.ActiveBlend = null;
            brain.ResetState();
        }

        if (active != null)
        {
            SnapOutputCamera(brain, active);
        }
        else
        {
            RestoreOutputCameraFallback();
        }

        if (zoom != null)
        {
            zoom.SyncTargetHalfWidthsFromCameras();
        }
    }

    private CinemachineCamera FindHighestPriorityCamera()
    {
        CinemachineCamera best = null;
        int bestPriority = int.MinValue;

        for (int i = 0; i < cameras.Count; i++)
        {
            var entry = cameras[i];
            var camera = entry.Camera;
            if (camera == null || !entry.ActiveSelf || !entry.Enabled) continue;

            int priority = entry.Priority.Value;
            if (priority > bestPriority)
            {
                bestPriority = priority;
                best = camera;
            }
        }

        return best;
    }

    private static CinemachineBrain FindMainBrain()
    {
        if (Camera.main != null)
        {
            var brain = Camera.main.GetComponent<CinemachineBrain>();
            if (brain != null) return brain;
        }

        return Object.FindAnyObjectByType<CinemachineBrain>();
    }

    private void SnapOutputCamera(CinemachineBrain brain, CinemachineCamera active)
    {
        Camera camera = (brain != null) ? brain.OutputCamera : Camera.main;
        if (camera == null) camera = outputCamera;
        if (camera == null) return;

        var state = active.State;
        camera.transform.SetPositionAndRotation(
            state.GetFinalPosition(),
            state.GetFinalOrientation());

        if (camera.orthographic)
        {
            camera.orthographicSize = state.Lens.OrthographicSize;
        }
        else
        {
            camera.fieldOfView = state.Lens.FieldOfView;
        }
    }

    private void RestoreOutputCameraFallback()
    {
        if (outputCamera == null) return;

        outputCamera.transform.SetPositionAndRotation(outputPosition, outputRotation);
        outputCamera.orthographic = outputOrthographic;

        if (outputOrthographic)
        {
            outputCamera.orthographicSize = outputOrthographicSize;
        }
        else
        {
            outputCamera.fieldOfView = outputFieldOfView;
        }
    }
}
