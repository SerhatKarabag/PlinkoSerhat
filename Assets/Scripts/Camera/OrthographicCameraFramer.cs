using UnityEngine;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class OrthographicCameraFramer : MonoBehaviour
{
    [Header("Reference Configuration")]
    [Tooltip("Orthographic size that looks correct on reference device (1080x1920 = 11.2)")]
    [SerializeField] private float referenceOrthoSize = 11.2f;

    [Tooltip("Reference aspect ratio (width/height). 1080x1920 portrait = 0.5625")]
    [SerializeField] private float referenceAspect = 0.5625f;

    [Header("Playfield Bounds (World Units)")]
    [Tooltip("Width of the playfield that must always be visible")]
    [SerializeField] private float playfieldWidth = 12.6f;

    [Tooltip("Height of the playfield that must always be visible")]
    [SerializeField] private float playfieldHeight = 22.4f;

    [Header("Optional Settings")]
    [Tooltip("Additional padding around the playfield (world units)")]
    [SerializeField] private float padding = 0f;

    [Tooltip("Center offset if your playfield isn't centered at origin")]
    [SerializeField] private Vector2 playfieldCenter = Vector2.zero;

    private Camera _camera;
    private int _lastScreenWidth;
    private int _lastScreenHeight;
    private float _lastOrthoSize;

    private float _targetOrthoSize;

    private void Awake()
    {
        _camera = GetComponent<Camera>();

        if (playfieldWidth <= 0f)
        {
            playfieldWidth = referenceOrthoSize * 2f * referenceAspect;
        }
        if (playfieldHeight <= 0f)
        {
            playfieldHeight = referenceOrthoSize * 2f;
        }

        UpdateCameraSize();
    }

    private void Start()
    {
        UpdateCameraPosition();
    }

    private void LateUpdate()
    {
        if (ScreenDimensionsChanged())
        {
            UpdateCameraSize();
        }
    }

    private bool ScreenDimensionsChanged()
    {
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            return true;
        }
        return false;
    }

    private void UpdateCameraSize()
    {
        if (_camera == null) return;

        float currentAspect = (float)Screen.width / Screen.height;

        _targetOrthoSize = CalculateRequiredOrthoSize(currentAspect);

        if (!Mathf.Approximately(_targetOrthoSize, _lastOrthoSize))
        {
            _camera.orthographicSize = _targetOrthoSize;
            _lastOrthoSize = _targetOrthoSize;

            #if UNITY_EDITOR
            Debug.Log($"[{nameof(OrthographicCameraFramer)}] Screen: {Screen.width}x{Screen.height}, " +
                      $"Aspect: {currentAspect:F4}, OrthoSize: {_targetOrthoSize:F2}");
            #endif
        }
    }

    private float CalculateRequiredOrthoSize(float currentAspect)
    {
        float effectiveWidth = playfieldWidth + (padding * 2f);
        float effectiveHeight = playfieldHeight + (padding * 2f);

        float orthoSizeForHeight = effectiveHeight / 2f;

        float orthoSizeForWidth = effectiveWidth / (2f * currentAspect);

        return Mathf.Max(orthoSizeForHeight, orthoSizeForWidth);
    }

    private void UpdateCameraPosition()
    {
        Vector3 pos = _camera.transform.position;
        pos.x = playfieldCenter.x;
        pos.y = playfieldCenter.y;
        _camera.transform.position = pos;
    }

    public void ForceUpdate()
    {
        _lastScreenWidth = 0;
        _lastScreenHeight = 0;
        UpdateCameraSize();
    }

    public Bounds GetCameraBounds()
    {
        float height = _camera.orthographicSize * 2f;
        float width = height * _camera.aspect;
        return new Bounds(
            new Vector3(playfieldCenter.x, playfieldCenter.y, 0f),
            new Vector3(width, height, 0f)
        );
    }

    public bool IsPointVisible(Vector3 worldPoint)
    {
        Bounds bounds = GetCameraBounds();
        return bounds.Contains(new Vector3(worldPoint.x, worldPoint.y, bounds.center.z));
    }
}
