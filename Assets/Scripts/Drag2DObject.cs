using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class Drag2DObject : MonoBehaviour
{
    // 올바른 Input Actions 참조
    public InputActionReference pointAction;
    public InputActionReference clickAction;

    [Header("Reference Transform")]
    public Transform referenceTransform; // 참조할 GameObject의 Transform (null이면 자신의 위치 기준)

    [Header("Directional Bounds (Local Space)")]
    // 초기 로컬 위치(startLocalPosition)를 기준으로 허용되는 최대 이동 거리
    public float maxDistanceLeft = 3f;  // 왼쪽으로 최대 이동 거리 (X-)
    public float maxDistanceRight = 3f; // 오른쪽으로 최대 이동 거리 (X+)
    public float maxDistanceUp = 3f;    // 위쪽으로 최대 이동 거리 (Y+)
    public float maxDistanceDown = 3f;  // 아래쪽으로 최대 이동 거리 (Y-)

    [Header("Return Settings")]
    public float returnSpeed = 5f;

    [Header("Event Hooks")]
    public UnityEvent OnDragStartSuccess;
    public UnityEvent OnDragEndSuccess;

    private bool dragging = false;
    private bool isReturning = false;
    private Vector3 startLocalPosition;    // 오브젝트의 초기 시작 로컬 위치

    private Camera cam;
    private Vector3 localOffset; // 로컬 오프셋

    void Awake()
    {
        cam = Camera.main;
        // startLocalPosition은 Awake에서 자신의 현재 로컬 위치로 설정됩니다.
        // referenceTransform은 드래그 경계 및 복귀 위치 계산에 사용됩니다.
        startLocalPosition = transform.localPosition;
    }

    void OnEnable()
    {
        pointAction.action.Enable();
        clickAction.action.Enable();
        clickAction.action.performed += HandleClick;
    }

    void OnDisable()
    {
        pointAction.action.Disable();
        clickAction.action.Disable();
        clickAction.action.performed -= HandleClick;
    }

    private void HandleClick(InputAction.CallbackContext context)
    {
        if (context.ReadValue<float>() > 0.5f) // 버튼을 눌렀을 때
        {
            TryStartDrag();
        }
        else // 버튼을 뗐을 때
        {
            StartReturn();
        }
    }

    void TryStartDrag()
    {
        Vector2 mousePos = pointAction.action.ReadValue<Vector2>();
        float z = cam.WorldToScreenPoint(transform.position).z; // 오브젝트의 월드 Z 깊이
        Vector3 mouseWorldPos = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, z));

        // 마우스 월드 위치를 부모를 기준으로 한 로컬 위치로 변환
        Vector3 mouseLocalPos = transform.parent.InverseTransformPoint(mouseWorldPos);

        // OverlapPoint는 월드 좌표를 사용하므로, 현재 오브젝트의 월드 위치를 사용
        Collider2D hitCollider = Physics2D.OverlapPoint(mouseWorldPos);

        if (hitCollider != null && hitCollider.gameObject == this.gameObject)
        {
            dragging = true;
            isReturning = false;
            localOffset = transform.localPosition - mouseLocalPos; // 로컬 오프셋 계산

            OnDragStartSuccess?.Invoke();
        }
    }

    void StartReturn()
    {
        if (dragging)
            OnDragEndSuccess?.Invoke();

        dragging = false;

        Vector3 returnTargetLocalPosition = (referenceTransform != null && transform.parent != null && referenceTransform.parent == transform.parent)
                                            ? referenceTransform.localPosition
                                            : startLocalPosition;
        
        if (Vector3.Distance(transform.localPosition, returnTargetLocalPosition) > 0.01f)
        {
            isReturning = true;
        }
    }

    void Update()
    {
        if (dragging)
        {
            Vector2 mousePos = pointAction.action.ReadValue<Vector2>();
            float z = cam.WorldToScreenPoint(transform.position).z;
            Vector3 mouseWorldPos = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, z));
            Vector3 mouseLocalPos = transform.parent.InverseTransformPoint(mouseWorldPos);

            // 드래그 중인 임시 목표 로컬 위치
            Vector3 targetLocalPosition = mouseLocalPos + localOffset;

            // 2. 클램핑 경계 계산 (referenceTransform 또는 startLocalPosition 기준)
            Vector3 baseLocalPosition;
            if (referenceTransform != null && transform.parent != null && referenceTransform.parent == transform.parent)
            {
                baseLocalPosition = referenceTransform.localPosition;
            }
            else
            {
                baseLocalPosition = startLocalPosition;
            }
            
            float minX = baseLocalPosition.x - maxDistanceLeft;  // 왼쪽 경계 (X축 최소)
            float maxX = baseLocalPosition.x + maxDistanceRight; // 오른쪽 경계 (X축 최대)
            float minY = baseLocalPosition.y - maxDistanceDown;  // 아래쪽 경계 (Y축 최소)
            float maxY = baseLocalPosition.y + maxDistanceUp;    // 위쪽 경계 (Y축 최대)

            // 3. X, Y좌표를 경계 내에서 클램핑
            targetLocalPosition.x = Mathf.Clamp(targetLocalPosition.x, minX, maxX);
            targetLocalPosition.y = Mathf.Clamp(targetLocalPosition.y, minY, maxY);

            // 4. 오브젝트 로컬 위치 업데이트
            transform.localPosition = targetLocalPosition;
        }
        else if (isReturning)
        {
            // 원래 로컬 위치로 복귀
            Vector3 returnTargetLocalPosition = (referenceTransform != null && transform.parent != null && referenceTransform.parent == transform.parent)
                                                ? referenceTransform.localPosition
                                                : startLocalPosition;
            
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                returnTargetLocalPosition,
                Time.deltaTime * returnSpeed
            );

            if (Vector3.Distance(transform.localPosition, returnTargetLocalPosition) < 0.01f)
            {
                transform.localPosition = returnTargetLocalPosition;
                isReturning = false;
            }
        }
    }
}