using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IsoGolf.LookTest.Editor
{
    /// <summary>
    /// Throwaway: builds a hand-tuned low poly island to judge the look, not the future level generator.
    /// Values are hardcoded on purpose; none of this should survive the art direction decision.
    /// </summary>
    public static class LowPolyLookTestBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Sandbox_LowPoly.unity";
        private const string ArtFolder = "Assets/_Project/Art/LookTest";
        private const float CellSize = 1f;
        private const float FacetSize = 3.5f;
        private const int HalfCellsX = 19;
        private const int HalfCellsZ = 9;
        private const float CaptureAspect = 1280f / 2800f;

        private enum Surface
        {
            None,
            Rough,
            FairwayLight,
            FairwayMid,
            FairwayDark,
            Green,
            Sand,
            Water,
            Earth,
        }

        private static readonly Dictionary<Surface, string> SurfaceColors = new Dictionary<Surface, string>
        {
            { Surface.Rough, "#56713A" },
            { Surface.FairwayLight, "#8FA85A" },
            { Surface.FairwayMid, "#86A051" },
            { Surface.FairwayDark, "#7E9A48" },
            { Surface.Green, "#A7B86A" },
            { Surface.Sand, "#E8D9B5" },
            { Surface.Water, "#4FB0B8" },
            { Surface.Earth, "#BF6C43" },
        };

        private static Dictionary<string, Material> _materials;

        [MenuItem("Iso Golf/Look Test/Build Low Poly Island")]
        public static void Build()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
            {
                Debug.LogError($"Open {ScenePath} first.");
                return;
            }

            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }

            if (AssetDatabase.IsValidFolder(ArtFolder))
            {
                AssetDatabase.DeleteAsset(ArtFolder);
            }

            AssetDatabase.CreateFolder("Assets/_Project/Art", "LookTest");
            _materials = new Dictionary<string, Material>();

            var random = new System.Random(7);
            GameObject stripes = BuildTerrain("Terrain Stripes", false);
            GameObject facets = BuildTerrain("Terrain Facets", true);
            facets.SetActive(false);

            BuildProps(random);
            BuildLighting();
            BuildCamera(stripes, facets);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[LookTest] Island built.");
        }

        [MenuItem("Iso Golf/Look Test/Capture All States")]
        public static void CaptureAll()
        {
            LookTestCycler cycler = Object.FindAnyObjectByType<LookTestCycler>();
            Camera view = cycler.GetComponent<Camera>();
            var target = new RenderTexture(1280, 2800, GraphicsFormat.R8G8B8A8_SRGB, SystemInfo.GetGraphicsFormat(DefaultFormat.DepthStencil))
            {
                antiAliasing = 4,
            };
            target.Create();
            view.targetTexture = target;
            System.IO.Directory.CreateDirectory("Temp/Captures");

            for (int i = 0; i < cycler.StateCount; i++)
            {
                cycler.Apply(i);
                view.Render();
                view.Render();

                RenderTexture.active = target;
                var readback = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                readback.Apply();
                RenderTexture.active = null;
                System.IO.File.WriteAllBytes($"Temp/Captures/lowpoly-{i}.png", readback.EncodeToPNG());
                Object.DestroyImmediate(readback);
            }

            view.targetTexture = null;
            cycler.Apply(0);
            target.Release();
            Object.DestroyImmediate(target);
            Debug.Log("[LookTest] Captures written to Temp/Captures.");
        }

        private static float IslandHalfWidth(float x)
        {
            const float halfLength = 17f;
            float along = Mathf.Clamp01(1f - ((x / halfLength) * (x / halfLength)));
            float wobble = (Mathf.PerlinNoise((x * 0.18f) + 11f, 3.1f) - 0.5f) * 3.2f;
            return (5.2f + wobble) * Mathf.Sqrt(along);
        }

        private static float GroundHeight(float x, float z)
        {
            // Flat near the play axis, more broken toward the edges.
            float amplitude = Mathf.Lerp(0.05f, 0.4f, Mathf.Clamp01(Mathf.Abs(z) / 5f));
            return (Mathf.PerlinNoise((x * 0.55f) + 40f, (z * 0.55f) + 9f) - 0.5f) * amplitude * 2f;
        }

        // Fields are negative inside a surface. Triangles are cut exactly along the zero line,
        // so borders stay clean instead of following the triangle grid as sawteeth.
        private static float IslandField(Vector3 p) => Mathf.Abs(p.z) - IslandHalfWidth(p.x);

        private static float WaterField(Vector3 p) => EllipseField(p, -1.5f, 3.4f, 3.2f, 1.6f, 0.25f);

        private static float SandField(Vector3 p) => EllipseField(p, 9.4f, -2.9f, 1.8f, 1.1f, 0.2f);

        private static float GreenField(Vector3 p) => EllipseField(p, 12.8f, 0.2f, 3f, 2.7f, 0.12f);

        private static float FairwayField(Vector3 p)
        {
            float halfWidth = 2.5f + ((Mathf.PerlinNoise((p.x * 0.2f) + 70f, 1.3f) - 0.5f) * 1.6f);
            float across = Mathf.Abs(p.z) - halfWidth;
            float along = Mathf.Max(-15.5f - p.x, p.x - 10.5f);
            return Mathf.Max(across, along);
        }

        private static float EllipseField(Vector3 p, float centerX, float centerZ, float radiusX, float radiusZ, float wobble)
        {
            float dx = (p.x - centerX) / radiusX;
            float dz = (p.z - centerZ) / radiusZ;
            float noise = (Mathf.PerlinNoise((p.x * 0.8f) + centerX + 30f, (p.z * 0.8f) + centerZ + 30f) - 0.5f) * wobble * 2f;
            return Mathf.Sqrt((dx * dx) + (dz * dz)) - 1f + noise;
        }

        private readonly struct Triangle
        {
            public Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                A = a;
                B = b;
                C = c;
            }

            public Vector3 A { get; }
            public Vector3 B { get; }
            public Vector3 C { get; }
            public Vector3 Centroid => (A + B + C) / 3f;
        }

        private static void Split(Triangle triangle, System.Func<Vector3, float> field, List<Triangle> inside, List<Triangle> outside, List<(Vector3, Vector3)> border)
        {
            Vector3[] corners = { triangle.A, triangle.B, triangle.C };
            var inPolygon = new List<Vector3>(4);
            var outPolygon = new List<Vector3>(4);
            var crossings = new List<Vector3>(2);

            for (int i = 0; i < 3; i++)
            {
                Vector3 p = corners[i];
                Vector3 q = corners[(i + 1) % 3];
                float fp = field(p);
                float fq = field(q);
                (fp < 0f ? inPolygon : outPolygon).Add(p);

                if ((fp < 0f) != (fq < 0f))
                {
                    Vector3 crossing = Vector3.Lerp(p, q, fp / (fp - fq));
                    inPolygon.Add(crossing);
                    outPolygon.Add(crossing);
                    crossings.Add(crossing);
                }
            }

            Fan(inPolygon, inside);
            Fan(outPolygon, outside);
            if (crossings.Count == 2 && border != null)
            {
                border.Add((crossings[0], crossings[1]));
            }
        }

        private static void Fan(List<Vector3> polygon, List<Triangle> output)
        {
            for (int i = 1; output != null && i + 1 < polygon.Count; i++)
            {
                output.Add(new Triangle(polygon[0], polygon[i], polygon[i + 1]));
            }
        }

        private static List<Triangle> SplitAlongLines(List<Triangle> triangles, System.Func<Vector3, float> coordinate, float spacing)
        {
            // Cuts along parallel lines so a tone change never follows the triangle grid.
            var result = new List<Triangle>();
            foreach (Triangle triangle in triangles)
            {
                float a = coordinate(triangle.A);
                float b = coordinate(triangle.B);
                float c = coordinate(triangle.C);
                int first = Mathf.CeilToInt(Mathf.Min(a, b, c) / spacing);
                int last = Mathf.FloorToInt(Mathf.Max(a, b, c) / spacing);
                var pieces = new List<Triangle> { triangle };

                for (int k = first; k <= last; k++)
                {
                    float line = k * spacing;
                    var next = new List<Triangle>();
                    foreach (Triangle piece in pieces)
                    {
                        Split(piece, p => coordinate(p) - line, next, next, null);
                    }

                    pieces = next;
                }

                result.AddRange(pieces);
            }

            return result;
        }

        private static Surface StripeTone(float z)
        {
            // Long mowing stripes along the play axis.
            return Mathf.FloorToInt(z + 20f) % 2 == 0 ? Surface.FairwayLight : Surface.FairwayDark;
        }

        private static Surface FacetTone(Vector3 p)
        {
            // Large triangles with a little tone variation, instead of a repeating pattern.
            int cellX = Mathf.FloorToInt(p.x / FacetSize);
            int cellZ = Mathf.FloorToInt(p.z / FacetSize);
            int diagonal = Mathf.FloorToInt((p.x + p.z) / FacetSize);
            int hash = (cellX * 73856093) ^ (cellZ * 19349663) ^ (diagonal * 83492791);
            switch (Mathf.Abs(hash) % 3)
            {
                case 0: return Surface.FairwayLight;
                case 1: return Surface.FairwayMid;
                default: return Surface.FairwayDark;
            }
        }

        private static void AddWall(Dictionary<Surface, List<Vector3>> triangles, Vector3 top0, Vector3 top1, Vector3 low0, Vector3 low1, Vector3 facing)
        {
            Emit(triangles, Surface.Earth, top0, top1, low1, facing);
            Emit(triangles, Surface.Earth, top0, low1, low0, facing);
        }

        private static GameObject BuildTerrain(string name, bool facets)
        {
            const float waterLevel = -0.22f;
            var triangles = new Dictionary<Surface, List<Vector3>>();

            Vector3 Corner(int ix, int iz)
            {
                float x = ix * CellSize;
                float z = iz * CellSize;
                return new Vector3(x, GroundHeight(x, z), z);
            }

            void AddGround(Triangle triangle)
            {
                var land = new List<Triangle>();
                var islandBorder = new List<(Vector3, Vector3)>();
                Split(triangle, IslandField, land, null, islandBorder);

                foreach ((Vector3 p, Vector3 q) in islandBorder)
                {
                    // An uneven earth skirt under the island edge.
                    Vector3 middle = (p + q) * 0.5f;
                    Vector3 across = Vector3.Cross((q - p).normalized, Vector3.up);
                    Vector3 outward = IslandField(middle + (across * 0.02f)) > IslandField(middle) ? across : -across;
                    float depthP = 0.9f + (Mathf.PerlinNoise((p.x * 0.7f) + 5f, (p.z * 0.7f) + 5f) * 0.9f);
                    float depthQ = 0.9f + (Mathf.PerlinNoise((q.x * 0.7f) + 5f, (q.z * 0.7f) + 5f) * 0.9f);
                    AddWall(triangles, p, q, new Vector3(p.x, -depthP, p.z), new Vector3(q.x, -depthQ, q.z), outward);
                }

                foreach (Triangle piece in land)
                {
                    var water = new List<Triangle>();
                    var dry = new List<Triangle>();
                    var bank = new List<(Vector3, Vector3)>();
                    Split(piece, WaterField, water, dry, bank);

                    foreach (Triangle wet in water)
                    {
                        Vector3 Lower(Vector3 v) => new Vector3(v.x, waterLevel, v.z);
                        Emit(triangles, Surface.Water, Lower(wet.A), Lower(wet.B), Lower(wet.C), Vector3.up);
                    }

                    foreach ((Vector3 start, Vector3 end) in bank)
                    {
                        Vector3 middle = (start + end) * 0.5f;
                        Vector3 across = Vector3.Cross((end - start).normalized, Vector3.up);
                        Vector3 toWater = WaterField(middle + (across * 0.02f)) < WaterField(middle) ? across : -across;
                        AddWall(triangles, start, end, new Vector3(start.x, waterLevel, start.z), new Vector3(end.x, waterLevel, end.z), toWater);
                    }

                    foreach (Triangle ground in dry)
                    {
                        AddDry(ground);
                    }
                }
            }

            void AddDry(Triangle ground)
            {
                var sand = new List<Triangle>();
                var notSand = new List<Triangle>();
                Split(ground, SandField, sand, notSand, null);
                sand.ForEach(t => Emit(triangles, Surface.Sand, t.A, t.B, t.C, Vector3.up));

                foreach (Triangle first in notSand)
                {
                    var green = new List<Triangle>();
                    var notGreen = new List<Triangle>();
                    Split(first, GreenField, green, notGreen, null);
                    green.ForEach(t => Emit(triangles, Surface.Green, t.A, t.B, t.C, Vector3.up));

                    foreach (Triangle second in notGreen)
                    {
                        var fairway = new List<Triangle>();
                        var rough = new List<Triangle>();
                        Split(second, FairwayField, fairway, rough, null);
                        rough.ForEach(t => Emit(triangles, Surface.Rough, t.A, t.B, t.C, Vector3.up));

                        if (facets)
                        {
                            fairway = SplitAlongLines(fairway, v => v.x, FacetSize);
                            fairway = SplitAlongLines(fairway, v => v.z, FacetSize);
                            fairway = SplitAlongLines(fairway, v => v.x + v.z, FacetSize);
                        }
                        else
                        {
                            fairway = SplitAlongLines(fairway, v => v.z, 1f);
                        }

                        foreach (Triangle piece in fairway)
                        {
                            Vector3 center = piece.Centroid;
                            Emit(triangles, facets ? FacetTone(center) : StripeTone(center.z), piece.A, piece.B, piece.C, Vector3.up);
                        }
                    }
                }
            }

            for (int ix = -HalfCellsX; ix < HalfCellsX; ix++)
            {
                for (int iz = -HalfCellsZ; iz < HalfCellsZ; iz++)
                {
                    Vector3 a = Corner(ix, iz);
                    Vector3 b = Corner(ix, iz + 1);
                    Vector3 c = Corner(ix + 1, iz + 1);
                    Vector3 d = Corner(ix + 1, iz);

                    // Alternating diagonals, so the ground reads as facets rather than rows.
                    if ((ix + iz) % 2 == 0)
                    {
                        AddGround(new Triangle(a, b, c));
                        AddGround(new Triangle(a, c, d));
                    }
                    else
                    {
                        AddGround(new Triangle(a, b, d));
                        AddGround(new Triangle(b, c, d));
                    }
                }
            }

            var root = new GameObject(name);
            foreach (KeyValuePair<Surface, List<Vector3>> pair in triangles)
            {
                Mesh mesh = ToMesh(pair.Value);
                SaveMesh(mesh, $"{name} {pair.Key}");
                AddMeshObject(pair.Key.ToString(), root.transform, mesh, MaterialFor(SurfaceColors[pair.Key]), Vector3.zero, Quaternion.identity, Vector3.one);
            }

            return root;
        }

        private static void Emit(Dictionary<Surface, List<Vector3>> triangles, Surface surface, Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
        {
            if (!triangles.TryGetValue(surface, out List<Vector3> list))
            {
                list = new List<Vector3>();
                triangles[surface] = list;
            }

            AddFacing(list, a, b, c, facing);
        }

        private static void AddFacing(List<Vector3> list, Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
        {
            // On a front face in Unity, this cross product points toward the viewer.
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f)
            {
                list.Add(a);
                list.Add(c);
                list.Add(b);
            }
            else
            {
                list.Add(a);
                list.Add(b);
                list.Add(c);
            }
        }

        private static Mesh ToMesh(List<Vector3> positions)
        {
            // No shared vertices, so every triangle gets its own flat normal.
            var indices = new int[positions.Count];
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
            }

            var mesh = new Mesh { indexFormat = positions.Count > 60000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(positions);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void SaveMesh(Mesh mesh, string name)
        {
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, $"{ArtFolder}/{name}.asset");
        }

        private static Material MaterialFor(string hex)
        {
            if (_materials.TryGetValue(hex, out Material material))
            {
                return material;
            }

            ColorUtility.TryParseHtmlString(hex, out Color color);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0f);
            material.SetFloat("_SpecularHighlights", 0f);
            material.SetFloat("_EnvironmentReflections", 0f);
            AssetDatabase.CreateAsset(material, $"{ArtFolder}/{hex.Substring(1)}.mat");
            _materials[hex] = material;
            return material;
        }

        private static GameObject AddMeshObject(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static Mesh Blob(System.Random random, string name, float jitter)
        {
            // A subdivided-once icosahedron, with its corners pushed around so each blob looks hand cut.
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new List<Vector3>
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            var midpoints = new Dictionary<(int, int), int>();
            int Mid(int a, int b)
            {
                (int, int) key = a < b ? (a, b) : (b, a);
                if (!midpoints.TryGetValue(key, out int index))
                {
                    index = v.Count;
                    v.Add((v[a].normalized + v[b].normalized) * 0.5f);
                    midpoints[key] = index;
                }

                return index;
            }

            var subdivided = new List<int>();
            for (int i = 0; i < faces.Length; i += 3)
            {
                int a = faces[i];
                int b = faces[i + 1];
                int c = faces[i + 2];
                int ab = Mid(a, b);
                int bc = Mid(b, c);
                int ca = Mid(c, a);
                subdivided.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
            }

            for (int i = 0; i < v.Count; i++)
            {
                float push = 1f + (((float)random.NextDouble() - 0.5f) * jitter);
                v[i] = v[i].normalized * push;
            }

            var positions = new List<Vector3>();
            for (int i = 0; i < subdivided.Count; i += 3)
            {
                Vector3 a = v[subdivided[i]];
                Vector3 b = v[subdivided[i + 1]];
                Vector3 c = v[subdivided[i + 2]];
                AddFacing(positions, a, b, c, (a + b + c).normalized);
            }

            Mesh mesh = ToMesh(positions);
            SaveMesh(mesh, name);
            return mesh;
        }

        private static Mesh Cone(string name, int sides)
        {
            var positions = new List<Vector3>();
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = Ring(i, sides);
                Vector3 b = Ring(i + 1, sides);
                AddFacing(positions, Vector3.up, a, b, a + b);
                AddFacing(positions, Vector3.zero, a, b, Vector3.down);
            }

            Mesh mesh = ToMesh(positions);
            SaveMesh(mesh, name);
            return mesh;
        }

        private static Mesh Prism(string name, int sides)
        {
            var positions = new List<Vector3>();
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = Ring(i, sides);
                Vector3 b = Ring(i + 1, sides);
                Vector3 up = Vector3.up;
                AddFacing(positions, a, b, b + up, a + b);
                AddFacing(positions, a, b + up, a + up, a + b);
                AddFacing(positions, up, a + up, b + up, Vector3.up);
                AddFacing(positions, Vector3.zero, a, b, Vector3.down);
            }

            Mesh mesh = ToMesh(positions);
            SaveMesh(mesh, name);
            return mesh;
        }

        private static Mesh GableRoof(string name)
        {
            var positions = new List<Vector3>();
            var a = new Vector3(-0.5f, 0f, -0.5f);
            var b = new Vector3(0.5f, 0f, -0.5f);
            var c = new Vector3(0.5f, 0f, 0.5f);
            var d = new Vector3(-0.5f, 0f, 0.5f);
            var e = new Vector3(-0.5f, 1f, 0f);
            var f = new Vector3(0.5f, 1f, 0f);
            AddFacing(positions, a, b, f, new Vector3(0f, 1f, -1f));
            AddFacing(positions, a, f, e, new Vector3(0f, 1f, -1f));
            AddFacing(positions, d, c, f, new Vector3(0f, 1f, 1f));
            AddFacing(positions, d, f, e, new Vector3(0f, 1f, 1f));
            AddFacing(positions, a, d, e, Vector3.left);
            AddFacing(positions, b, c, f, Vector3.right);
            Mesh mesh = ToMesh(positions);
            SaveMesh(mesh, name);
            return mesh;
        }

        private static Vector3 Ring(int index, int sides)
        {
            float angle = index * Mathf.PI * 2f / sides;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        private static void BuildProps(System.Random random)
        {
            var root = new GameObject("Props").transform;
            var blobs = new[] { Blob(random, "Blob A", 0.3f), Blob(random, "Blob B", 0.35f), Blob(random, "Blob C", 0.25f) };
            Mesh cone = Cone("Cone", 7);
            Mesh trunk = Prism("Trunk", 6);
            Mesh box = Prism("Box", 4);
            Mesh roof = GableRoof("Roof");

            Material leaf = MaterialFor("#56713A");
            Material darkLeaf = MaterialFor("#34482A");
            Material wood = MaterialFor("#6A4128");
            Material stone = MaterialFor("#DAD3C3");
            Material whiteStone = MaterialFor("#F1ECE0");
            Material tile = MaterialFor("#B2663F");
            Material flag = MaterialFor("#C8423B");
            Material ballWhite = MaterialFor("#F2EEE6");

            Vector3 OnGround(float x, float z) => new Vector3(x, GroundHeight(x, z), z);

            void Olive(float x, float z, float size)
            {
                Vector3 p = OnGround(x, z);
                AddMeshObject("Olive Trunk", root, trunk, wood, p, Quaternion.identity, new Vector3(0.14f, 1.1f, 0.14f) * size);
                for (int i = 0; i < 4; i++)
                {
                    var offset = new Vector3(((float)random.NextDouble() - 0.5f) * 1.1f, 1.3f + ((float)random.NextDouble() * 0.7f), ((float)random.NextDouble() - 0.5f) * 1.1f) * size;
                    float s = (0.55f + ((float)random.NextDouble() * 0.3f)) * size;
                    Quaternion spin = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                    AddMeshObject("Olive Leaves", root, blobs[i % blobs.Length], leaf, p + offset, spin, Vector3.one * s);
                }
            }

            void Cypress(float x, float z, float size)
            {
                Vector3 p = OnGround(x, z);
                AddMeshObject("Cypress Trunk", root, trunk, wood, p, Quaternion.identity, new Vector3(0.09f, 0.4f, 0.09f) * size);
                Quaternion spin = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                AddMeshObject("Cypress Leaves", root, cone, darkLeaf, p + (Vector3.up * 0.3f * size), spin, new Vector3(0.42f, 3f, 0.42f) * size);
            }

            void Rock(float x, float z, float size, Material material)
            {
                Quaternion spin = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                AddMeshObject("Rock", root, blobs[random.Next(blobs.Length)], material, OnGround(x, z), spin, new Vector3(1f, 0.65f, 0.85f) * size);
            }

            // Clusters at the edges, with the shot line kept clear.
            Olive(-11f, 3.6f, 1.15f);
            Olive(-9.4f, 4.1f, 0.9f);
            Olive(-10f, -3.8f, 1f);
            Olive(3.5f, -3.8f, 1.2f);
            Olive(5.2f, -3.3f, 0.9f);
            Cypress(5f, 4.1f, 1f);
            Cypress(5.9f, 3.7f, 1.2f);
            Cypress(6.9f, 4f, 0.9f);
            Cypress(-4.5f, -4.4f, 1.1f);
            Cypress(-3.6f, -4.7f, 0.85f);
            Cypress(15f, 2.1f, 1.1f);
            Olive(15.3f, -1.5f, 0.75f);

            Rock(-3.6f, 2.5f, 0.45f, whiteStone);
            Rock(0.3f, 4.6f, 0.55f, whiteStone);
            Rock(0.9f, 2.5f, 0.3f, whiteStone);
            Rock(-13.5f, -2.8f, 0.5f, stone);

            // A small shed and a low wall give the scale.
            Vector3 shed = OnGround(9.5f, 3.6f);
            AddMeshObject("Shed Walls", root, box, stone, shed - (Vector3.up * 0.1f), Quaternion.Euler(0f, 45f, 0f), new Vector3(1.35f, 1.4f, 1.35f));
            AddMeshObject("Shed Roof", root, roof, tile, shed + (Vector3.up * 1.3f), Quaternion.identity, new Vector3(2.2f, 0.8f, 2.2f));
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = OnGround(-7f + (i * 0.72f), -4.5f + (i * 0.04f));
                AddMeshObject("Wall Stone", root, box, stone, p, Quaternion.Euler(0f, 45f + (i * 3f), 0f), new Vector3(0.48f, 0.42f, 0.34f));
            }

            // Flag, ball and a preview arc, to judge how the shot reads in this camera.
            Vector3 hole = OnGround(13.5f, 0.2f);
            AddMeshObject("Flag Pole", root, trunk, whiteStone, hole, Quaternion.identity, new Vector3(0.035f, 1.5f, 0.035f));
            AddMeshObject("Flag", root, box, flag, hole + new Vector3(0f, 1.15f, 0.22f), Quaternion.Euler(0f, 45f, 0f), new Vector3(0.28f, 0.3f, 0.03f));

            Vector3 tee = OnGround(-13.5f, -0.3f) + (Vector3.up * 0.1f);
            Vector3 landing = OnGround(1f, 0.4f) + (Vector3.up * 0.1f);
            AddMeshObject("Ball", root, blobs[2], ballWhite, tee, Quaternion.identity, Vector3.one * 0.13f);
            for (int i = 1; i < 26; i++)
            {
                float t = i / 26f;
                Vector3 point = Vector3.Lerp(tee, landing, t) + (Vector3.up * 4f * t * (1f - t) * 3.5f);
                GameObject dot = AddMeshObject("Arc Dot", root, blobs[2], ballWhite, point, Quaternion.identity, Vector3.one * 0.07f);
                dot.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static void BuildLighting()
        {
            var sunObject = new GameObject("Sun");

            // Sun from screen top-left for the leaning camera below, so shadows fall toward bottom-right.
            sunObject.transform.rotation = Quaternion.Euler(52f, 215f, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            ColorUtility.TryParseHtmlString("#FFF1D6", out Color color);
            sun.color = color;
            sun.intensity = 1.05f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;

            RenderSettings.skybox = null;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.72f, 0.68f, 0.66f);
            RenderSettings.fog = false;
        }

        private static void BuildCamera(GameObject stripes, GameObject facets)
        {
            var cameraObject = new GameObject("Look Test Camera") { tag = "MainCamera" };
            var view = cameraObject.AddComponent<Camera>();
            view.orthographic = false;
            view.nearClipPlane = 1f;
            view.farClipPlane = 400f;
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = Color.white;
            view.allowHDR = false;
            view.allowMSAA = true;
            cameraObject.AddComponent<AudioListener>();
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.SetRenderer(0);

            var cycler = cameraObject.AddComponent<LookTestCycler>();
            var so = new SerializedObject(cycler);
            so.FindProperty("stripesTerrain").objectReferenceValue = stripes;
            so.FindProperty("facetsTerrain").objectReferenceValue = facets;
            SerializedProperty states = so.FindProperty("states");

            (string label, float fov, float pitch, bool facetsOn)[] setups =
            {
                ("Long lens 22, stripes", 22f, 46f, false),
                ("Medium lens 32, stripes", 32f, 44f, false),
                ("Wide lens 45, stripes", 45f, 40f, false),
                ("Medium lens 32, facets", 32f, 44f, true),
            };

            states.arraySize = setups.Length;
            for (int i = 0; i < setups.Length; i++)
            {
                // Yaw 80 instead of 90 is the slight lean: the course runs up and a little to the right.
                Quaternion rotation = Quaternion.Euler(setups[i].pitch, 80f, 0f);
                Vector3 position = FitIsland(rotation, setups[i].fov);
                SerializedProperty state = states.GetArrayElementAtIndex(i);
                state.FindPropertyRelative("Label").stringValue = setups[i].label;
                state.FindPropertyRelative("Position").vector3Value = position;
                state.FindPropertyRelative("Euler").vector3Value = rotation.eulerAngles;
                state.FindPropertyRelative("FieldOfView").floatValue = setups[i].fov;
                state.FindPropertyRelative("Facets").boolValue = setups[i].facetsOn;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Vector3 FitIsland(Quaternion rotation, float fieldOfView)
        {
            // Moves the camera until the whole island fills the portrait screen, centred.
            var bounds = new List<Vector3>();
            foreach (float x in new[] { -17f, -12f, -6f, 0f, 6f, 12f, 17f })
            {
                float halfWidth = IslandHalfWidth(x) + 0.5f;
                bounds.Add(new Vector3(x, -1.2f, -halfWidth));
                bounds.Add(new Vector3(x, -1.2f, halfWidth));
                bounds.Add(new Vector3(x, 3.5f, -halfWidth));
                bounds.Add(new Vector3(x, 3.5f, halfWidth));
            }

            float tan = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 position = rotation * (Vector3.back * 40f);
            for (int i = 0; i < 60; i++)
            {
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue, depth = 0f;
                foreach (Vector3 point in bounds)
                {
                    Vector3 v = Quaternion.Inverse(rotation) * (point - position);
                    float ndcX = v.x / (v.z * tan * CaptureAspect);
                    float ndcY = v.y / (v.z * tan);
                    minX = Mathf.Min(minX, ndcX);
                    maxX = Mathf.Max(maxX, ndcX);
                    minY = Mathf.Min(minY, ndcY);
                    maxY = Mathf.Max(maxY, ndcY);
                    depth += v.z / bounds.Count;
                }

                position += rotation * new Vector3(((minX + maxX) * 0.5f) * depth * tan * CaptureAspect, ((minY + maxY) * 0.5f) * depth * tan, 0f);
                float fill = Mathf.Max((maxX - minX) * 0.5f, (maxY - minY) * 0.5f) / 0.94f;
                position -= rotation * Vector3.forward * (depth * (fill - 1f) * 0.5f);
            }

            return position;
        }
    }
}
