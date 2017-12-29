using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Generates plants based on Lindenmayer Systems.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[ExecuteInEditMode]
public class PlantGenerator : MonoBehaviour {
	/// <summary>
	/// The axiom, that is, the starting point of the L-System.
	/// </summary>
	public string _axiom = "X";
	/// <summary>
	/// The set of rules that define the L-System.
	/// </summary>
	public List<string> _rules = new List<string> {"F=FF", "X=F[[X]+X]-FX"};
	/// <summary>
	/// Determines the number of L-System iterations.
	/// </summary>
	[Range(0, 10)]
	public int _numIterations = 1;
	/// <summary>
	/// Determines the discrete angle at which L-System rules rotate the object.
	/// </summary>
	[Range(0.0f, 90.0f)]
	public float _rotationAngle = 22.5f;
	/// <summary>
	/// Determines the trunk radius at the base of the plant.
	/// </summary>
	[Range(0.25f, 4.0f)]
	public float _maxRadius = 2.0f;
	/// <summary>
	/// Determines the number of faces on each segment ring.
	/// </summary>
	[Range(3, 32)]
	public int _faceNumber = 16;
	/// <summary>
	/// Controls the factor at which the branch radius decreases with every step.
	/// </summary>
	[Range(0.5f, 0.99f)]
	public float _radiusFactor = 0.9f;
	/// <summary>
	/// Sets the length of each branch segment.
	/// </summary>
	[Range(0.1f, 2.0f)]
	public float _segmentLength = 0.5f;

	private MeshFilter _meshFilter;
	private LSystem<char> _lSystem;
	private Regex _ruleRegex;
	private float _texCoordUIncrement;
	private float _angleIncrement;

	/// <summary>
	/// Reloads the rules specified in this object's serialization to populate the L-System.
	/// </summary>
	private void ReloadRules () {
		_lSystem.ClearRules ();
		foreach (var rule in _rules) {
			var match = _ruleRegex.Match (rule);
			if (match.Success) {
				var ruleName = match.Groups [1].Value[0];
				var ruleValue = new List<char> (match.Groups [2].Value.ToCharArray ());
				_lSystem.InsertRule (ruleName, ruleValue);
			} else {
				Debug.LogWarning (String.Format ("The rule '{0}' could not be parsed. " +
					"Expected something like 'A=B+C[D]'.", rule));
			}
		}
	}
	private void DrawBranch (ref List<Vertex> vertices, ref List<int> indices, int vertexIndex, Vector3 position, Quaternion orientation, float radius, float texCoordV) {
		// Create the ring of vertices around the current position.
		var vertexOffset = Vector3.zero;
		var texCoord = new Vector2 (0.0f, texCoordV);
		var angle = 0.0f;
		for (var i = 0; i <= _faceNumber; i++, angle += _angleIncrement) {
			vertexOffset.x = radius * Mathf.Cos (angle);
			vertexOffset.z = radius * Mathf.Sin (angle);
			vertices.Add (new Vertex (position + orientation * vertexOffset, texCoord));
			texCoord.x += _texCoordUIncrement;
		}

		// Applies only after the base of the tree has been created.
		// Create two triangles for each face that connects the current ring to the last ring of vertices.
		if (vertexIndex >= 0) {
			for (var i = vertices.Count - _faceNumber - 1; i < vertices.Count - 1; i++, vertexIndex++) {
				indices.Add (vertexIndex + 1);
				indices.Add (vertexIndex);
				indices.Add (i);
				indices.Add (i);
				indices.Add (i + 1);
				indices.Add (vertexIndex + 1);
			}
		}
	}
	private void DrawCap (ref List<Vertex> vertices, ref List<int> indices, Vector3 position, Vector2 texCoord) {
		vertices.Add (new Vertex (position, texCoord + Vector2.one));
		for (var i = vertices.Count - _faceNumber - 2; i < vertices.Count - 2; i++) {
			indices.Add (i);
			indices.Add (vertices.Count - 1);
			indices.Add (i + 1);
		}
	}
	private int GenerateTreeRecursive (List<char> pattern, int patternIndex, ref List<Vertex> vertices, ref List<int> indices, int vertexIndex, Vector3 position, Quaternion orientation, float radius, float texCoordV) {
		if (vertexIndex < 0) {
			DrawBranch (ref vertices, ref indices, vertexIndex, position, orientation, radius, texCoordV);
			vertexIndex = vertices.Count - _faceNumber - 1;
		}

		int i = 0;
		for (i = patternIndex; i < pattern.Count; i++) {
			switch (pattern [i]) {
			case 'F':
				radius *= _radiusFactor;
				texCoordV += 0.0625f * (_segmentLength + _segmentLength / radius);
				position += _segmentLength * (orientation * new Vector3 (0.0f, 1.0f, 0.0f));
				DrawBranch (ref vertices, ref indices, vertexIndex, position, orientation, radius, texCoordV);
				vertexIndex = vertices.Count - _faceNumber - 1;
				break;
			case '+':
				orientation *= Quaternion.AngleAxis (_rotationAngle, Vector3.forward);
				break;
			case '-':
				orientation *= Quaternion.AngleAxis (-_rotationAngle, Vector3.forward);
				break;
			case '[':
				i = GenerateTreeRecursive (pattern, i + 1, ref vertices, ref indices, vertexIndex, position, orientation, radius, texCoordV) - 1;
				break;
			case ']':
				DrawCap (ref vertices, ref indices, position, new Vector2 (0.0f, texCoordV));
				return i + 1;
			default:
				// Ignore any other symbols.
				break;
			}
		}
			
		DrawCap (ref vertices, ref indices, position, new Vector2 (0.0f, texCoordV));
		return i + 1;
	}
	/// <summary>
	/// Generates the current L-System iteration as 3D tree, and updates the current object's mesh.
	/// </summary>
	private void GenerateTree () {
		Debug.Log (String.Format ("{0}", new string(_lSystem.Current.ToArray ())));

		// Create the vertex data containers.
		var vertices = new List<Vertex> ();
		var indices = new List<int> ();

		// Initialize position, orientation, radius, and the second component of the texture coordinate.
		var position = Vector3.zero;
		// var orientation = Quaternion.AngleAxis (90.0f, Vector3.right);
		var orientation = Quaternion.identity;
		var radius = _maxRadius;
		var texCoordV = 0.0f;

		// Generate the tree.
		GenerateTreeRecursive (_lSystem.Current, 0, ref vertices, ref indices, -1, position, orientation, radius, texCoordV);

		// Calculate the resulting mesh.
		UpdateMesh (ref vertices, ref indices);
	}
	/// <summary>
	/// Given two vertex data containers, updates the current object's mesh.
	/// </summary>
	/// <param name="vertices">Vertex data (position and texture coordinates).</param>
	/// <param name="indices">Triangle indices.</param>
	private void UpdateMesh (ref List<Vertex> vertices, ref List<int> indices) {
		// Start afresh (create or clear the mesh).
		if (_meshFilter.sharedMesh == null) {
			_meshFilter.sharedMesh = new Mesh ();
		} else {
			_meshFilter.sharedMesh.Clear ();
		}

		var mesh = _meshFilter.sharedMesh;

		// Copy over the vertex data.
		mesh.name = "Procedural Mesh";
		mesh.vertices = vertices.Select (v => v.position).ToArray ();
		mesh.uv = vertices.Select (v => v.texCoord).ToArray ();
		mesh.triangles = indices.ToArray ();

		// Update the other mesh parameters
		mesh.RecalculateNormals ();
		mesh.RecalculateBounds ();
	}
	/// <summary>
	/// Initialize this MonoBehaviour instance. Is run even if the respective component is inactive.
	/// </summary>
	void Awake () {
		// Find the MeshFilter component.
		_meshFilter = GetComponent<MeshFilter> ();

		// Initialize the other properties
		_ruleRegex = new Regex (@"([a-zA-Z])\s*=\s*([-+a-zA-Z[\]]+)");
		_texCoordUIncrement = 1.0f / (float) _faceNumber;
		_angleIncrement = 2.0f * Mathf.PI * _texCoordUIncrement;

		// Initialize the actual L-System.
		_lSystem = new LSystem<char> (new List<char> (_axiom.ToCharArray()));

		ReloadRules ();

		for (var i = 0; i <= _numIterations; i++) {
			_lSystem.MoveNext ();
		}
		GenerateTree ();
	}
}
/// <summary>
/// Simplifies the creation of a single vertex.
/// </summary>
class Vertex {
	public Vector3 position;
	public Vector2 texCoord;

	/// <summary>
	/// Initializes a new instance of the <see cref="Vertex"/> class.
	/// </summary>
	/// <param name="p">Position.</param>
	/// <param name="t">Texture coordinates.</param>
	public Vertex (Vector3 p, Vector2 t) {
		position = p;
		texCoord = t;
	}
}
/// <summary>
/// An implementation of a Lindenmayer System as an IEnumerator.
/// </summary>
class LSystem<T> : IEnumerator<List<T>> where T: struct {
	private List<T> _state;
	private List<T> _axiom;
	private Dictionary<T, List<T>> _rules;

	/// <summary>
	/// Initializes a new instance of the <see cref="LSystem"/> class
	/// with an empty ruleset.
	/// </summary>
	/// <param name="axiom">The initial starting point of the L-System.</param>
	public LSystem (List<T> axiom) {
		_state = new List<T> ();
		_axiom = axiom;
		_rules = new Dictionary<T, List<T>> ();
	}
	/// <summary>
	/// Inserts the specified rule.
	/// </summary>
	/// <param name="symbol">Symbol.</param>
	/// <param name="production">Production.</param>
	public void InsertRule (T symbol, List<T> production) {
		_rules.Add (symbol, production);
	}
	/// <summary>
	/// Clears the rules.
	/// </summary>
	public void ClearRules () {
		_rules.Clear ();
	}
	/// <summary>
	/// Next this instance.
	/// </summary>
	public bool MoveNext () {
		// If the states list is empty, assign the axiom as the first state.
		if (_state.Count == 0) {
			_state = new List<T> (_axiom);
			return true;
		} else {
			var nextState = new List<T> (_state);
			var expanded = false;
			var i = 0;
			while (i < nextState.Count) {
				var atom = nextState [i];
				List<T> products;
				if (_rules.TryGetValue (atom, out products)) {
					nextState.RemoveAt (i);
					foreach (var product in products) {
						nextState.Insert (i, product);
						i += 1;
					}
					_state = nextState;
					expanded = true;
				} else {
					i += 1;
				}
			}

			return expanded;
		}
	}
	/// <summary>
	/// Provides access to the internal state (i.e. the current string pattern).
	/// </summary>
	/// <value>The state.</value>
	public List<T> Current {
		get {
			return _state;
		}
	}
	/// <summary>
	/// Reset this instance such that the internal state matches the originally supplied axiom.
	/// </summary>
	public void Reset () {
		_state.Clear ();
	}
	/// <summary>
	/// Implements the IEnumerator interface for the Current property.
	/// </summary>
	/// <value>The current state.</value>
	object IEnumerator.Current {
		get {
			return Current;
		}
	}
	/// <summary>
	/// Releases all resource used by the <see cref="LSystem`1"/> object.
	/// </summary>
	/// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="LSystem`1"/>. The <see cref="Dispose"/>
	/// method leaves the <see cref="LSystem`1"/> in an unusable state. After calling <see cref="Dispose"/>, you must
	/// release all references to the <see cref="LSystem`1"/> so the garbage collector can reclaim the memory that the
	/// <see cref="LSystem`1"/> was occupying.</remarks>
	void IDisposable.Dispose () {}
}