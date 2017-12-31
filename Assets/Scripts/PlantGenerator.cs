using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AssemblyCSharp
{
    /// <summary>
    /// <para>
    /// Generates plants based on Lindenmayer Systems. The public parameter <see cref="PlantGenerator.rules"/> 
    /// may contain a set of mappings from a symbol to a string of other symbols.
    /// </para>
    /// <para>
    /// With every iteration of the L-system, the specified symbols are replaced with their
    /// expanded counterpart, if the symbols are found in the current state of the L-system
    /// (in itself a string of symbols that starts of with the <see cref="PlantGenerator.axiom"/>).
    /// </para>
    /// </summary>
    /// <remarks>
    /// 
    /// <list type="bullet">
    /// <listheader>
    /// <term>Known Symbols</term>
    /// </listheader>
    /// <item>
    /// <term>F</term>
    /// <description>Results in a segment of the plant being drawn.</description>
    /// </item>
    /// <item>
    /// <term>+</term>
    /// <description>Results in a clockwise rotation around the x-axis.</description>
    /// </item>
    /// <item>
    /// <term>-</term>
    /// <description>Results in an anti-clockwise rotation around the x-axis.</description>
    /// </item>
    /// <item>
    /// <term>a</term>
    /// <description>Results in an anti-clockwise rotation around the y-axis.</description>
    /// </item>
    /// <item>
    /// <term>c</term>
    /// <description>Results in a clockwise rotation around the y-axis.</description>
    /// </item>
    /// <item>
    /// <term>l</term>
    /// <description>Results in an anti-clockwise rotation around the z-axis.</description>
    /// </item>
    /// <item>
    /// <term>r</term>
    /// <description>Results in a clockwise rotation around the z-axis.</description>
    /// </item>
    /// <item>
    /// <term>[</term>
    /// <description>Results in a branching instruction. Saves the current transformational state
    /// of the pattern interpreter.</description>
    /// </item>
    /// <item>
    /// <term>]</term>
    /// <description>Results in the end of a branching instruction. Restores the previous transformational
    /// state of the pattern interpreter.</description>
    /// </item>
    /// </list>
    /// </remarks>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [ExecuteInEditMode]
    public class PlantGenerator : MonoBehaviour
    {
        /// <summary>
        /// The axiom, that is, the starting point of the L-System.
        /// </summary>
        public string axiom = "X";
        /// <summary>
        /// The set of rules that define the L-System.
        /// </summary>
        public List<string> rules = new List<string> { "X=F[-XF][+XF][lXF][rXF],F[-FX][+FX][lFX][rFX]" };
        /// <summary>
        /// Determines the number of L-System iterations. Its fractional part 
        /// determines the length of the end of branch segments.
        /// </summary>
        [Range(0.0f, 10.0f)]
        public float numIterations = 1.0f;
        /// <summary>
        /// Determines the quantized rotation angle for each rotation operation.
        /// </summary>
        [Range(0.0f, 90.0f)]
        public float rotationAngle = 22.5f;
        /// <summary>
        /// Determines the factor with which the rotation angle is randomly skewed.
        /// </summary>
        [Range(0.0f, 20.0f)]
        public float rotationUncertainty = 6.0f;
        /// <summary>
        /// Determines the trunk radius at the base of the plant.
        /// </summary>
        [Range(0.01f, 4.0f)]
        public float baseRadius = 0.05f;
        /// <summary>
        /// Controls the factor at which the branch radius decreases with every step.
        /// </summary>
        [Range(0.5f, 0.99f)]
        public float radiusDecrease = 0.9f;
        /// <summary>
        /// Determines the number of faces on each segment ring.
        /// </summary>
        [Range(3, 32)]
        public int faceNumber = 8;
        /// <summary>
        /// Sets the length of each branch segment.
        /// </summary>
        [Range(0.1f, 2.0f)]
        public float segmentLength = 0.2f;

        MeshFilter meshFilter;
        LSystem<char> lSystem;
        Regex ruleRegex = new Regex(@"([a-zA-Z])\s*=\s*([-+a-zA-Z[\]]+)(\s*,\s*([-+a-zA-Z[\]]+))*");
        float texCoordUIncrement;
        float angleIncrement;

        /// <summary>
        /// Gets the randomized quantized rotation angle. 
        /// This is used to destroy the perfect symmetry of L-systems by 
        /// introducing a slight variation in the angle every time a rotation rule is interpreted.
        /// </summary>
        /// <value>The randomized angle.</value>
        float RandomizedAngle
        {
            get
            {
                return this.rotationAngle + this.rotationUncertainty * (UnityEngine.Random.value - 0.5f);
            }
        }

        /// <summary>
        /// Reloads the rules specified in this object's serialization to populate the L-System.
        /// </summary>
        void ReloadRules()
        {
            this.lSystem.ClearRules();
            foreach (string rule in this.rules)
            {
                var match = ruleRegex.Match(rule);
                if (match.Success)
                {
                    // Determine the symbol to which the rule(s) will be assigned.
                    var ruleName = match.Groups[1].Value[0];

                    // Retrieve all rules.
                    var ruleValues = new List<List<char>>();
                    ruleValues.Add(new List<char> (match.Groups[2].Value.ToCharArray()));
                    foreach (Capture ruleValueAdditional in match.Groups[4].Captures) {
                        ruleValues.Add(new List<char>(ruleValueAdditional.Value.ToCharArray()));
                    }

                    // Insert the resulting rules.
                    this.lSystem.InsertRule(ruleName, ruleValues);
                }
                else
                {
                    Debug.LogWarning(String.Format("The rule '{0}' could not be parsed. " +
                            "Expected something like 'A=B+C[D]'.", rule));
                }
            }
        }

        /// <summary>
        /// Draws the branch.
        /// </summary>
        /// <returns>The branch.</returns>
        /// <param name="vertices">Vertices.</param>
        /// <param name="indices">Indices.</param>
        /// <param name="vertexIndex">Vertex index.</param>
        /// <param name="position">Position.</param>
        /// <param name="orientation">Orientation.</param>
        /// <param name="radius">Radius.</param>
        /// <param name="texCoordV">Tex coordinate v.</param>
        int DrawBranch(ref List<Vertex> vertices, ref List<int> indices, int vertexIndex, Vector3 position, Quaternion orientation, float radius, float texCoordV)
        {
            // Create the ring of vertices around the current position.
            var vertexOffset = Vector3.zero;
            var texCoord = new Vector2(0.0f, texCoordV);
            var angle = 0.0f;
            for (var i = 0; i <= this.faceNumber; i++, angle += this.angleIncrement)
            {
                vertexOffset.x = radius * Mathf.Cos(angle);
                vertexOffset.z = radius * Mathf.Sin(angle);
                vertices.Add(new Vertex(position + orientation * vertexOffset, texCoord));
                texCoord.x += this.texCoordUIncrement;
            }

            // Applies only after the base of the tree has been created.
            // Create two triangles for each face that connects the current ring to the last ring of vertices.
            if (vertexIndex >= 0)
            {
                for (var i = vertices.Count - this.faceNumber - 1; i < vertices.Count - 1; i++, vertexIndex++)
                {
                    indices.Add(vertexIndex + 1);
                    indices.Add(vertexIndex);
                    indices.Add(i);
                    indices.Add(i);
                    indices.Add(i + 1);
                    indices.Add(vertexIndex + 1);
                }
            }

            // Always draw a branch cap (I know, I know...).
            this.DrawCap(ref vertices, ref indices, position, texCoord);

            // Return the next vertex index.
            return vertices.Count - this.faceNumber - 2;
        }

        /// <summary>
        /// Draws the branch cap.
        /// </summary>
        /// <param name="vertices">Vertices.</param>
        /// <param name="indices">Indices.</param>
        /// <param name="position">Position.</param>
        /// <param name="texCoord">Tex coordinate.</param>
        void DrawCap(ref List<Vertex> vertices, ref List<int> indices, Vector3 position, Vector2 texCoord)
        {
            vertices.Add(new Vertex(position, texCoord + Vector2.one));
            for (var i = vertices.Count - this.faceNumber - 2; i < vertices.Count - 2; i++)
            {
                indices.Add(i);
                indices.Add(vertices.Count - 1);
                indices.Add(i + 1);
            }
        }

        /// <summary>
        /// Recursively generates the procedural tree.
        /// </summary>
        /// <returns>The tree recursive.</returns>
        /// <param name="pattern">Pattern.</param>
        /// <param name="patternIndex">Pattern index.</param>
        /// <param name="vertices">Vertices.</param>
        /// <param name="indices">Indices.</param>
        /// <param name="vertexIndex">Vertex index.</param>
        /// <param name="position">Position.</param>
        /// <param name="orientation">Orientation.</param>
        /// <param name="radius">Radius.</param>
        /// <param name="texCoordV">Tex coordinate v.</param>
        int GenerateTreeRecursive(List<char> pattern, int patternIndex, ref List<Vertex> vertices, ref List<int> indices, int vertexIndex, Vector3 position, Quaternion orientation, float radius, float texCoordV)
        {
            if (vertexIndex < 0)
            {
                vertexIndex = this.DrawBranch(ref vertices, ref indices, vertexIndex, position, orientation, radius, texCoordV);
            }

            int i = 0;
            for (i = patternIndex; i < pattern.Count; i++)
            {
                switch (pattern[i])
                {
                    case 'F':
                        position += this.segmentLength * (orientation * Vector3.up);
                        radius *= this.radiusDecrease;
                        texCoordV += 0.0625f * this.segmentLength * (1.0f + 1.0f / radius);
                        vertexIndex = this.DrawBranch(ref vertices, ref indices, vertexIndex, position, orientation, radius, texCoordV);
                        break;
                    case '+':
                        orientation *= Quaternion.AngleAxis(this.RandomizedAngle, Vector3.right);
                        break;
                    case '-':
                        orientation *= Quaternion.AngleAxis(-this.RandomizedAngle, Vector3.right);
                        break;
                    case 'a':
                        orientation *= Quaternion.AngleAxis(this.RandomizedAngle, Vector3.up);
                        break;
                    case 'c':
                        orientation *= Quaternion.AngleAxis(-this.RandomizedAngle, Vector3.up);
                        break;
                    case 'l':
                        orientation *= Quaternion.AngleAxis(this.RandomizedAngle, Vector3.forward);
                        break;
                    case 'r':
                        orientation *= Quaternion.AngleAxis(-this.RandomizedAngle, Vector3.forward);
                        break;
                    case '[':
                        i = this.GenerateTreeRecursive(pattern, i + 1, ref vertices, ref indices, vertexIndex, position, orientation, radius, texCoordV) - 1;
                        break;
                    case ']':
                        return i + 1;
                }
            }
			
            return i + 1;
        }

        /// <summary>
        /// Generates the current L-System iteration as 3D tree, and updates the current object's mesh.
        /// </summary>
        void GenerateTree()
        {
            Debug.Log(String.Format("Current pattern: {0}", new string(this.lSystem.Current.ToArray())));

            // Create the vertex data containers.
            var vertices = new List<Vertex>();
            var indices = new List<int>();

            // Generate the tree.
            this.GenerateTreeRecursive(this.lSystem.Current, 0, ref vertices, ref indices, -1, Vector3.zero, Quaternion.identity, this.baseRadius, 0.0f);

            // Calculate the resulting mesh.
            this.UpdateMesh(ref vertices, ref indices);
        }

        /// <summary>
        /// Given two vertex data containers, updates the current object's mesh.
        /// </summary>
        /// <param name="vertices">Vertex data (position and texture coordinates).</param>
        /// <param name="indices">Triangle indices.</param>
        void UpdateMesh(ref List<Vertex> vertices, ref List<int> indices)
        {
            // Start afresh (create or clear the mesh).
            if (this.meshFilter.sharedMesh == null)
            {
                this.meshFilter.sharedMesh = new Mesh();
            }
            else
            {
                this.meshFilter.sharedMesh.Clear();
            }

            var mesh = meshFilter.sharedMesh;

            // Copy over the vertex data.
            mesh.name = "Procedural Mesh";
            mesh.vertices = vertices.Select(v => v.position).ToArray();
            mesh.uv = vertices.Select(v => v.texCoord).ToArray();
            mesh.triangles = indices.ToArray();

            // Update the other mesh parameters
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Updates the internal private parameters based on the public ones
        /// (some data is duplicated).
        /// </summary>
        void UpdateParameters()
        {
            // Initialize the other properties
            this.texCoordUIncrement = 1.0f / (float)this.faceNumber;
            this.angleIncrement = 2.0f * Mathf.PI * this.texCoordUIncrement;
            this.lSystem.axiom = new List<char>(this.axiom.ToCharArray());
            this.ReloadRules();
            this.lSystem.Reset();
            // Move the iterator to the first element, i.e. the axiom.
            this.lSystem.MoveNext();
            for (var i = 0; i < (int)Mathf.Floor(this.numIterations); i++)
            {
                this.lSystem.MoveNext();
            }
        }

        /// <summary>
        /// Initialize this MonoBehaviour instance. Is run even if the respective component is inactive.
        /// </summary>
        void Awake()
        {
            // Find the MeshFilter component.
            this.meshFilter = this.GetComponent<MeshFilter>();

            // Initialize the actual L-System.
            this.lSystem = new LSystem<char>(new List<char>(this.axiom.ToCharArray()));

            this.UpdateParameters();
            this.GenerateTree();
        }

        /// <summary>
        /// Re-generate the tree, if inspector parameters are changed from within the editor.
        /// </summary>
        void OnValidate()
        {
            if (this.meshFilter != null && this.lSystem != null)
            {
                this.UpdateParameters();
                this.GenerateTree();
            }
        }
    }

    /// <summary>
    /// Simplifies the creation of a single vertex.
    /// </summary>
    class Vertex
    {
        public Vector3 position;
        public Vector2 texCoord;

        /// <summary>
        /// Initializes a new instance of the <see cref="Vertex"/> class.
        /// </summary>
        /// <param name="p">Position.</param>
        /// <param name="t">Texture coordinates.</param>
        public Vertex(Vector3 p, Vector2 t)
        {
            this.position = p;
            this.texCoord = t;
        }
    }

    /// <summary>
    /// An implementation of a Lindenmayer System as an IEnumerator.
    /// Includes stochastic rules: if multiple replacements (i.e. products) are specified
    /// for a particular symbol, one is chosen at random.
    /// </summary>
    class LSystem<T> : IEnumerator<List<T>> where T: struct
    {
        List<T> state;
        public List<T> axiom;
        Dictionary<T, List<List<T>>> rules;
        System.Random rng;

        /// <summary>
        /// Initializes a new instance with an empty state and ruleset.
        /// </summary>
        /// <param name="axiom">The initial starting point of the L-System.</param>
        public LSystem(List<T> axiom)
        {
            this.state = new List<T>();
            this.axiom = axiom;
            this.rules = new Dictionary<T, List<List<T>>>();
            this.rng = new System.Random();
        }

        /// <summary>
        /// Randomly chooses one element of the supplied list.
        /// Assumes that the list is not empty.
        /// </summary>
        /// <returns>The random choice.</returns>
        /// <param name="choices">A list of possible choices.</param>
        List<T> RandomChoice(List<List<T>> choices)
        {
            var i = this.rng.Next(choices.Count);
            return choices[i];
        }

        /// <summary>
        /// Inserts the specified rule. Note that when expanding the rules,
        /// one of the productions is chosen at random.
        /// </summary>
        /// <param name="symbol">Symbol.</param>
        /// <param name="productions">Productions (i.e. a list of symbol replacements).</param>
        public void InsertRule(T symbol, List<List<T>> productions)
        {
            this.rules.Add(symbol, productions);
        }

        /// <summary>
        /// Clears the rules.
        /// </summary>
        public void ClearRules()
        {
            this.rules.Clear();
        }

        /// <summary>
        /// Increment the current state of the instance by applying rule replacements.
        /// Stops the iteration by returning false, once the state does not expand any further.
        /// Note that if multiple replacement rules (i.e. productions) are specified for a 
        /// particular symbol, one is chosen at random.
        /// </summary>
        public bool MoveNext()
        {
            // If the states list is empty, assign the axiom as the first state.
            if (this.state.Count == 0)
            {
                this.state = new List<T>(this.axiom);
                return true;
            }
            // Otherwise, apply the replacement rules to expand the current state
            // into the next one.
            else
            {
                // Copy the current state to the next state.
                var nextState = new List<T>(this.state);
                var expanded = false;

                // Iterate through all elements of the state (i.e. symbols).
                var i = 0;
                while (i < nextState.Count)
                {
                    // If the current symbol is represented within the ruleset,
                    // replace it with a random choice of the available rules
                    // for the specified symbol.
                    List<List<T>> products;
                    if (this.rules.TryGetValue(nextState[i], out products))
                    {
                        var product = this.RandomChoice(products);
                        nextState.RemoveAt(i);
                        foreach (T symbol in product)
                        {
                            nextState.Insert(i, symbol);
                            i += 1;
                        }
                        this.state = nextState;
                        expanded = true;
                    }
                    else
                    {
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
        public List<T> Current
        {
            get
            {
                return this.state;
            }
        }

        /// <summary>
        /// Reset this instance such that the internal state matches the originally supplied axiom.
        /// </summary>
        public void Reset()
        {
            this.state.Clear();
        }

        /// <summary>
        /// Implements the IEnumerator interface for the Current property.
        /// </summary>
        /// <value>The current state.</value>
        object IEnumerator.Current
        {
            get
            {
                return this.Current;
            }
        }

        /// <summary>
        /// Releases all resource used by the instance. This is necessary for
        /// <see cref="IEnumerable"/> to be implemented correctly, but does
        /// nothing internally.
        /// </summary>
        void IDisposable.Dispose()
        {
        }
    }
}