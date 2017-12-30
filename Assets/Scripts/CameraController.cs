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
        public float minFov = 15.0f;
        /// <summary>
        /// Determines the largest valid field of view.
        /// </summary>
        public float maxFov = 90.0f;
        /// <summary>
        /// Specifies the mouse-wheel sensitivity.
        /// </summary>
        public float wheelSensitivity = 10.0f;
        /// <summary>
        /// Determines the camera rotation speed.
        /// </summary>
        public float rotationSpeed = 10.0f;
        /// <summary>
        /// Determines the offset from the target's bounding box center
        /// on the basis of which the camera rotation center is chosen. 
        /// </summary>
        public Vector3 centerOffset = new Vector3(0.0f, -2.0f, 0.0f);
        /// <summary>
        /// The target.
        /// </summary>
        public GameObject target;

        Camera cameraComponent;
        Renderer targetRenderer;

        /// <summary>
        /// Obtains a reference to the <see cref="Camera"/>.
        /// </summary>
        void Awake()
        {
            this.cameraComponent = this.GetComponent<Camera>();
            this.targetRenderer = this.target.GetComponent<Renderer>();
        }

        /// <summary>
        /// Changes the <see cref="Camera"/> transform to look at the target object.
        /// </summary>
        void Start()
        {
            this.cameraComponent.transform.LookAt(this.targetRenderer.bounds.center + this.centerOffset);
        }

        /// <summary>
        /// Update this instance.
        /// </summary>
        void Update()
        {
            // Allow the player to zoom the camera.
            var fieldOfView = this.cameraComponent.fieldOfView;
            fieldOfView += Input.GetAxis("Mouse ScrollWheel") * this.wheelSensitivity;
            fieldOfView = Mathf.Clamp(fieldOfView, this.minFov, this.maxFov);
            this.cameraComponent.fieldOfView = fieldOfView;

            // Rotate around the target object.
            this.cameraComponent.transform.RotateAround(this.targetRenderer.bounds.center + this.centerOffset, Vector3.up, this.rotationSpeed * Time.deltaTime);

            // If the first mouse button is held down.
//            if (Input.GetMouseButton(0))
//            {
//                var ray = camera.ScreenPointToRay(Input.mousePosition);
//
//                // If an object was hit
//                RaycastHit hit;
//                if (Physics.Raycast(ray, out hit))
//                {
//                    if (!_dragActive)
//                    {
//                        _vDown = hit.point - camera.transform.position;
//                        _dragActive = true;
//                    }
//                    else
//                    {
//                        _vDrag = hit.point - camera.transform.position;
//                        _rotationAxis = Vector3.Cross(_vDown, _vDrag);
//                        _angularVelocity = Vector3.Angle(_vDown, _vDrag) * _mouseSensitivity;
//                    }
//                }
//                else
//                {
//                    _dragActive = false;
//                }
//            }
//
//            if (Input.GetMouseButtonUp(0))
//            {
//                _dragActive = false;
//            }
//
//            if (_angularVelocity > 0)
//            {
//                camera.transform.RotateAround(_targetRenderer.bounds.center + _centerOffset, _rotationAxis, _angularVelocity * Time.deltaTime);
//                _angularVelocity = (_angularVelocity > _epsilon) ? _angularVelocity * _damping : 0.0f;
//            }
//            else
//            {
//                // _camera.transform.RotateAround (_targetRenderer.bounds.center + _centerOffset, Vector3.up, _rotationSpeed * Time.deltaTime);
//            }
        }
    }
}