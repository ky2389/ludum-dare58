using UnityEngine;

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

	private int flyBool;                          // Animator variable related to flying.
	private bool fly = false;                     // Boolean to determine whether or not the player activated fly mode.
	private CapsuleCollider col;                  // Reference to the player capsule collider.
	private EnergySystem energySystem;           // Reference to the energy system component.

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
		}

		// Assert this is the active behaviour
		fly = fly && behaviourManager.IsCurrentBehaviour(this.behaviourCode);

		// Set fly related variables on the Animator Controller.
		behaviourManager.GetAnim.SetBool(flyBool, fly);
	}

	// This function is called when another behaviour overrides the current one.
	public override void OnOverride()
	{
		// Ensure the collider will return to vertical position when behaviour is overriden.
		col.direction = 1;
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
			// Set collider direction to horizontal.
			col.direction = 2;
		}

		// Return the movement direction (horizontal only if camera vertical is disabled)
		return useCameraVertical ? targetDirection : new Vector3(targetDirection.x, 0f, targetDirection.z);
	}
}
