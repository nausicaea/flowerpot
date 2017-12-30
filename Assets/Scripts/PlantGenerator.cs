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
        public List<string> rules = new List<string> { "F=FF", "X=F[[X]+X]-FX" };
        /// <summary>
        /// Determines the number of L-System iterations.
        /// </summary>
        [Range(0, 10)]
        public int numIterations = 1;
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
        public float baseRadius = 2.0f;
        /// <summary>
        /// Determines the number of faces on each segment ring.
        /// </summary>
        [Range(3, 32)]
        public int faceNumber = 16;
        /// <summary>
        /// Controls the factor at which the branch radius decreases with every step.
        /// </summary>
        [Range(0.5f, 0.99f)]
        public float radiusDecrease = 0.9f;
        /// <summary>
        /// Sets the length of each branch segment.
        /// </summary>
        [Range(0.1f, 2.0f)]
        public float segmentLength = 0.5f;

        MeshFilter meshFilter;
        LSystem<char> lSystem;
        Regex ruleRegex;
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
            foreach (var rule in this.rules)
            {
                var match = ruleRegex.Match(rule);
                if (match.Success)
                {
                    var ruleName = match.Groups[1].Value[0];
                    var ruleValue = new List<char>(match.Groups[2].Value.ToCharArray());
                    Debug.Log(String.Format("Additional rules: {1} (found {0} Groups)", match.Groups.Count, match.Groups[3].Captures.Count));
                    this.lSystem.InsertRule(ruleName, ruleValue);
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

            // Return the next vertex index.
            return vertices.Count - this.faceNumber - 1;
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
                        radius *= this.radiusDecrease;
                        texCoordV += 0.0625f * this.segmentLength * (1.0f + 1.0f / radius);
                        position += this.segmentLength * (orientation * new Vector3(0.0f, 1.0f, 0.0f));
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
                        this.DrawCap(ref vertices, ref indices, position, new Vector2(0.0f, texCoordV));
                        return i + 1;
                    default:
				// Ignore any other symbols.
                        break;
                }
            }
			
            this.DrawCap(ref vertices, ref indices, position, new Vector2(0.0f, texCoordV));
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
            this.ReloadRules();
            this.lSystem.Reset();
            for (var i = 0; i <= this.numIterations; i++)
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
            this.ruleRegex = new Regex(@"([a-zA-Z])\s*=\s*([-+a-zA-Z[\]]+)(?!,\s*([-+a-zA-Z[\]]+))*");

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
    /// </summary>
    class LSystem<T> : IEnumerator<List<T>> where T: struct
    {
        List<T> state;
        List<T> axiom;
        Dictionary<T, List<T>> rules;

        /// <summary>
        /// Initializes a new instance with an empty state and ruleset.
        /// </summary>
        /// <param name="axiom">The initial starting point of the L-System.</param>
        public LSystem(List<T> axiom)
        {
            this.state = new List<T>();
            this.axiom = axiom;
            this.rules = new Dictionary<T, List<T>>();
        }

        /// <summary>
        /// Inserts the specified rule.
        /// </summary>
        /// <param name="symbol">Symbol.</param>
        /// <param name="production">Production.</param>
        public void InsertRule(T symbol, List<T> production)
        {
            this.rules.Add(symbol, production);
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
        /// </summary>
        public bool MoveNext()
        {
            // If the states list is empty, assign the axiom as the first state.
            if (this.state.Count == 0)
            {
                this.state = new List<T>(this.axiom);
                return true;
            }
            else
            {
                var nextState = new List<T>(this.state);
                var expanded = false;
                var i = 0;
                while (i < nextState.Count)
                {
                    var atom = nextState[i];
                    List<T> products;
                    if (this.rules.TryGetValue(atom, out products))
                    {
                        nextState.RemoveAt(i);
                        foreach (var product in products)
                        {
                            nextState.Insert(i, product);
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