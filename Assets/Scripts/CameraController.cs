using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CameraController : MonoBehaviour
{
    [Header("Перемещение камеры")]
    public float panSpeed = 20f;
    public float edgePanSize = 10f;
    public bool edgePanEnabled = true;

    [Header("Зум")]
    public float zoomSpeed = 5f;
    public float minZoom = 2f;
    public float maxZoom = 11f;
    public float targetZoom = 10f;
    public float zoomLerpSpeed = 5f;

    [Header("Перетаскивание мышью")]
    public float dragSpeed = 10f; 
    private Vector3 dragOrigin;
    private bool isDragging = false;

    [Header("Границы камеры")]
    public bool useBounds = true;
    public Vector2 boundsMin = new Vector2(-20, -15);
    public Vector2 boundsMax = new Vector2(20, 15);

    [Header("Блокировка зума для UI")]
    [SerializeField] private ScrollRect battleLogScrollRect;

    private Camera cam;
    private Vector3 lastMousePosition;

    void Start()
    {
        cam = GetComponent<Camera>();
        targetZoom = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        cam.orthographicSize = targetZoom;
        lastMousePosition = Input.mousePosition;
    }

    void Update()
    {

        HandleZoom();
        HandleMovement();
        HandleMouseDrag();
        ClampCameraPosition();
    }



    void HandleZoom()
    {
        // Если есть ScrollRect И курсор над ним - блокируем зум
        if (battleLogScrollRect != null && IsPointerOverScrollRect())
        {
            return;
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            targetZoom -= scroll * zoomSpeed;
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * 5f);
    }

    // Проверяет, находится ли курсор над конкретным ScrollRect
    private bool IsPointerOverScrollRect()
    {
        if (battleLogScrollRect == null) return false;

        // Получаем RectTransform ScrollRect'а
        RectTransform rectTransform = battleLogScrollRect.GetComponent<RectTransform>();

        // Конвертируем позицию мыши в локальные координаты RectTransform'а
        Vector2 localMousePosition = rectTransform.InverseTransformPoint(Input.mousePosition);

        // Проверяем, находится ли точка внутри прямоугольника
        return rectTransform.rect.Contains(localMousePosition);
    }

    void HandleMovement()
    {
        Vector3 move = Vector3.zero;

        // 1. Клавиатура (WASD/стрелки)
        float moveX = Input.GetAxis("Horizontal");
        float moveY = Input.GetAxis("Vertical");

        if (moveX != 0 || moveY != 0)
        {
            move.x = moveX * panSpeed * Time.deltaTime;
            move.y = moveY * panSpeed * Time.deltaTime;
        }
        // 2. Edge pan
        else if (edgePanEnabled)
        {
            Vector3 mousePos = Input.mousePosition;

            // Плавное изменение скорости в зависимости от расстояния до края
            if (mousePos.x <= edgePanSize)
            {
                float factor = 1f - (mousePos.x / edgePanSize);
                move.x = -panSpeed * factor * Time.deltaTime;
            }
            else if (mousePos.x >= Screen.width - edgePanSize)
            {
                float factor = (mousePos.x - (Screen.width - edgePanSize)) / edgePanSize;
                move.x = panSpeed * factor * Time.deltaTime;
            }

            if (mousePos.y <= edgePanSize)
            {
                float factor = 1f - (mousePos.y / edgePanSize);
                move.y = -panSpeed * factor * Time.deltaTime;
            }
            else if (mousePos.y >= Screen.height - edgePanSize)
            {
                float factor = (mousePos.y - (Screen.height - edgePanSize)) / edgePanSize;
                move.y = panSpeed * factor * Time.deltaTime;
            }
        }

        // Применяем движение, если есть
        if (move != Vector3.zero)
        {
            Vector3 newPosition = transform.position + move;

            if (useBounds)
            {
                newPosition = GetClampedPosition(newPosition);
            }

            transform.position = newPosition;
        }
    }

    void HandleMouseDrag()
    {
        // Начало перетаскивания
        if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
        {
            isDragging = true;
            dragOrigin = GetMouseWorldPosition();
        }

        // Во время перетаскивания
        if ((Input.GetMouseButton(1) || Input.GetMouseButton(2)) && isDragging)
        {
            Vector3 currentPos = GetMouseWorldPosition();
            Vector3 difference = dragOrigin - currentPos;

            Vector3 newPosition = transform.position + difference * dragSpeed;

            if (useBounds)
            {
                newPosition = GetClampedPosition(newPosition);
            }

            transform.position = newPosition;

            dragOrigin = GetMouseWorldPosition();
        }

        // Конец перетаскивания
        if (Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
        {
            isDragging = false;
        }
    }

    Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = -transform.position.z;
        return cam.ScreenToWorldPoint(mousePos);
    }

    Vector3 GetClampedPosition(Vector3 position)
    {
        float cameraHeight = cam.orthographicSize;
        float cameraWidth = cameraHeight * cam.aspect;

        float leftBound = boundsMin.x + cameraWidth;
        float rightBound = boundsMax.x - cameraWidth;
        float bottomBound = boundsMin.y + cameraHeight;
        float topBound = boundsMax.y - cameraHeight;

        // Защита от некорректных границ
        if (leftBound < rightBound)
            position.x = Mathf.Clamp(position.x, leftBound, rightBound);
        else
            position.x = (leftBound + rightBound) * 0.5f;

        if (bottomBound < topBound)
            position.y = Mathf.Clamp(position.y, bottomBound, topBound);
        else
            position.y = (bottomBound + topBound) * 0.5f;

        return position;
    }

    void ClampCameraPosition()
    {
        if (!useBounds) return;
        transform.position = GetClampedPosition(transform.position);
    }

    public void ZoomIn()
    {
        targetZoom = Mathf.Clamp(targetZoom - 2f, minZoom, maxZoom);
    }

    public void ZoomOut()
    {
        targetZoom = Mathf.Clamp(targetZoom + 2f, minZoom, maxZoom);
    }

    public void ResetCamera()
    {
        Vector3 targetPos = new Vector3(0, 0, transform.position.z);
        if (useBounds)
            targetPos = GetClampedPosition(targetPos);
        transform.position = targetPos;
        targetZoom = 10f;
    }

    public void FocusOnCriticalNode()
    {
        if (GameManager.Instance != null && GameManager.Instance.criticalNode != null)
        {
            Vector3 targetPos = GameManager.Instance.criticalNode.transform.position;
            targetPos.z = transform.position.z;

            if (useBounds)
                targetPos = GetClampedPosition(targetPos);

            transform.position = targetPos;
            targetZoom = 5f;
        }
    }
}