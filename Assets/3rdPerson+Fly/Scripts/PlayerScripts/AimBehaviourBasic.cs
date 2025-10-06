using UnityEngine;
using System.Collections;

// AimBehaviour inherits from GenericBehaviour. This class corresponds to aim and strafe behaviour.
public class AimBehaviourBasic : GenericBehaviour
{
	// =========================
	// Projectile spawning
	// =========================
	public GameObject bulletPrefab;          // Bullet prefab (should have a Rigidbody)
	public float bulletSpeed = 80f;          // Initial speed (m/s)
	public float maxAimDistance = 1000f;     // Max distance for crosshair ray
	public LayerMask aimMask;                // Raycast mask for aiming (exclude Player)

	// Cached camera used for crosshair ray
	private Camera cam;
	[Header("Audio")]
	public AudioSource sfxSource;          // 可选：指定一个AudioSource来播枪声（推荐挂在武器或玩家上）
	public AudioClip fireSfx;              // 枪声音频
	[Range(0f, 1f)] public float fireSfxVolume = 1f;
	[Tooltip("给每发子弹加一点音高随机，避免完全重复的听感")]
	[Range(0f, 0.3f)] public float firePitchJitter = 0.06f;
	// =========================
	// Aim/camera settings
	// =========================
	public string aimButton = "Aim", shoulderButton = "Aim Shoulder"; // Input axes
	public Texture2D crosshair;                                       // Crosshair texture
	public float aimTurnSmoothing = 0.15f;
	[Header("Aim Yaw Offset")]
	[Tooltip("瞄准时给角色相对相机的偏航角（度）。负值=向左偏一点，正值=向右。")]
	public float aimYawOffsetDegrees = -8f;

	[Tooltip("偏航角是否跟随左右肩。开启后会根据 aimCamOffset.x 方向自动取左右。")]
	public bool yawOffsetFollowsShoulder = true;
	// Yaw smoothing while aiming
	public Vector3 aimPivotOffset = new Vector3(0.5f, 1.2f, 0f);      // Camera pivot offset in aim
	public Vector3 aimCamOffset = new Vector3(0f, 0.4f, -0.7f);       // Camera local offset in aim
	public float blendSpeed = 8f;                                     // Chest layer blend speed

	// =========================
	// Firing input & timing
	// =========================
	public string fireButton = "Fire1";       // Left mouse (Old Input System)
	public string fireTrigger = "Fire";       // Animator Trigger used by both Arms/Chest layers
	public bool onlyWhenAiming = true;        // Fire only when aiming
	public float fireRate = 0.12f;            // Fire rate (seconds/shot)

	// =========================
	// Muzzle VFX
	// =========================
	public Transform muzzle;                  // Muzzle transform
	public GameObject gunEffectPrefab;        // Muzzle flash/effect prefab

	// =========================
	// Animator layers/state names
	// =========================
	public string armsLayerName = "ArmsOverride";     // Layer that drives arm recoil
	public string fireStateName = "Firing Rifle";     // Fire state name on Arms layer
	public string chestLayerNameInAnimator = "ChestOverride"; // (Only for clarity in Inspector)
	public string chestFireStateName = "Firing Rifle";        // Fire state name on Chest layer (can be same)

	// =========================
	// IK LookAt (Chest only)
	// =========================
	[Header("Chest-only LookAt (enable IK Pass on the layer)")]
	public float lookAtBodyWeight = 0.9f;    // How much torso follows the aim (0..1)
	public float lookAtHeadWeight = 0.0f;    // Keep head still (use 0..0.2 if you want slight head motion)
	public float lookAtEyesWeight = 0f;      // Eyes (Humanoid only)
	public float lookAtClampWeight = 0.6f;   // Clamp to avoid extreme twisting

	// =========================
	// Internals
	// =========================
	private int fireTriggerHash;
	private float nextFireTime;

	private int aimBool;             // Animator bool "Aim"
	private bool aim;                // Are we currently aiming?
	private int chestLayer;          // Index of ChestOverride layer (also used for weighting)
	private int armsLayer;           // Index of ArmsOverride layer
	private Animator anim;
	private bool isFiring;           // Lock: true while any fire animation is playing

	void Start()
	{
		// Animator references
		aimBool = Animator.StringToHash("Aim");
		anim = GetComponent<Animator>();

		// Get layer indices
		chestLayer = anim.GetLayerIndex(chestLayerNameInAnimator); // Use the real name in Animator
		armsLayer = anim.GetLayerIndex(armsLayerName);

		// Trigger hash & timers
		fireTriggerHash = Animator.StringToHash(fireTrigger);
		nextFireTime = 0f;

		// Camera cache
		cam = behaviourManager.playerCamera.GetComponentInChildren<Camera>();
		if (!cam) cam = Camera.main;
	}

	void Update()
	{
		// === Aim on/off (prevent turning off while firing) ===
		if (Input.GetAxisRaw(aimButton) != 0 && !aim)
		{
			StartCoroutine(ToggleAimOn());
		}
		else if (aim && Input.GetAxisRaw(aimButton) == 0 && !isFiring)
		{
			// Only allow leaving aim when NOT firing
			StartCoroutine(ToggleAimOff());
		}

		// No sprinting while aiming
		canSprint = !aim;

		// Shoulder switch
		if (aim && Input.GetButtonDown(shoulderButton))
		{
			aimCamOffset.x = -aimCamOffset.x;
			aimPivotOffset.x = -aimPivotOffset.x;
		}

		// Drive "Aim" bool and ChestOverride weight
		behaviourManager.GetAnim.SetBool(aimBool, aim);
		float target = aim ? 1f : 0f;
		float w = Mathf.MoveTowards(anim.GetLayerWeight(chestLayer), target, Time.deltaTime * blendSpeed);
		anim.SetLayerWeight(chestLayer, w);

		// === Firing input + fire-rate gate ===
		bool wantShoot = Input.GetButtonDown(fireButton);
		if (wantShoot && Time.time >= nextFireTime && (!onlyWhenAiming || aim))
		{
			ShootOnce();
		}
	}

	// Start aiming with a small delay
	private IEnumerator ToggleAimOn()
	{
		yield return new WaitForSeconds(0.05f);

		// If another behaviour is temporarily locking or overriding, abort
		if (behaviourManager.GetTempLockStatus(this.behaviourCode) || behaviourManager.IsOverriding(this))
			yield break;

		// Start aiming and override other behaviours
		aim = true;
		int signal = 1;
		aimCamOffset.x = Mathf.Abs(aimCamOffset.x) * signal;
		aimPivotOffset.x = Mathf.Abs(aimPivotOffset.x) * signal;
		yield return new WaitForSeconds(0.1f);
		behaviourManager.GetAnim.SetFloat(speedFloat, 0);
		behaviourManager.OverrideWithBehaviour(this);
	}

	// End aiming with a small delay (but only after firing is done)
	private IEnumerator ToggleAimOff()
	{
		// Safety: if a fire has just started, wait until it's done
		while (isFiring) yield return null;

		aim = false;
		yield return new WaitForSeconds(0.3f);
		behaviourManager.GetCamScript.ResetTargetOffsets();
		behaviourManager.GetCamScript.ResetMaxVerticalAngle();
		yield return new WaitForSeconds(0.05f);
		behaviourManager.RevokeOverridingBehaviour(this);
	}

	// Camera placement while aiming
	public override void LocalFixedUpdate()
	{
		if (aim)
			behaviourManager.GetCamScript.SetTargetOffsets(aimPivotOffset, aimCamOffset);
	}

	// LateUpdate for orientation
	public override void LocalLateUpdate()
	{
		AimManagement();
	}

	void AimManagement()
	{
		Rotating();
	}

	// Rotate the player to match camera heading while aiming (yaw only)
	void Rotating()
	{
		Vector3 forward = behaviourManager.playerCamera.TransformDirection(Vector3.forward);
		forward.y = 0.0f;
		forward = forward.normalized;

		// === 新增：计算瞄准时的偏航角 ===
		float yawBias = 0f;
		if (aim)
		{
			if (yawOffsetFollowsShoulder)
			{
				// 右肩(aimCamOffset.x>0)时让角色略向左（负角），左肩则相反
				float sign = Mathf.Sign(aimCamOffset.x);
				yawBias = -sign * Mathf.Abs(aimYawOffsetDegrees);
			}
			else
			{
				// 固定向左（负）或向右（正）
				yawBias = aimYawOffsetDegrees;
			}
		}

		Quaternion targetRotation = Quaternion.Euler(0, behaviourManager.GetCamScript.GetH + yawBias, 0);
		float minSpeed = Quaternion.Angle(transform.rotation, targetRotation) * aimTurnSmoothing;

		behaviourManager.SetLastDirection(forward);
		transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, minSpeed * Time.deltaTime);
	}

	// Single shot: trigger recoil on both layers, spawn bullet & VFX, then wait for both layers to finish
	private void ShootOnce()
	{
		isFiring = true;                                 // Lock aiming-off while firing
		nextFireTime = Time.time + fireRate;             // Fire-rate cooldown

		// Trigger "Fire" so recoil plays on both Arms/Chest layers
		anim.ResetTrigger(fireTriggerHash);
		anim.SetTrigger(fireTriggerHash);
		PlayFireSfx();
		// --- Compute aim point from crosshair (camera center) ---
		Vector3 aimPoint;
		bool hasAimPoint = GetAimPointFromCrosshair(out aimPoint);

		// Direction from muzzle to aim point (fallback to camera forward if no hit)
		Vector3 dir = hasAimPoint ? (aimPoint - muzzle.position).normalized
								  : (cam ? cam.transform.forward : transform.forward);

		// --- Near-obstacle safety check (avoid immediately hitting a wall near muzzle) ---
		if (Physics.Raycast(muzzle.position, dir, out RaycastHit blockHit, 0.1f, aimMask, QueryTriggerInteraction.Ignore))
		{
			aimPoint = blockHit.point;
			dir = (aimPoint - muzzle.position).normalized;
		}

		// --- Spawn bullet from the muzzle, flying toward aimPoint ---
		if (bulletPrefab && muzzle)
		{
			GameObject bullet = Instantiate(bulletPrefab, muzzle.position, Quaternion.LookRotation(dir));
			var rb = bullet.GetComponent<Rigidbody>();
			if (rb)
			{
				rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; // smoother trails on fast bullets
				rb.linearVelocity = dir * bulletSpeed; // Gravity will take over if enabled on Rigidbody
			}

			// Check if bullet is spawned inside a shield and mark it as outgoing
			ShieldAwareBulletSpawner shieldAware = GetComponent<ShieldAwareBulletSpawner>();
			if (shieldAware != null)
			{
				shieldAware.OnBulletSpawned(bullet, muzzle.position);
			}
		}

		// --- Muzzle VFX (purely visual) ---
		if (muzzle && gunEffectPrefab)
		{
			var fx = Instantiate(gunEffectPrefab, muzzle.position, muzzle.rotation, muzzle);
			var ps = fx.GetComponent<ParticleSystem>();
			if (ps != null)
			{
				ps.Play();
				Destroy(fx, ps.main.duration + ps.main.startLifetime.constantMax);
			}
			else Destroy(fx, 2f);
		}

		// Wait until BOTH ArmsOverride and ChestOverride finish their firing states
		StartCoroutine(WaitForFireEnd());
	}
	private void PlayFireSfx()
	{
		if (!fireSfx) return;

		if (sfxSource)
		{
			// 轻微随机音高，别让每次声效一模一样
			float basePitch = 1f + Random.Range(-firePitchJitter, firePitchJitter);
			float oldPitch = sfxSource.pitch;
			sfxSource.pitch = basePitch;
			sfxSource.PlayOneShot(fireSfx, fireSfxVolume);
			sfxSource.pitch = oldPitch;
		}
		else
		{
			// 没给AudioSource就临时在枪口位置播放一次（3D音效）
			Vector3 pos = muzzle ? muzzle.position : transform.position;
			AudioSource.PlayClipAtPoint(fireSfx, pos, fireSfxVolume);
		}
	}


	// Wait until BOTH layers have finished their fire states
	private IEnumerator WaitForFireEnd()
	{
		// Small timeout to allow transitions to enter fire states
		const float enterTimeout = 0.30f;

		// Wait for Arms layer to enter its fire state (if layer exists)
		if (LayerExists(armsLayer))
			yield return WaitForStateEnter(armsLayer, fireStateName, enterTimeout);

		// Wait for Chest layer to enter its fire state (if layer exists)
		if (LayerExists(chestLayer))
			yield return WaitForStateEnter(chestLayer, chestFireStateName, enterTimeout);

		// Now wait until BOTH states are finished (normalizedTime >= 0.99 or not in state anymore)
		// A safety timeout prevents infinite loops in case of bad transitions.
		const float safetyTimeout = 2.0f; // Adjust according to clip length if needed
		float elapsed = 0f;

		while (elapsed < safetyTimeout)
		{
			bool armsDone = !LayerExists(armsLayer) || IsStateDone(armsLayer, fireStateName);
			bool chestDone = !LayerExists(chestLayer) || IsStateDone(chestLayer, chestFireStateName);

			if (armsDone && chestDone) break;

			elapsed += Time.deltaTime;
			yield return null;
		}

		// Unlock: now we can leave aiming if the player releases RMB
		isFiring = false;
	}

	// Helper: wait until a specific layer enters a state (or timeout)
	private IEnumerator WaitForStateEnter(int layerIndex, string stateName, float timeout)
	{
		float t = 0f;
		while (t < timeout && !IsInState(layerIndex, stateName))
		{
			t += Time.deltaTime;
			yield return null;
		}
	}

	// Helper: is the animator currently in a given state on a given layer?
	private bool IsInState(int layerIndex, string stateName)
	{
		if (!LayerExists(layerIndex)) return false;
		return anim.GetCurrentAnimatorStateInfo(layerIndex).IsName(stateName);
	}

	// Helper: has that state's playback finished (or no longer in that state)?
	private bool IsStateDone(int layerIndex, string stateName, float normalizedThreshold = 0.99f)
	{
		if (!LayerExists(layerIndex)) return true;
		var info = anim.GetCurrentAnimatorStateInfo(layerIndex);
		if (!info.IsName(stateName)) return true;                 // Already left the state
		return info.normalizedTime >= normalizedThreshold;        // Played through
	}

	// Helper: layer index bounds check
	private bool LayerExists(int layerIndex)
	{
		return layerIndex >= 0 && layerIndex < anim.layerCount;
	}

	// Raycast from the screen center to find the crosshair aim point.
	// Returns true if something was hit, otherwise returns a far point in front of the camera.
	private bool GetAimPointFromCrosshair(out Vector3 aimPoint)
	{
		if (cam == null)
		{
			aimPoint = transform.position + transform.forward * maxAimDistance;
			return false;
		}

		aimPoint = cam.transform.position + cam.transform.forward * maxAimDistance;
		Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

		if (Physics.Raycast(ray, out RaycastHit hit, maxAimDistance, aimMask, QueryTriggerInteraction.Ignore))
		{
			aimPoint = hit.point;
			return true;
		}
		return false;
	}

	// IK: chest-only LookAt to follow the crosshair (no hand IK at all)
	void OnAnimatorIK(int layerIndex)
	{
		if (anim == null) return;

		if (!aim)
		{
			// Disable LookAt when not aiming
			anim.SetLookAtWeight(0f);
			return;
		}

		// Compute aim point from crosshair
		Vector3 aimPoint;
		if (!GetAimPointFromCrosshair(out aimPoint))
		{
			// Fallback far point
			aimPoint = (cam ? cam.transform.position + cam.transform.forward * 1000f
							: transform.position + transform.forward * 1000f);
		}

		// Chest (body) does most of the work; head/eyes are minimal or zero
		anim.SetLookAtWeight(lookAtBodyWeight, lookAtHeadWeight, lookAtEyesWeight, lookAtClampWeight, 0.5f);
		anim.SetLookAtPosition(aimPoint);
	}

	// Crosshair GUI
	void OnGUI()
	{
		if (!crosshair) return;

		float mag = behaviourManager.GetCamScript.GetCurrentPivotMagnitude(aimPivotOffset);
		if (mag < 0.05f)
		{
			GUI.DrawTexture(
				new Rect(
					Screen.width * 0.5f - crosshair.width * 0.5f,
					Screen.height * 0.5f - crosshair.height * 0.5f,
					crosshair.width, crosshair.height),
				crosshair);
		}
	}
}
