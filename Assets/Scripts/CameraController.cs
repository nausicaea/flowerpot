using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour {
	/// <summary>
	/// Determines the smalles valid field of view.
	/// </summary>
	public float _minFov = 15.0f;
	/// <summary>
	/// Determines the largest valid field of view.
	/// </summary>
	public float _maxFov = 90.0f;
	/// <summary>
	/// Specifies the mouse-wheel sensitivity.
	/// </summary>
	public float _wheelSensitivity = 10.0f;
	public float _mouseSensitivity = 0.1f;
	/// <summary>
	/// Determines the camera rotation speed.
	/// </summary>
	public float _rotationSpeed = 10.0f;
	public float _damping = 0.9f;
	/// <summary>
	/// Determines the offset from the target's bounding box center
	/// on the basis of which the camera rotation center is chosen. 
	/// </summary>
	public Vector3 _centerOffset = new Vector3 (0.0f, -2.0f, 0.0f);
	/// <summary>
	/// The target.
	/// </summary>
	public GameObject _target;

	private Camera _camera;
	private Renderer _targetRenderer;
	private Vector3 _vDown = Vector3.zero;
	private Vector3 _vDrag = Vector3.zero;
	private Vector3 _rotationAxis = Vector3.zero;
	private float _angularVelocity = 0.0f;
	private bool _dragActive = false;
	private float _epsilon = 0.01f;

	/// <summary>
	/// Obtains a reference to the <see cref="Camera"/>.
	/// </summary>
	void Awake () {
		_camera = GetComponent<Camera> ();
		_targetRenderer = _target.GetComponent<Renderer> ();
	}

	/// <summary>
	/// Changes the <see cref="Camera"/> transform to look at the target object.
	/// </summary>
	void Start () {
		_camera.transform.LookAt (_targetRenderer.bounds.center + _centerOffset);
	}

	/// <summary>
	/// Update this instance.
	/// </summary>
	void Update () {
		// Allow the player to zoom the camera.
		var fieldOfView = _camera.fieldOfView;
		fieldOfView += Input.GetAxis ("Mouse ScrollWheel") * _wheelSensitivity;
		fieldOfView = Mathf.Clamp (fieldOfView, _minFov, _maxFov);
		_camera.fieldOfView = fieldOfView;

		// Rotate around the target object.
		// _camera.transform.RotateAround (_targetRenderer.bounds.center + _centerOffset, Vector3.up, _rotationSpeed * Time.deltaTime);

		// If the first mouse button is held down.
		if (Input.GetMouseButton (0)) {
			var ray = _camera.ScreenPointToRay (Input.mousePosition);

			// If an object was hit
			RaycastHit hit;
			if (Physics.Raycast (ray, out hit)) {
				if (!_dragActive) {
					_vDown = hit.point - _camera.transform.position;
					_dragActive = true;
				} else {
					_vDrag = hit.point - _camera.transform.position;
					_rotationAxis = Vector3.Cross (_vDown, _vDrag);
					_angularVelocity = Vector3.Angle (_vDown, _vDrag) * _mouseSensitivity;
				}
			} else {
				_dragActive = false;
			}
		}

		if (Input.GetMouseButtonUp (0)) {
			_dragActive = false;
		}

		if (_angularVelocity > 0) {
			_camera.transform.RotateAround (_targetRenderer.bounds.center + _centerOffset, _rotationAxis, _angularVelocity * Time.deltaTime);
			_angularVelocity = (_angularVelocity > _epsilon) ? _angularVelocity * _damping : 0.0f;
		} else {
			// _camera.transform.RotateAround (_targetRenderer.bounds.center + _centerOffset, Vector3.up, _rotationSpeed * Time.deltaTime);
		}
	}
}
