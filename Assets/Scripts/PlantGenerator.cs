using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Generates plants based on Lindenmayer Systems.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class PlantGenerator : MonoBehaviour {

	/// The `axiom` specifies the starting value of the L-System.
	public string axiom = "F";
	/// The `rules` container holds a set of L-System rules.
	public List<string> rules = new List<string> {"F=FF", "X=F[[X]+X]-FX"};
	/// `iterations` holds the number of L-System iterations.
	public uint iterations = 5;
	public string lSystemState = "";

	private LSystem<char> _lSystem;
	private Regex _ruleRegex = new Regex (@"([a-zA-Z])\s*=\s*(\S+)");

	/// <summary>
	/// Reloads the rules specified in this object's serialization to populate the L-System.
	/// </summary>
	private void ReloadRules () {
		_lSystem.ClearRules ();
		foreach (var rule in rules) {
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
		BackupState ();
	}
	private void InterpretState () {
		foreach (var atom in _lSystem.Current) {
			switch (atom) {
			case 'F':
				break;
			default:
				// Ignore any other symbols.
				break;
			}
		}
		BackupState ();
	}
	/// <summary>
	/// Copies the state of the L-System such that Unity can serialize it.
	/// </summary>
	private void BackupState () {
		lSystemState = new string (_lSystem.Current.ToArray ());
	}

	void Awake () {
		if (lSystemState.Length > 0) {
			_lSystem = new LSystem<char> (new List<char> (lSystemState.ToCharArray()), new List<char> (axiom.ToCharArray()), new Dictionary<char, List<char>> ());
		} else {
			_lSystem = new LSystem<char> (new List<char> (axiom.ToCharArray()));
		}

		ReloadRules ();
	}

	// Use this for initialization
	void Start () {
	}
	
	// Update is called once per frame
	void Update () {
		
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
	/// Initializes a new instance of the <see cref="LSystem`1"/> class.
	/// </summary>
	/// <param name="state">The internal state of the L-System.</param>
	/// <param name="axiom">The initial starting point of the L-System.</param>
	/// <param name="rules">The set of rules that are used to evolve the L-System.</param>
	public LSystem (List<T> state, List<T> axiom, Dictionary<T, List<T>> rules) {
		_state = state;
		_axiom = axiom;
		_rules = rules;
	}
	/// <summary>
	/// Initializes a new instance of the <see cref="LSystem"/> class
	/// using two parameters.
	/// </summary>
	/// <param name="axiom">The initial starting point of the L-System.</param>
	/// <param name="rules">The set of rules that are used to evolve the L-System.</param>
	public LSystem (List<T> axiom, Dictionary<T, List<T>> rules) {
		_state = new List<T> (axiom);
		_axiom = axiom;
		_rules = rules;
	}
	/// <summary>
	/// Initializes a new instance of the <see cref="LSystem"/> class
	/// with an empty ruleset.
	/// </summary>
	/// <param name="axiom">The initial starting point of the L-System.</param>
	public LSystem (List<T> axiom) {
		_state = new List<T> (axiom);
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
		var expanded = false;
		var i = 0;
		while (i < _state.Count) {
			var atom = _state [i];
			List<T> products;
			if (_rules.TryGetValue (atom, out products)) {
				_state.RemoveAt (i);
				foreach (var product in products) {
					_state.Insert (i, product);
					i += 1;
				}
				expanded = true;
			} else {
				i += 1;
			}
		}

		return expanded;
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
		_state = new List<T> (_axiom);
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