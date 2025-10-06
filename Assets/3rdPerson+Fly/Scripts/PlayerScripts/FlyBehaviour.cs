using UnityEngine;
using System.Collections;  // <-- 为了淡入淡出协程

// FlyBehaviour inherits from GenericBehaviour. This class corresponds to the flying behaviour.
public class FlyBehaviour : GenericBehaviour
{
	public string flyButton = "Fly";              // Default fly button.
	public float flySpeed = 4.0f;                 // Default flying speed.
	public float sprintFactor = 2.0f;             // How much sprinting affects fly speed.
	public float flyMaxVerticalAngle = 60f;       // Angle to clamp camera vertical movement when flying.

	[Header("Vertical Flight Controls")]
	public bool useCameraVertical = true;             // Use camera pitch for vertical movement (original behavior)
	public bool useButtonVertical = true;             // Use buttons for vertical movement (new behavior)
	public KeyCode ascendKey = KeyCode.LeftControl;   // Key to ascend (rise up) - Ctrl
	public KeyCode descendKey = KeyCode.Space;        // Key to descend (go down) - Space
	public float verticalSpeed = 3.0f;               // Vertical movement speed for buttons
	public float cameraVerticalMultiplier = 1.0f;    // Multiplier for camera-based vertical movement

	[Header("Energy Integration")]
	public bool requiresEnergy = true;            // Whether flying requires energy
	public bool enableDebugLogs = true;          // Enable debug logging for energy consumption

	// ==== Audio ====
	[Header("Audio")]
	[Tooltip("循环播放的飞行音（循环环境/喷射声等）")]
	public AudioClip flyLoopClip;
	[Tooltip("起飞瞬间的短促音效（可选）")]
	public AudioClip takeoffClip;
	[Tooltip("落地/结束飞行的短促音效（可选）")]
	public AudioClip landClip;
	[Tooltip("用于播放飞行音的AudioSource，不指定则自动创建一个（2D）")]
	public AudioSource flyAudioSource;
	[Range(0f, 1f)] public float flyVolume = 0.6f;          // 循环音目标音量
	[Range(0f, 5f)] public float fadeInDuration = 0.6f;     // 起飞淡入
	[Range(0f, 5f)] public float fadeOutDuration = 0.4f;    // 落地淡出
	[Header("Audio Dynamics")]
	[Tooltip("基础音高")]
	[Range(0.5f, 2f)] public float basePitch = 1.0f;
	[Tooltip("冲刺时的附加音高")]
	[Range(0f, 1f)] public float sprintPitchBoost = 0.2f;
	[Tooltip("上升/下降对音高的影响（乘以|verticalInput|）")]
	[Range(0f, 0.5f)] public float verticalPitchBoost = 0.15f;
	[Tooltip("根据水平移动强度提升音量（0=不随动，1=完全随动）")]
	[Range(0f, 1f)] public float horizontalVolumeFollow = 0.4f;

	private int flyBool;                          // Animator variable related to flying.
	private bool fly = false;                     // Boolean to determine whether or not the player activated fly mode.
	private CapsuleCollider col;                  // Reference to the player capsule collider.
	private EnergySystem energySystem;           // Reference to the energy system component.

	// Audio internals
	private Coroutine volumeFadeRoutine;
	private float currentTargetVolume = 0f;

	// Start is always called after any Awake functions.
	void Start()
	{
		// Set up the references.
		flyBool = Animator.StringToHash("Fly");
		col = this.GetComponent<CapsuleCollider>();

		// Get energy system component if energy is required
		if (requiresEnergy)
		{
			energySystem = GetComponent<EnergySystem>();
			if (energySystem == null)
			{
				Debug.LogWarning($"[FlyBehaviour] EnergySystem component not found on {gameObject.name}. Flying will work without energy consumption.");
				requiresEnergy = false;
			}
			else if (enableDebugLogs)
			{
				Debug.Log($"[FlyBehaviour] Energy system integrated successfully.");
			}
		}

		// ==== Audio: 确保有一个可用的AudioSource（如果没手动指定的话）====
		EnsureFlyAudioSource();

		// Subscribe this behaviour on the manager.
		behaviourManager.SubscribeBehaviour(this);
	}

	// Update is used to set features regardless the active behaviour.
	void Update()
	{
		// Toggle fly by input, only if there is no overriding state or temporary transitions.
		if (Input.GetButtonDown(flyButton) && !behaviourManager.IsOverriding()
			&& !behaviourManager.GetTempLockStatus(behaviourManager.GetDefaultBehaviour))
		{
			// Check if we have enough energy to start flying
			if (!fly && requiresEnergy && energySystem != null && !energySystem.CanFly)
			{
				if (enableDebugLogs)
					Debug.LogWarning($"[FlyBehaviour] Cannot start flying - insufficient energy! ({energySystem.CurrentEnergy:F1}/{energySystem.MaxEnergy:F1})");
				return;
			}

			fly = !fly;

			// Force end jump transition.
			behaviourManager.UnlockTempBehaviour(behaviourManager.GetDefaultBehaviour);

			// Obey gravity. It's the law!
			behaviourManager.GetRigidBody.useGravity = !fly;

			// Player is flying.
			if (fly)
			{
				if (enableDebugLogs)
					Debug.Log($"[FlyBehaviour] Started flying. Energy: {(energySystem != null ? energySystem.CurrentEnergy.ToString("F1") : "N/A")}");

				// Register this behaviour.
				behaviourManager.RegisterBehaviour(this.behaviourCode);

				// ==== Audio: 起飞音 + 开启循环并淡入 ====
				PlayOneShotSafe(takeoffClip);
				StartFlyLoop();
			}
			else
			{
				if (enableDebugLogs)
					Debug.Log($"[FlyBehaviour] Stopped flying. Energy: {(energySystem != null ? energySystem.CurrentEnergy.ToString("F1") : "N/A")}");

				// Set collider direction to vertical.
				col.direction = 1;
				// Set camera default offset.
				behaviourManager.GetCamScript.ResetTargetOffsets();

				// Unregister this behaviour and set current behaviour to the default one.
				behaviourManager.UnregisterBehaviour(this.behaviourCode);

				// ==== Audio: 落地音 + 淡出循环 ====
				PlayOneShotSafe(landClip);
				StopFlyLoop();
			}
		}

		// Force stop flying if energy is depleted
		if (fly && requiresEnergy && energySystem != null && energySystem.IsEnergyDepleted)
		{
			if (enableDebugLogs)
				Debug.LogWarning($"[FlyBehaviour] Energy depleted! Forced landing.");

			fly = false;
			col.direction = 1;
			behaviourManager.GetCamScript.ResetTargetOffsets();
			behaviourManager.GetRigidBody.useGravity = true;
			behaviourManager.UnregisterBehaviour(this.behaviourCode);

			// ==== Audio: 落地音 + 淡出循环 ====
			PlayOneShotSafe(landClip);
			StopFlyLoop();
		}

		// Assert this is the active behaviour
		fly = fly && behaviourManager.IsCurrentBehaviour(this.behaviourCode);

		// Set fly related variables on the Animator Controller.
		behaviourManager.GetAnim.SetBool(flyBool, fly);

		// ==== Audio: 动态调节音量/音高（在 Update 里根据输入实时变化）====
		UpdateFlyLoopDynamics();
	}

	// This function is called when another behaviour overrides the current one.
	public override void OnOverride()
	{
		// Ensure the collider will return to vertical position when behaviour is overriden.
		col.direction = 1;

		// ==== Audio: 被覆盖时淡出 ====
		if (fly)
		{
			StopFlyLoop();
		}
	}

	// LocalFixedUpdate overrides the virtual function of the base class.
	public override void LocalFixedUpdate()
	{
		// Set camera limit angle related to fly mode.
		behaviourManager.GetCamScript.SetMaxVerticalAngle(flyMaxVerticalAngle);

		// Call the fly manager.
		FlyManagement(behaviourManager.GetH, behaviourManager.GetV);
	}

	// Deal with the player movement when flying.
	void FlyManagement(float horizontal, float vertical)
	{
		// Consume energy while flying
		if (requiresEnergy && energySystem != null)
		{
			bool isSprinting = behaviourManager.IsSprinting();
			bool energyConsumed;

			if (isSprinting)
			{
				energyConsumed = energySystem.ConsumeSprintFlyEnergy();
			}
			else
			{
				energyConsumed = energySystem.ConsumeFlyEnergy();
			}

			// If energy consumption failed, we can't fly effectively
			if (!energyConsumed)
			{
				// Reduce effectiveness when low on energy
				return;
			}
		}

		// Get horizontal movement direction (no Y component)
		Vector3 horizontalDirection = RotatingHorizontal(horizontal, vertical);

		// Get vertical movement input
		float verticalInput = GetVerticalInput();

		// Combine horizontal and vertical movement
		Vector3 totalDirection = horizontalDirection + Vector3.up * verticalInput;

		// Apply movement force
		float currentSpeed = flySpeed * (behaviourManager.IsSprinting() ? sprintFactor : 1);
		behaviourManager.GetRigidBody.AddForce(totalDirection * (currentSpeed * 100), ForceMode.Acceleration);
	}

	// Rotate the player to match correct orientation, according to camera and key pressed.
	Vector3 Rotating(float horizontal, float vertical)
	{
		Vector3 forward = behaviourManager.playerCamera.TransformDirection(Vector3.forward);
		// Camera forward Y component is relevant when flying.
		forward = forward.normalized;

		Vector3 right = new Vector3(forward.z, 0, -forward.x);

		// Calculate target direction based on camera forward and direction key.
		Vector3 targetDirection = forward * vertical + right * horizontal;

		// Rotate the player to the correct fly position.
		if ((behaviourManager.IsMoving() && targetDirection != Vector3.zero))
		{
			Quaternion targetRotation = Quaternion.LookRotation(targetDirection);

			Quaternion newRotation = Quaternion.Slerp(behaviourManager.GetRigidBody.rotation, targetRotation, behaviourManager.turnSmoothing);

			behaviourManager.GetRigidBody.MoveRotation(newRotation);
			behaviourManager.SetLastDirection(targetDirection);
		}

		// Player is flying and idle?
		if (!(Mathf.Abs(horizontal) > 0.2 || Mathf.Abs(vertical) > 0.2))
		{
			// Rotate the player to stand position.
			behaviourManager.Repositioning();
			// Set collider direction to vertical.
			col.direction = 1;
		}
		else
		{
			// Set collider direction to horizontal.
			col.direction = 2;
		}

		// Return the current fly direction.
		return targetDirection;
	}

	// Get vertical input for helicopter-style flying (combines camera and button input)
	private float GetVerticalInput()
	{
		float verticalInput = 0f;

		// Button-based vertical input
		if (useButtonVertical)
		{
			// Ascend (rise up) - Ctrl key
			if (Input.GetKey(ascendKey))
			{
				verticalInput += 1f;
			}

			// Descend (go down) - Space key
			if (Input.GetKey(descendKey))
			{
				verticalInput -= 1f;
			}

			// Apply vertical speed multiplier for buttons
			verticalInput *= verticalSpeed;
		}

		return verticalInput;
	}

	// Rotate the player for movement (supports both horizontal-only and full 3D movement)
	Vector3 RotatingHorizontal(float horizontal, float vertical)
	{
		// Get camera forward direction
		Vector3 forward = behaviourManager.playerCamera.TransformDirection(Vector3.forward);

		// If camera vertical movement is disabled, remove Y component
		if (!useCameraVertical)
		{
			forward.y = 0f; // Remove vertical component for helicopter-style flight
		}

		forward = forward.normalized;

		Vector3 right = new Vector3(forward.z, 0, -forward.x);

		// Calculate target direction based on camera forward and direction key
		Vector3 targetDirection = forward * vertical + right * horizontal;

		// Apply camera vertical multiplier if using camera vertical
		if (useCameraVertical)
		{
			targetDirection *= cameraVerticalMultiplier;
		}

		// Rotate the player to face movement direction (only if moving)
		if ((behaviourManager.IsMoving() && targetDirection != Vector3.zero))
		{
			// For horizontal-only mode, only rotate around Y axis
			Vector3 rotationDirection = useCameraVertical ? targetDirection : new Vector3(targetDirection.x, 0f, targetDirection.z);

			if (rotationDirection != Vector3.zero)
			{
				Quaternion targetRotation = Quaternion.LookRotation(rotationDirection);
				Quaternion newRotation = Quaternion.Slerp(behaviourManager.GetRigidBody.rotation, targetRotation, behaviourManager.turnSmoothing);
				behaviourManager.GetRigidBody.MoveRotation(newRotation);
				behaviourManager.SetLastDirection(rotationDirection);
			}
		}

		// Player is flying and idle?
		bool isIdleHorizontally = !(Mathf.Abs(horizontal) > 0.2 || Mathf.Abs(vertical) > 0.2);
		bool isIdleVertically = !useButtonVertical || Mathf.Abs(GetVerticalInput()) < 0.1f;

		if (isIdleHorizontally && isIdleVertically)
		{
			// Rotate the player to stand position.
			behaviourManager.Repositioning();
			// Set collider direction to vertical.
			col.direction = 1;
		}
		else
		{
			// Set collider direction to horizontal。
			col.direction = 2;
		}

		// Return the movement direction (horizontal only if camera vertical is disabled)
		return useCameraVertical ? targetDirection : new Vector3(targetDirection.x, 0f, targetDirection.z);
	}

	// ==== Audio helpers ====
	private void EnsureFlyAudioSource()
	{
		if (flyAudioSource != null) return;

		flyAudioSource = gameObject.AddComponent<AudioSource>();
		flyAudioSource.playOnAwake = false;
		flyAudioSource.loop = true;
		flyAudioSource.spatialBlend = 0f;         // 0=2D；如果想让飞行声有空间感，改成1并配置距离衰减
		flyAudioSource.rolloffMode = AudioRolloffMode.Linear;
		flyAudioSource.minDistance = 5f;
		flyAudioSource.maxDistance = 30f;
		flyAudioSource.volume = 0f;
		flyAudioSource.pitch = basePitch;
	}

	private void StartFlyLoop()
	{
		if (!flyLoopClip) return;
		if (!flyAudioSource) EnsureFlyAudioSource();

		flyAudioSource.clip = flyLoopClip;
		flyAudioSource.pitch = basePitch;
		flyAudioSource.volume = 0f;
		if (!flyAudioSource.isPlaying) flyAudioSource.Play();

		FadeVolumeTo(flyVolume, fadeInDuration);
	}

	private void StopFlyLoop()
	{
		if (!flyAudioSource) return;
		FadeVolumeTo(0f, fadeOutDuration, stopAfterFade: true);
	}

	private void UpdateFlyLoopDynamics()
	{
		if (!fly || !flyAudioSource || !flyAudioSource.isPlaying) return;

		// 水平输入强度（0~1），用来稍微抬一点音量
		float h = Mathf.Abs(behaviourManager.GetH);
		float v = Mathf.Abs(behaviourManager.GetV);
		float horizIntensity = Mathf.Clamp01((h + v) * 0.7f); // 简单合成

		// 垂直输入（按钮控制部分）
		float verticalInput = 0f;
		if (useButtonVertical)
		{
			if (Input.GetKey(ascendKey)) verticalInput += 1f;
			if (Input.GetKey(descendKey)) verticalInput -= 1f;
		}
		verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);

		// 动态音高：基础 + 冲刺 + 垂直分量
		float pitch = basePitch;
		if (behaviourManager.IsSprinting()) pitch += sprintPitchBoost;
		pitch += Mathf.Abs(verticalInput) * verticalPitchBoost;
		flyAudioSource.pitch = Mathf.Clamp(pitch, 0.1f, 3f);

		// 动态音量：在目标音量附近按水平强度微调
		float target = flyVolume * (1f - horizontalVolumeFollow) + flyVolume * horizontalVolumeFollow * Mathf.Lerp(0.6f, 1f, horizIntensity);

		// 如果当前正在做淡入淡出，则以淡入淡出的目标为准；否则平滑追随
		if (volumeFadeRoutine == null)
		{
			// 小范围平滑
			flyAudioSource.volume = Mathf.MoveTowards(flyAudioSource.volume, target, Time.deltaTime * 1.5f);
			currentTargetVolume = target;
		}
	}

	private void PlayOneShotSafe(AudioClip clip)
	{
		if (!clip) return;
		// 使用同一个source临时播一下不会打断loop，因为loop是同一source的话就用PlayOneShot
		if (flyAudioSource)
		{
			flyAudioSource.PlayOneShot(clip);
		}
		else
		{
			AudioSource.PlayClipAtPoint(clip, transform.position, 1f);
		}
	}

	private void FadeVolumeTo(float target, float duration, bool stopAfterFade = false)
	{
		if (volumeFadeRoutine != null) StopCoroutine(volumeFadeRoutine);
		volumeFadeRoutine = StartCoroutine(FadeVolumeRoutine(target, duration, stopAfterFade));
	}

	private IEnumerator FadeVolumeRoutine(float target, float duration, bool stopAfterFade)
	{
		if (!flyAudioSource) yield break;

		currentTargetVolume = target;
		float start = flyAudioSource.volume;
		float t = 0f;
		float dur = Mathf.Max(0.001f, duration);

		while (t < dur)
		{
			t += Time.deltaTime;
			float a = t / dur;
			flyAudioSource.volume = Mathf.Lerp(start, target, a);
			yield return null;
		}

		flyAudioSource.volume = target;
		volumeFadeRoutine = null;

		if (stopAfterFade && Mathf.Approximately(target, 0f))
		{
			flyAudioSource.Stop();
			flyAudioSource.clip = null;
		}
	}
}
