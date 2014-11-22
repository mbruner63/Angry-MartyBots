using UnityEngine;
using System.Collections;
#if UNITY_ANDROID && !UNITY_EDITOR
using tv.ouya.console.api;
#endif

public class playerController : MonoBehaviour {




	// Objects to drag in

	public Vector3 movementDirection;
	
	// Simpler motors might want to drive movement based on a target purely

	public Vector3 movementTarget;
	
	// The direction the character wants to face towards, in world space.

		public Vector3 facingDirection;

	//public  MovementMotor motor  ;
	public  Transform character;
	public GameObject cursorPrefab;
	public GameObject joystickPrefab;
	
	// Settings
	public float cameraSmoothing = 0.0f;
	public float cameraPreview = 2.0f;
	
	// Cursor settings
	public float cursorPlaneHeight = 0.0f;
	public float cursorFacingCamera  = 0.0f;
	public float cursorSmallerWithDistance  = 0.0f;
	public float cursorSmallerWhenClose  = 1.0f;
	
	// Private memeber data
	private  Camera mainCamera ;
	
	private Transform cursorObject;
	//private Joystick joystickLeft;
	//private Joystick joystickRight; 
	
	private Transform mainCameraTransform;
	private Vector3 cameraVelocity  = Vector3.zero;
	private Vector3 cameraOffset   = Vector3.zero;
	private Vector3 initOffsetToPlayer ;
	
	// Prepare a cursor point varibale. This is the mouse position on PC and controlled by the thumbstick on mobiles.
	private Vector3 cursorScreenPosition;
	
	private Plane playerMovementPlane;
	
	private GameObject joystickRightGO;
	
	private Quaternion screenMovementSpace;
	private Vector3 screenMovementForward;
	private Vector3 screenMovementRight;

	
	public float speed;
	private bool resetForce = false;
	public GameObject Pickups;
	public AudioClip resetSound;
	public AudioClip killSound;	
	public GameObject explosion;

	void Awake(){
		movementDirection = Vector2.zero;
		facingDirection = Vector2.zero;
		
		// Set main camera
		mainCamera = Camera.main;
		mainCameraTransform = mainCamera.transform;
		
		// Ensure we have character set
		// Default to using the transform this component is on
		if (!character)
			character = transform;
		
		initOffsetToPlayer = mainCameraTransform.position - character.position;
		

		
		// Save camera offset so we can use it in the first frame
		cameraOffset = mainCameraTransform.position - character.position;
		
		// Set the initial cursor position to the center of the screen
		cursorScreenPosition = new Vector3 (0.5f * Screen.width, 0.5f * Screen.height, 0.0f);
		
		// caching movement plane
		playerMovementPlane = new Plane (character.up, character.position + character.up * cursorPlaneHeight);

		}

	void Start()
	{
		#if UNITY_ANDROID && !UNITY_EDITOR
		WaitForOuyaSdk();
		#endif
		screenMovementSpace = Quaternion.Euler (0.0f, mainCameraTransform.eulerAngles.y, 0.0f);
		screenMovementForward = screenMovementSpace * Vector3.forward;
		screenMovementRight = screenMovementSpace * Vector3.right;	

		Debug.Log ("Martybots: setting shader LOD to ");
		
	}
	
	#if UNITY_ANDROID && !UNITY_EDITOR
	IEnumerable WaitForOuyaSdk()
	{
		while (!OuyaSDK.isIAPInitComplete())
		{
			yield return null;
		}
	}
	#endif
	
	
	
	void Update()
	{
		#if UNITY_ANDROID && !UNITY_EDITOR
		
		if(OuyaSDK.OuyaInput.GetButton(0, OuyaController.BUTTON_O)){
			resetForce = true;
			
		}
		
		#else
		if(Input.GetButtonDown("Jump")){
			resetForce = true;
		}
		#endif



		#if UNITY_ANDROID && !UNITY_EDITOR
		movementDirection = OuyaSDK.OuyaInput.GetAxis(0, OuyaController.AXIS_LS_X)* screenMovementRight+
			OuyaSDK.OuyaInput.GetAxis(0, OuyaController.AXIS_LS_Y)* screenMovementForward*-1.0f;
		#else
		float moveHorizontal = Input.GetAxis ("Horizontal");
		float moveVertical = Input.GetAxis ("Vertical");
		float forceMultiplier = speed * Time.deltaTime;

		movementDirection =  moveHorizontal * screenMovementRight + moveVertical * screenMovementForward;
		#endif
		
		// Make sure the direction vector doesn't exceed a length of 1
		// so the character can't move faster diagonally than horizontally or vertically
		if (movementDirection.sqrMagnitude > 1.0f)
			movementDirection.Normalize();
		
		
		// HANDLE CHARACTER FACING DIRECTION AND SCREEN FOCUS POINT
		
		// First update the camera position to take into account how much the character moved since last frame
		//mainCameraTransform.position = Vector3.Lerp (mainCameraTransform.position, character.position + cameraOffset, Time.deltaTime * 45.0f * deathSmoothoutMultiplier);
		
		// Set up the movement plane of the character, so screenpositions
		// can be converted into world positions on this plane
		//playerMovementPlane = new Plane (Vector3.up, character.position + character.up * cursorPlaneHeight);
		
		// optimization (instead of newing Plane):
		
		playerMovementPlane.normal = character.up;
		playerMovementPlane.distance = -character.position.y + cursorPlaneHeight;
		
		// used to adjust the camera based on cursor or joystick position
		
		Vector3 cameraAdjustmentVector = Vector3.zero;
		

		
		#if !UNITY_EDITOR && UNITY_ANDROID
		
		// On consoles use the analog sticks
		float axisX  = Input.GetAxis("LookHorizontal");
		float axisY  = Input.GetAxis("LookVertical");
		facingDirection  = OuyaSDK.OuyaInput.GetAxis(0, OuyaController.AXIS_RS_X)* screenMovementRight+
			OuyaSDK.OuyaInput.GetAxis(0, OuyaController.AXIS_RS_Y)* screenMovementForward*-1.0f;
		
		cameraAdjustmentVector = facingDirection;		
		
		#else
		
		// On PC, the cursor point is the mouse position
		Vector3 cursorScreenPosition  = Input.mousePosition;
		
		// Find out where the mouse ray intersects with the movement plane of the player
		Vector3 cursorWorldPosition   = ScreenPointToWorldPointOnPlane (cursorScreenPosition, playerMovementPlane, mainCamera);

		//Debug.Log ("Martybots: setting shader LOD to ");
		float halfWidth  = Screen.width / 2.0f;
		float halfHeight  = Screen.height / 2.0f;
		float maxHalf  = Mathf.Max (halfWidth, halfHeight);
		
		// Acquire the relative screen position			
		Vector3 posRel  = cursorScreenPosition - new Vector3 (halfWidth, halfHeight, cursorScreenPosition.z);		
		posRel.x /= maxHalf; 
		posRel.y /= maxHalf;
		
		cameraAdjustmentVector = posRel.x * screenMovementRight + posRel.y * screenMovementForward;
		cameraAdjustmentVector.y = 0.0f;	
		
		// The facing direction is the direction from the character to the cursor world position
		facingDirection = (cursorWorldPosition - character.position);
		facingDirection.y = 0.0f;			
		
		// Draw the cursor nicely
		HandleCursorAlignment (cursorWorldPosition);
		
		#endif
		

		
		// HANDLE CAMERA POSITION
		
		// Set the target position of the camera to point at the focus point
		Vector3 cameraTargetPosition = character.position + initOffsetToPlayer + cameraAdjustmentVector * cameraPreview;
		
		// Apply some smoothing to the camera movement
		mainCameraTransform.position = Vector3.SmoothDamp (mainCameraTransform.position, cameraTargetPosition, ref cameraVelocity, cameraSmoothing);
		
		// Save camera offset so we can use it in the next frame
		cameraOffset = mainCameraTransform.position - character.position;
	}
	
	public Vector3 PlaneRayIntersection (Plane plane  , Ray ray )   {
		float dist;
		plane.Raycast (ray, out dist);
		return ray.GetPoint (dist);
	}
	
	public  Vector3 ScreenPointToWorldPointOnPlane (Vector3 screenPoint ,Plane plane , Camera camera )  {
		// Set up a ray corresponding to the screen position
		Ray ray = camera.ScreenPointToRay (screenPoint);
		
		// Find out where the ray intersects with the plane
		return PlaneRayIntersection (plane, ray);
	}
	
	void HandleCursorAlignment ( Vector3 cursorWorldPosition ) {
		if (!cursorObject)
			return;
		
		// HANDLE CURSOR POSITION
		
		// Set the position of the cursor object
		cursorObject.position = cursorWorldPosition;
		
		#if !UNITY_FLASH
		// Hide mouse cursor when within screen area, since we're showing game cursor instead
		Screen.showCursor = (Input.mousePosition.x < 0 || Input.mousePosition.x > Screen.width || Input.mousePosition.y < 0 || Input.mousePosition.y > Screen.height);
		#endif
		
		
		// HANDLE CURSOR ROTATION
		
		Quaternion cursorWorldRotation  = cursorObject.rotation;
		if (facingDirection != Vector3.zero)
			cursorWorldRotation = Quaternion.LookRotation (facingDirection);
		
		// Calculate cursor billboard rotation
		Vector3 cursorScreenspaceDirection   = Input.mousePosition - mainCamera.WorldToScreenPoint (transform.position + character.up * cursorPlaneHeight);
		cursorScreenspaceDirection.z = 0.0f;
		Quaternion cursorBillboardRotation = mainCameraTransform.rotation * Quaternion.LookRotation (cursorScreenspaceDirection, -Vector3.forward);
		
		// Set cursor rotation
		cursorObject.rotation = Quaternion.Slerp (cursorWorldRotation, cursorBillboardRotation, cursorFacingCamera);
		
		
		// HANDLE CURSOR SCALING
		
		// The cursor is placed in the world so it gets smaller with perspective.
		// Scale it by the inverse of the distance to the camera plane to compensate for that.
		float compensatedScale  = 0.1f * Vector3.Dot (cursorWorldPosition - mainCameraTransform.position, mainCameraTransform.forward);
		
		// Make the cursor smaller when close to character
		float cursorScaleMultiplier  = Mathf.Lerp (0.7f, 1.0f, Mathf.InverseLerp (0.5f, 4.0f, facingDirection.magnitude));
		
		// Set the scale of the cursor
		cursorObject.localScale = Vector3.one * Mathf.Lerp (compensatedScale, 1, cursorSmallerWithDistance) * cursorScaleMultiplier;
		
		// DEBUG - REMOVE LATER
		if (Input.GetKey(KeyCode.O)) cursorFacingCamera += Time.deltaTime * 0.5f;
		if (Input.GetKey(KeyCode.P)) cursorFacingCamera -= Time.deltaTime * 0.5f;
		cursorFacingCamera = Mathf.Clamp01(cursorFacingCamera);
		
		if (Input.GetKey(KeyCode.K)) cursorSmallerWithDistance += Time.deltaTime * 0.5f;
		if (Input.GetKey(KeyCode.L)) cursorSmallerWithDistance -= Time.deltaTime * 0.5f;
		cursorSmallerWithDistance = Mathf.Clamp01(cursorSmallerWithDistance);
	}

	void LateUpdate(){

		//Vector3 myVector = new Vector3(0.0f,0.5f,0.0f);
		//transform.position = myVector;
	}


	//public var movement : MoveController;
	public float walkingSpeed  = 5.0f;
	public float walkingSnappyness  = 50.0f;
	public float turningSmoothing   = 0.3f;
	
	void FixedUpdate () {
		// Handle the movement of the character
		Vector3 targetVelocity = movementDirection * walkingSpeed;
		Vector3 deltaVelocity  = targetVelocity - rigidbody.velocity;
		if (rigidbody.useGravity)
			deltaVelocity.y = 0.0f;
		rigidbody.AddForce (deltaVelocity * walkingSnappyness, ForceMode.Acceleration);
		
		// Setup player to face facingDirection, or if that is zero, then the movementDirection
		Vector3 faceDir = facingDirection;
		if (faceDir == Vector3.zero)
			faceDir = movementDirection;
		
		// Make the character rotate towards the target rotation
		if (faceDir == Vector3.zero) {
			rigidbody.angularVelocity = Vector3.zero;
		}
		else {
			float rotationAngle  = AngleAroundAxis (transform.forward, faceDir, Vector3.up);
			rigidbody.angularVelocity = (Vector3.up * rotationAngle * turningSmoothing);
		}
	}
	
	// The angle between dirA and dirB around axis
	float AngleAroundAxis ( Vector3 dirA  , Vector3 dirB , Vector3 axis ) {
		// Project A and B onto the plane orthogonal target axis
		dirA = dirA - Vector3.Project (dirA, axis);
		dirB = dirB - Vector3.Project (dirB, axis);
		
		// Find (positive) angle between A and B
		float angle  = Vector3.Angle (dirA, dirB);
		
		// Return angle multiplied with 1 or -1
		return angle * (Vector3.Dot (axis, Vector3.Cross (dirA, dirB)) < 0 ? -1 : 1);
	}

	
	// Destroy everything that enters the trigger
	void OnTriggerEnter(Collider other) {
		/*if (other.gameObject.tag == "Pickup") {
			Instantiate(explosion,other.transform.position,other.transform.rotation);
			other.gameObject.SetActive(false);
			audio.PlayOneShot(killSound);
		}*/
	}
	
	
}
