using UnityEngine;

// FlyBehaviour inherits from GenericBehaviour. This class corresponds to the flying behaviour.
public class FlyBehaviour : GenericBehaviour
{
	public string flyButton = "Fly";              // Default fly button.
	public float flySpeed = 4.0f;                 // Default flying speed.
	public float sprintFactor = 2.0f;             // How much sprinting affects fly speed.
	public float flyMaxVerticalAngle = 60f;       // Angle to clamp camera vertical movement when flying.

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
		
		// Add a force player's rigidbody according to the fly direction.
		Vector3 direction = Rotating(horizontal, vertical);
		behaviourManager.GetRigidBody.AddForce(direction * (flySpeed * 100 * (behaviourManager.IsSprinting() ? sprintFactor : 1)), ForceMode.Acceleration);
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
}
