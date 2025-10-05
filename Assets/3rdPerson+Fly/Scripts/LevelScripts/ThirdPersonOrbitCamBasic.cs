using UnityEngine;

/// <summary>
/// Basic 3rd person orbit camera with simple collision handling.
/// 修复点：使用 hit.collider；忽略触发器；可配置遮挡层；修正变量名。
/// </summary>
public class ThirdPersonOrbitCamBasic : MonoBehaviour
{
	[Header("Target")]
	public Transform player;                                           // Player to follow

	[Header("Offsets")]
	public Vector3 pivotOffset = new Vector3(0.0f, 1.7f, 0.0f);        // Pivot (should carry all vertical offset)
	public Vector3 camOffset = new Vector3(0.0f, 0.0f, -3.0f);       // Local offset from pivot (x=side, z=back)

	[Header("Smoothing")]
	public float smooth = 10f;                                         // Position smoothing

	[Header("Orbit Speeds")]
	public float horizontalAimingSpeed = 6f;
	public float verticalAimingSpeed = 6f;

	[Header("Vertical Limits")]
	public float maxVerticalAngle = 30f;
	public float minVerticalAngle = -60f;

	[Header("Input Axes (gamepad)")]
	public string XAxis = "Analog X";
	public string YAxis = "Analog Y";

	[Header("Occlusion")]
	public LayerMask occluderLayers = ~0;                              // Which layers can occlude (default: all)
	public float collisionProbeRadius = 0.2f;                          // SphereCast radius
	public float collisionStep = 0.2f;                                 // Step when shrinking cam offset

	// --- internals ---
	private float angleH = 0f;
	private float angleV = 0f;
	private Transform cam;                                             // this.transform
	private Vector3 smoothPivotOffset;
	private Vector3 smoothCamOffset;
	private Vector3 targetPivotOffset;
	private Vector3 targetCamOffset;
	private float defaultFOV;
	private float targetFOV;
	private float targetMaxVerticalAngle;
	private bool isCustomOffset;

	// Public getter for current horizontal angle (used by character to face camera yaw)
	public float GetH => angleH;

	void Awake()
	{
		cam = transform;

		// Place camera at default pose relative to player
		if (player != null)
		{
			cam.position = player.position + Quaternion.identity * pivotOffset + Quaternion.identity * camOffset;
			cam.rotation = Quaternion.identity;
			angleH = player.eulerAngles.y;
		}

		smoothPivotOffset = pivotOffset;
		smoothCamOffset = camOffset;
		targetPivotOffset = pivotOffset;
		targetCamOffset = camOffset;

		var cameraComp = cam.GetComponent<Camera>();
		defaultFOV = cameraComp != null ? cameraComp.fieldOfView : 60f;
		targetFOV = defaultFOV;

		ResetMaxVerticalAngle();

		if (camOffset.y > 0f)
		{
			Debug.LogWarning(
				"Vertical Cam Offset (Y) is ignored during collisions. " +
				"建议把垂直高度放到 pivotOffset.y。");
		}
	}

	void Update()
	{
		if (player == null) return;

		// Mouse input
		angleH += Mathf.Clamp(Input.GetAxis("Mouse X"), -1f, 1f) * horizontalAimingSpeed;
		angleV += Mathf.Clamp(Input.GetAxis("Mouse Y"), -1f, 1f) * verticalAimingSpeed;

		// Gamepad input
		angleH += Mathf.Clamp(Input.GetAxis(XAxis), -1f, 1f) * 60f * horizontalAimingSpeed * Time.deltaTime;
		angleV += Mathf.Clamp(Input.GetAxis(YAxis), -1f, 1f) * 60f * verticalAimingSpeed * Time.deltaTime;

		// Clamp vertical
		angleV = Mathf.Clamp(angleV, minVerticalAngle, targetMaxVerticalAngle);

		// Compute orientations
		Quaternion camYRotation = Quaternion.Euler(0f, angleH, 0f);
		Quaternion aimRotation = Quaternion.Euler(-angleV, angleH, 0f);

		cam.rotation = aimRotation;

		// FOV
		var cameraComp = cam.GetComponent<Camera>();
		if (cameraComp)
			cameraComp.fieldOfView = Mathf.Lerp(cameraComp.fieldOfView, targetFOV, Time.deltaTime * 8f);

		// ----- Collision handling -----
		Vector3 basePos = player.position + camYRotation * targetPivotOffset;

		// Try to keep targetCamOffset; shrink until there is no occluder between cam and player
		Vector3 noCollisionOffset = targetCamOffset;
		while (noCollisionOffset.magnitude >= collisionStep)
		{
			if (DoubleViewingPosCheck(basePos + aimRotation * noCollisionOffset))
				break;

			noCollisionOffset -= noCollisionOffset.normalized * collisionStep;
		}
		if (noCollisionOffset.magnitude < collisionStep)
			noCollisionOffset = Vector3.zero;

		bool customOffsetCollision =
			isCustomOffset && noCollisionOffset.sqrMagnitude < targetCamOffset.sqrMagnitude;

		// Lerp to final pose
		smoothPivotOffset = Vector3.Lerp(
			smoothPivotOffset,
			customOffsetCollision ? pivotOffset : targetPivotOffset,
			smooth * Time.deltaTime);

		smoothCamOffset = Vector3.Lerp(
			smoothCamOffset,
			customOffsetCollision ? Vector3.zero : noCollisionOffset,
			smooth * Time.deltaTime);

		//cam.position =  player.position + camYRotation * smoothPivotOffset + aimRotation * smoothCamOffset;
		//add: camera shake
		var shakeComponent = player.gameObject.GetComponent<CameraShake0>();
		if (!shakeComponent)
		{
			Debug.LogError("No camera shake script attached to avatar!");
		}
		
		cam.position =  player.position + camYRotation * smoothPivotOffset + aimRotation * smoothCamOffset + shakeComponent.GetShakeOffset();
	}

	// --- API: offsets / FOV / limits ---
	public void SetTargetOffsets(Vector3 newPivotOffset, Vector3 newCamOffset)
	{
		targetPivotOffset = newPivotOffset;
		targetCamOffset = newCamOffset;
		isCustomOffset = true;
	}

	public void ResetTargetOffsets()
	{
		targetPivotOffset = pivotOffset;
		targetCamOffset = camOffset;
		isCustomOffset = false;
	}

	public void ResetYCamOffset()
	{
		targetCamOffset.y = camOffset.y;
	}

	public void SetYCamOffset(float y)
	{
		targetCamOffset.y = y;
	}

	public void SetXCamOffset(float x)
	{
		targetCamOffset.x = x;
	}

	public void SetFOV(float customFOV)
	{
		targetFOV = customFOV;
	}

	public void ResetFOV()
	{
		targetFOV = defaultFOV;
	}

	public void SetMaxVerticalAngle(float angle)
	{
		targetMaxVerticalAngle = angle;
	}

	public void ResetMaxVerticalAngle()
	{
		targetMaxVerticalAngle = maxVerticalAngle;
	}

	// --- Collision helpers ---
	// Concave surfaces may miss from outside: check both directions
	bool DoubleViewingPosCheck(Vector3 checkPos)
	{
		return ViewingPosCheck(checkPos) && ReverseViewingPosCheck(checkPos);
	}

	// From camera to player
	bool ViewingPosCheck(Vector3 checkPos)
	{
		Vector3 targetPos = player.position + pivotOffset;
		Vector3 dir = targetPos - checkPos;

		if (Physics.SphereCast(
				checkPos, collisionProbeRadius, dir, out RaycastHit hit, dir.magnitude,
				occluderLayers, QueryTriggerInteraction.Ignore))
		{
			// Use hit.collider (never null) and exclude player's own hierarchy
			var col = hit.collider;
			if (col && !col.transform.IsChildOf(player))
				return false;
		}
		return true;
	}

	// From player to camera
	bool ReverseViewingPosCheck(Vector3 checkPos)
	{
		Vector3 origin = player.position + pivotOffset;
		Vector3 dir = checkPos - origin;

		if (Physics.SphereCast(
				origin, collisionProbeRadius, dir, out RaycastHit hit, dir.magnitude,
				occluderLayers, QueryTriggerInteraction.Ignore))
		{
			var col = hit.collider;
			// Exclude player's own hierarchy and the camera transform itself
			if (col && !col.transform.IsChildOf(player) && col.transform != transform)
				return false;
		}
		return true;
	}

	// How far pivot is from desired (used by crosshair fade etc.)
	public float GetCurrentPivotMagnitude(Vector3 finalPivotOffset)
	{
		return Mathf.Abs((finalPivotOffset - smoothPivotOffset).magnitude);
	}
}
