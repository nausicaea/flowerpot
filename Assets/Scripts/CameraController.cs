using UnityEngine;

namespace AssemblyCSharp
{
    /// <summary>
    /// Determines the behaviour of the camera.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        /// <summary>
        /// Determines the smalles valid field of view.
        /// </summary>
        [Range(1.0f, 90.0f)]
        public float minFov = 15.0f;
        /// <summary>
        /// Determines the largest valid field of view.
        /// </summary>
        [Range(1.0f, 90.0f)]
        public float maxFov = 90.0f;
        /// <summary>
        /// Selects the mouse button that activates camera rotation.
        /// </summary>
        public int mouseButton = 0;
        /// <summary>
        /// Specifies the mouse-wheel sensitivity.
        /// </summary>
        [Range(0.0f, 20.0f)]
        public float wheelSensitivity = 10.0f;
        /// <summary>
        /// Specifies the mouse sensitivity (x and y axes).
        /// </summary>
        [Range(0.001f, 1.0f)]
        public float mouseSensitivity = 1.0f;
        /// <summary>
        /// Determines the camera rotation speed.
        /// </summary>
        public float rotationSpeed = 10.0f;
        /// <summary>
        /// Determines the offset from the target's bounding box center
        /// on the basis of which the camera rotation center is chosen. 
        /// </summary>
        public Vector3 centerOffset;
        /// <summary>
        /// The target.
        /// </summary>
        public GameObject target;

        Camera cameraComponent;
        Renderer targetRenderer;
        bool mouseButtonDown;

        /// <summary>
        /// Obtains a reference to the <see cref="Camera"/> and the target <see cref="Renderer"/>.
        /// </summary>
        void Awake()
        {
            this.cameraComponent = this.GetComponentInChildren<Camera>();
            this.targetRenderer = this.target.GetComponent<Renderer>();
            this.mouseButtonDown = false;
        }

        /// <summary>
        /// Changes the <see cref="Camera"/> transform to look at the target object.
        /// </summary>
        void Start()
        {
            if (this.targetRenderer != null)
            {
                this.cameraComponent.transform.LookAt(this.targetRenderer.bounds.center + this.centerOffset);
            }
        }

        /// <summary>
        /// Handles camera zoom and rotation around the target object.
        /// </summary>
        void LateUpdate()
        {
            // Allow the player to zoom the camera.
            var fieldOfView = this.cameraComponent.fieldOfView;
            fieldOfView += Input.GetAxis("Mouse ScrollWheel") * this.wheelSensitivity;
            fieldOfView = Mathf.Clamp(fieldOfView, this.minFov, this.maxFov);
            this.cameraComponent.fieldOfView = fieldOfView;

            if (this.target != null)
            {
                // Determine if the mouse button is being held down.
                if (Input.GetMouseButtonDown(this.mouseButton))
                {
                    this.mouseButtonDown = true;
                }
                else if (Input.GetMouseButtonUp(this.mouseButton))
                {
                    this.mouseButtonDown = false;
                }

                // If the mouse button is being held down,
                // allow the player to rotate the camera around the target object.
                // The y-axis of the camera is restricted to the world y-axis.
                // If the mouse button is not being held down, slowly rotate
                // around the target.
                if (this.mouseButtonDown)
                {
                    this.cameraComponent.transform.RotateAround(
                        this.target.transform.position, 
                        Vector3.up, 
                        3.0f * this.mouseSensitivity * Input.GetAxis("Mouse X")
                    );
                    this.cameraComponent.transform.RotateAround(
                        this.target.transform.position, 
                        this.cameraComponent.transform.right, 
                        -3.0f * this.mouseSensitivity * Input.GetAxis("Mouse Y")
                    );
                }
                else
                {
                    this.cameraComponent.transform.RotateAround(
                        this.target.transform.position, 
                        Vector3.up, 
                        this.rotationSpeed * Time.deltaTime
                    );
                }
            }
        }
    }
}