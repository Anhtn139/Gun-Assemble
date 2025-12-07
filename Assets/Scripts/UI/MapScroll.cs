using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MapScroll : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera dùng để tính toạ độ (mặc định: Main Camera)")]
    [SerializeField] private Camera cam;
    [Tooltip("Transform sẽ di chuyển khi cuộn/drag. Nếu để trống sẽ dùng object này.")]
    [SerializeField] private Transform content;
    [Tooltip("SpriteRenderer của bản đồ (nếu map là SpriteRenderer trong scene). Nếu gán sẽ lấy bounds từ sprite lúc Start")]
    [SerializeField] private SpriteRenderer mapSprite;

    [Header("Auto bounds from Canvas UI")]
    [Tooltip("Nếu enabled sẽ tự động lấy bounds từ Canvas/Viewport UI khi Start")]
    [SerializeField] private bool autoSetBoundsFromCanvas = true;
    [Tooltip("Canvas dùng để lấy vùng hiển thị UI (nếu để trống sẽ tìm Canvas đầu tiên trong scene)")]
    [SerializeField] private Canvas uiCanvas;
    [Tooltip("RectTransform đại diện viewport trong Canvas (optional). Nếu để trống sẽ dùng Canvas RectTransform")]
    [SerializeField] private RectTransform viewportRect;

    [Header("Drag / Inertia")]
    [SerializeField] private float dragSpeed = 1f;
    [SerializeField] private bool useInertia = true;
    [SerializeField] [Range(0.1f, 50f)] private float decelerationRate = 10f; // lớn => dừng nhanh hơn
    [SerializeField] private float velocityThreshold = 5f;

    [Header("Bounds (world space)")]
    [Tooltip("Nếu enabled, vị trí target sẽ bị clamp vào worldBounds")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Rect worldBounds = new Rect(-10, -10, 20, 20);

    [Header("Zoom (mouse wheel / pinch)")]
    [Tooltip("Nếu camera là orthographic thì zoom sẽ thay đổi orthographicSize; nếu không, scale sẽ áp dụng cho content")]
    [SerializeField] private bool allowZoom = false;
    [SerializeField] private float zoomSpeed = 1f;
    [SerializeField] private float minZoom = 2f;
    [SerializeField] private float maxZoom = 20f;

    // runtime
    private bool _dragging;
    private Vector3 _lastPointerWorld;
    private Vector2 _velocity; // world units / s
    private Camera _cachedCam;
    private Transform _content;

    // movement target (can be content or mapSprite.transform)
    private Transform _moveTarget;

    // caching for other features (kept for compatibility)
    private Vector2 _lastScreenSize;

    // Additional storage to clamp correctly when target has a parent transform
    private bool _hasComputedLocalBounds = false;
    private Vector2 _targetMinLocal;
    private Vector2 _targetMaxLocal;

    // store sprite initial center and corner offsets so bounds calculation doesn't depend on sprite current pos
    private Vector2 _spriteOffsetMin = Vector2.zero;   // (sMin - center)
    private Vector2 _spriteOffsetMax = Vector2.zero;   // (sMax - center)
    private Vector3 _spriteInitialCenter = Vector3.zero;
    private bool _hasSpriteOffsets = false;

    void Awake()
    {
        _cachedCam = cam ? cam : Camera.main;
        _content = content ? content : transform;
        _moveTarget = _content; // default, may switch to mapSprite in Start
        if (_cachedCam == null)
        {
            Debug.LogWarning("MapScroll: Không tìm thấy Camera (Main Camera). Vui lòng gán camera hoặc đặt tag MainCamera.");
        }
    }

    // IMPORTANT: set bounds once at Start (sprite đã có sẵn trong scene)
    void Start()
    {
        _lastScreenSize = new Vector2(Screen.width, Screen.height);

        // if mapSprite assigned, prefer moving the sprite directly (map is separate object)
        if (mapSprite != null)
        {
            _moveTarget = mapSprite.transform;
            SetBoundsFromSprite(mapSprite);
            autoSetBoundsFromCanvas = false;
            return;
        }

        // fallback: use canvas viewport to compute world bounds
        if (autoSetBoundsFromCanvas)
        {
            if (uiCanvas == null)
            {
                uiCanvas = FindObjectOfType<Canvas>();
            }

            if (uiCanvas != null)
            {
                SetBoundsFromCanvas(uiCanvas, viewportRect);
            }
            else
            {
                Debug.LogWarning("MapScroll: autoSetBoundsFromCanvas bật nhưng không tìm thấy Canvas trong scene.");
            }

            autoSetBoundsFromCanvas = false;
        }
    }

    void Update()
    {
        HandleInput();
        if (!_dragging && useInertia)
        {
            ApplyInertia();
        }

        if (allowZoom)
        {
            HandleZoom();
        }
    }

    private void HandleInput()
    {
        // Touch (single-finger pan)
        if (Input.touchCount == 1)
        {
            Touch t = Input.GetTouch(0);
            Vector3 pointerWorld = ScreenToWorldPointAtTargetZ(t.position);

            if (t.phase == TouchPhase.Began)
            {
                BeginDrag(pointerWorld);
            }
            else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
            {
                ContinueDrag(pointerWorld);
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                EndDrag();
            }

            return;
        }

        // Touch pinch zoom (two fingers)
        if (allowZoom && Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            if (t0.phase == TouchPhase.Moved || t1.phase == TouchPhase.Moved)
            {
                float prevDist = (t0.position - t0.deltaPosition - (t1.position - t1.deltaPosition)).magnitude;
                float curDist = (t0.position - t1.position).magnitude;
                float delta = curDist - prevDist;
                ApplyZoom(delta * 0.01f * zoomSpeed);
            }
        }

        // Mouse
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 pointerWorld = ScreenToWorldPointAtTargetZ(Input.mousePosition);
            BeginDrag(pointerWorld);
        }
        else if (Input.GetMouseButton(0))
        {
            Vector3 pointerWorld = ScreenToWorldPointAtTargetZ(Input.mousePosition);
            ContinueDrag(pointerWorld);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            EndDrag();
        }

        // Mouse wheel zoom
        if (allowZoom)
        {
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.0001f)
            {
                ApplyZoom(-wheel * zoomSpeed);
            }
        }
    }

    private void BeginDrag(Vector3 pointerWorld)
    {
        _dragging = true;
        _lastPointerWorld = pointerWorld;
        _velocity = Vector2.zero;
    }

    private void ContinueDrag(Vector3 pointerWorld)
    {
        Vector3 deltaWorld = pointerWorld - _lastPointerWorld;
        // apply dragSpeed and move moveTarget (could be sprite or content)
        _moveTarget.position += deltaWorld * dragSpeed;

        // compute velocity (world units per second)
        if (Time.deltaTime > 0)
        {
            _velocity = (Vector2)(deltaWorld * dragSpeed) / Time.deltaTime;
        }

        _lastPointerWorld = pointerWorld;

        if (useBounds)
        {
            ClampToBounds();
        }
    }

    private void EndDrag()
    {
        _dragging = false;
        if (_velocity.magnitude < velocityThreshold)
        {
            _velocity = Vector2.zero;
        }
    }

    private void ApplyInertia()
    {
        if (_velocity.sqrMagnitude <= 0.000001f) return;

        Vector3 move = (Vector3)(_velocity * Time.deltaTime);
        _moveTarget.position += move;

        _velocity = Vector2.Lerp(_velocity, Vector2.zero, decelerationRate * Time.deltaTime);

        if (_velocity.magnitude < 0.1f)
        {
            _velocity = Vector2.zero;
        }

        if (useBounds)
        {
            bool changed = ClampToBounds();
            if (changed)
            {
                _velocity = Vector2.zero;
            }
        }
    }

    private void HandleZoom()
    {
        if (_cachedCam != null && _cachedCam.orthographic)
        {
            _cachedCam.orthographicSize = Mathf.Clamp(_cachedCam.orthographicSize, minZoom, maxZoom);
        }
        else
        {
            float s = Mathf.Clamp(_moveTarget.localScale.x, minZoom, maxZoom);
            _moveTarget.localScale = new Vector3(s, s, s);
        }

        if (useBounds)
        {
            ClampToBounds();
        }
    }

    private void ApplyZoom(float delta)
    {
        if (_cachedCam != null && _cachedCam.orthographic)
        {
            _cachedCam.orthographicSize = Mathf.Clamp(_cachedCam.orthographicSize + delta, minZoom, maxZoom);
        }
        else
        {
            float newScale = Mathf.Clamp(_moveTarget.localScale.x + delta, minZoom, maxZoom);
            _moveTarget.localScale = new Vector3(newScale, newScale, newScale);
        }
    }

    private Vector3 ScreenToWorldPointAtTargetZ(Vector2 screenPos)
    {
        if (_cachedCam == null) return new Vector3(screenPos.x, screenPos.y, 10f);
        // convert using move target's current Z to get consistent plane
        float z = _cachedCam.WorldToScreenPoint(_moveTarget.position).z;
        return _cachedCam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
    }

    private bool ClampToBounds()
    {
        if (!useBounds) return false;

        // If move target is the map sprite (separate object) compute clamping based on stored sprite offsets and viewport.
        if (mapSprite != null && _moveTarget == mapSprite.transform && _hasSpriteOffsets)
        {
            // get viewport world corners at sprite depth
            RectTransform rt = viewportRect != null ? viewportRect : (uiCanvas != null ? uiCanvas.GetComponent<RectTransform>() : null);
            if (rt == null) return SimpleClamp();

            Vector3[] viewportWorldAtSpriteDepth = GetViewportWorldCornersAtDepth(_spriteInitialCenter.z, rt);

            float vMinX = viewportWorldAtSpriteDepth[0].x;
            float vMaxX = viewportWorldAtSpriteDepth[2].x;
            float vMinY = viewportWorldAtSpriteDepth[0].y;
            float vMaxY = viewportWorldAtSpriteDepth[2].y;

            // allowed absolute sprite position ranges so viewport stays inside sprite AABB:
            float allowedPosMinX = vMaxX - _spriteOffsetMax.x;
            float allowedPosMaxX = vMinX - _spriteOffsetMin.x;
            float allowedPosMinY = vMaxY - _spriteOffsetMax.y;
            float allowedPosMaxY = vMinY - _spriteOffsetMin.y;

            // if inverted (sprite smaller than viewport) freeze to initial center
            if (allowedPosMinX > allowedPosMaxX)
            {
                allowedPosMinX = allowedPosMaxX = _spriteInitialCenter.x;
            }
            if (allowedPosMinY > allowedPosMaxY)
            {
                allowedPosMinY = allowedPosMaxY = _spriteInitialCenter.y;
            }

            Vector3 cur = mapSprite.transform.position;
            float newX = Mathf.Clamp(cur.x, allowedPosMinX, allowedPosMaxX);
            float newY = Mathf.Clamp(cur.y, allowedPosMinY, allowedPosMaxY);

            bool changed = !Mathf.Approximately(newX, cur.x) || !Mathf.Approximately(newY, cur.y);
            if (changed)
            {
                mapSprite.transform.position = new Vector3(newX, newY, cur.z);
            }
            return changed;
        }

        // if target has computed local bounds, clamp in local space
        if (_hasComputedLocalBounds && _moveTarget.parent != null)
        {
            Vector3 localPos = _moveTarget.localPosition;
            float newX = Mathf.Clamp(localPos.x, _targetMinLocal.x, _targetMaxLocal.x);
            float newY = Mathf.Clamp(localPos.y, _targetMinLocal.y, _targetMaxLocal.y);

            bool changed = !Mathf.Approximately(newX, localPos.x) || !Mathf.Approximately(newY, localPos.y);
            if (changed)
            {
                _moveTarget.localPosition = new Vector3(newX, newY, localPos.z);
            }
            return changed;
        }

        // fallback world-space clamp
        return SimpleClamp();
    }

    // simple clamp using worldBounds rect (applies to the move target)
    private bool SimpleClamp()
    {
        Vector3 pos = _moveTarget.position;
        float minX = worldBounds.xMin;
        float maxX = worldBounds.xMax;
        float minY = worldBounds.yMin;
        float maxY = worldBounds.yMax;

        float newX = Mathf.Clamp(pos.x, minX, maxX);
        float newY = Mathf.Clamp(pos.y, minY, maxY);

        bool changed = !Mathf.Approximately(newX, pos.x) || !Mathf.Approximately(newY, pos.y);

        pos.x = newX;
        pos.y = newY;
        _moveTarget.position = pos;

        return changed;
    }

    // helper: compute viewport rect world corners projected to the plane at targetWorldZ
    private Vector3[] GetViewportWorldCornersAtDepth(float targetWorldZ, RectTransform rt)
    {
        Vector3[] worldCorners = new Vector3[4];
        rt.GetWorldCorners(worldCorners); // corners in world-space (canvas plane)

        Vector3[] result = new Vector3[4];
        Camera canvasCam = (uiCanvas != null) ? uiCanvas.worldCamera : null;

        // compute screen-space z (distance) that corresponds to targetWorldZ in _cachedCam
        Vector3 reference = new Vector3(_moveTarget.position.x, _moveTarget.position.y, targetWorldZ);
        float screenZ = _cachedCam.WorldToScreenPoint(reference).z;

        for (int i = 0; i < 4; i++)
        {
            Vector2 screenPt;
            if (canvasCam != null)
            {
                screenPt = RectTransformUtility.WorldToScreenPoint(canvasCam, worldCorners[i]);
            }
            else
            {
                screenPt = new Vector2(worldCorners[i].x, worldCorners[i].y);
            }

            result[i] = _cachedCam.ScreenToWorldPoint(new Vector3(screenPt.x, screenPt.y, screenZ));
        }

        return result;
    }

    // Public helper: set bounds from a SpriteRenderer size (world units)
    // If move target is the sprite itself we produce allowed world ranges for the sprite position.
    // Otherwise we compute allowed ranges for the move target (content) so that viewport stays inside sprite.
    public void SetBoundsFromSprite(SpriteRenderer sr)
    {
        if (sr == null)
            return;

        if (_content == null)
            _content = content ? content : transform;

        // local sprite bounds (in sprite local space)
        Bounds localBounds = sr.sprite.bounds; // in sprite local units (relative to sprite pivot)
        Vector3[] localCorners = new Vector3[4];
        Vector3 min = localBounds.min;
        Vector3 max = localBounds.max;
        localCorners[0] = new Vector3(min.x, min.y, 0f); // bottom-left
        localCorners[1] = new Vector3(min.x, max.y, 0f); // top-left
        localCorners[2] = new Vector3(max.x, max.y, 0f); // top-right
        localCorners[3] = new Vector3(max.x, min.y, 0f); // bottom-right

        // transform local corners to world space using sprite transform (handles lossyScale and rotation)
        Vector3[] spriteWorldCorners = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            spriteWorldCorners[i] = sr.transform.TransformPoint(localCorners[i]);
        }

        // compute sprite world AABB from transformed corners
        float sMinX = spriteWorldCorners[0].x;
        float sMaxX = spriteWorldCorners[0].x;
        float sMinY = spriteWorldCorners[0].y;
        float sMaxY = spriteWorldCorners[0].y;
        for (int i = 1; i < 4; i++)
        {
            sMinX = Mathf.Min(sMinX, spriteWorldCorners[i].x);
            sMaxX = Mathf.Max(sMaxX, spriteWorldCorners[i].x);
            sMinY = Mathf.Min(sMinY, spriteWorldCorners[i].y);
            sMaxY = Mathf.Max(sMaxY, spriteWorldCorners[i].y);
        }

        // sprite center in world (respects pivot)
        Vector3 spriteLocalCenter = localBounds.center;
        Vector3 spriteWorldCenterPoint = sr.transform.TransformPoint(spriteLocalCenter);

        // store offsets from center so later clamping won't depend on sprite current position
        _spriteOffsetMin = new Vector2(sMinX - spriteWorldCenterPoint.x, sMinY - spriteWorldCenterPoint.y);
        _spriteOffsetMax = new Vector2(sMaxX - spriteWorldCenterPoint.x, sMaxY - spriteWorldCenterPoint.y);
        _spriteInitialCenter = spriteWorldCenterPoint;
        _hasSpriteOffsets = true;

        // get viewport world size using provided viewportRect (or canvas rect) projected to sprite depth
        RectTransform rt = viewportRect != null ? viewportRect : (uiCanvas != null ? uiCanvas.GetComponent<RectTransform>() : null);
        if (rt == null)
        {
            Debug.LogWarning("SetBoundsFromSprite: viewport RectTransform không được gán và không thể lấy từ Canvas.");
            return;
        }

        Vector3[] viewportWorldAtSpriteDepth = GetViewportWorldCornersAtDepth(spriteWorldCenterPoint.z, rt);
        float vMinX = viewportWorldAtSpriteDepth[0].x;
        float vMaxX = viewportWorldAtSpriteDepth[2].x;
        float vMinY = viewportWorldAtSpriteDepth[0].y;
        float vMaxY = viewportWorldAtSpriteDepth[2].y;
        Vector2 viewportSizeWorld = new Vector2(vMaxX - vMinX, vMaxY - vMinY);

        // allowed sprite center range so viewport stays inside sprite AABB
        float allowedSpriteCenterMinX = sMinX + viewportSizeWorld.x * 0.5f;
        float allowedSpriteCenterMaxX = sMaxX - viewportSizeWorld.x * 0.5f;
        float allowedSpriteCenterMinY = sMinY + viewportSizeWorld.y * 0.5f;
        float allowedSpriteCenterMaxY = sMaxY - viewportSizeWorld.y * 0.5f;

        // if sprite smaller than viewport on any axis, freeze center to sprite center
        if (allowedSpriteCenterMinX > allowedSpriteCenterMaxX)
        {
            allowedSpriteCenterMinX = allowedSpriteCenterMaxX = spriteWorldCenterPoint.x;
        }
        if (allowedSpriteCenterMinY > allowedSpriteCenterMaxY)
        {
            allowedSpriteCenterMinY = allowedSpriteCenterMaxY = spriteWorldCenterPoint.y;
        }

        // If we're moving the sprite itself, worldBounds are allowed sprite center positions
        if (_moveTarget == sr.transform)
        {
            float w = Mathf.Max(0f, allowedSpriteCenterMaxX - allowedSpriteCenterMinX);
            float h = Mathf.Max(0f, allowedSpriteCenterMaxY - allowedSpriteCenterMinY);
            worldBounds = new Rect(allowedSpriteCenterMinX, allowedSpriteCenterMinY, w, h);
            useBounds = true;

            // compute local-space equivalents for move target parent if needed
            if (_moveTarget.parent != null)
            {
                Vector3 worldMin = new Vector3(worldBounds.xMin, worldBounds.yMin, _moveTarget.position.z);
                Vector3 worldMax = new Vector3(worldBounds.xMax, worldBounds.yMax, _moveTarget.position.z);
                Transform parent = _moveTarget.parent;
                Vector3 localMin = parent.InverseTransformPoint(worldMin);
                Vector3 localMax = parent.InverseTransformPoint(worldMax);
                _targetMinLocal = new Vector2(Mathf.Min(localMin.x, localMax.x), Mathf.Min(localMin.y, localMax.y));
                _targetMaxLocal = new Vector2(Mathf.Max(localMin.x, localMax.x), Mathf.Max(localMin.y, localMax.y));
                _hasComputedLocalBounds = true;
            }
            else
            {
                _hasComputedLocalBounds = false;
            }

            return;
        }

        // Otherwise compute allowed ranges for the move target (content) so that viewport stays inside sprite.
        Vector3 contentPos = _moveTarget.position;
        Vector3 offset = spriteWorldCenterPoint - contentPos; // spriteCenter = contentPos + offset

        float contentMinX = allowedSpriteCenterMinX - offset.x;
        float contentMaxX = allowedSpriteCenterMaxX - offset.x;
        float contentMinY = allowedSpriteCenterMinY - offset.y;
        float contentMaxY = allowedSpriteCenterMaxY - offset.y;

        float ww = Mathf.Max(0f, contentMaxX - contentMinX);
        float hh = Mathf.Max(0f, contentMaxY - contentMinY);
        worldBounds = new Rect(contentMinX, contentMinY, ww, hh);
        useBounds = true;

        if (_moveTarget.parent != null)
        {
            Vector3 worldMin = new Vector3(worldBounds.xMin, worldBounds.yMin, _moveTarget.position.z);
            Vector3 worldMax = new Vector3(worldBounds.xMax, worldBounds.yMax, _moveTarget.position.z);
            Transform parent = _moveTarget.parent;
            Vector3 localMin = parent.InverseTransformPoint(worldMin);
            Vector3 localMax = parent.InverseTransformPoint(worldMax);
            _targetMinLocal = new Vector2(Mathf.Min(localMin.x, localMax.x), Mathf.Min(localMin.y, localMax.y));
            _targetMaxLocal = new Vector2(Mathf.Max(localMin.x, localMax.x), Mathf.Max(localMin.y, localMax.y));
            _hasComputedLocalBounds = true;
        }
        else
        {
            _hasComputedLocalBounds = false;
        }
    }

    // Set bounds from Canvas/viewport in world space (uses GetWorldCorners for robustness)
    public void SetBoundsFromCanvas(Canvas canvas, RectTransform viewport = null)
    {
        if (canvas == null)
        {
            Debug.LogWarning("SetBoundsFromCanvas: canvas null.");
            return;
        }

        RectTransform rt = viewport != null ? viewport : canvas.GetComponent<RectTransform>();
        if (rt == null)
        {
            Debug.LogWarning("SetBoundsFromCanvas: không tìm thấy RectTransform trên canvas/viewport.");
            return;
        }

        Vector3[] worldCorners = new Vector3[4];
        rt.GetWorldCorners(worldCorners); // 0 = bottom-left, 2 = top-right

        float minX = worldCorners[0].x;
        float maxX = worldCorners[2].x;
        float minY = worldCorners[0].y;
        float maxY = worldCorners[2].y;

        worldBounds = new Rect(minX, minY, maxX - minX, maxY - minY);
        useBounds = true;

        if (_moveTarget != null && _moveTarget.parent != null)
        {
            Vector3 worldMin = new Vector3(worldBounds.xMin, worldBounds.yMin, _moveTarget.position.z);
            Vector3 worldMax = new Vector3(worldBounds.xMax, worldBounds.yMax, _moveTarget.position.z);
            Transform parent = _moveTarget.parent;
            Vector3 localMin = parent.InverseTransformPoint(worldMin);
            Vector3 localMax = parent.InverseTransformPoint(worldMax);
            _targetMinLocal = new Vector2(Mathf.Min(localMin.x, localMax.x), Mathf.Min(localMin.y, localMax.y));
            _targetMaxLocal = new Vector2(Mathf.Max(localMin.x, localMax.x), Mathf.Max(localMin.y, localMax.y));
            _hasComputedLocalBounds = true;
        }
        else
        {
            _hasComputedLocalBounds = false;
        }
    }

    // Debug draw
    void OnDrawGizmosSelected()
    {
        if (useBounds)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(new Vector3(worldBounds.center.x, worldBounds.center.y, 0f),
                                new Vector3(worldBounds.size.x, worldBounds.size.y, 0.1f));
        }
    }
}
