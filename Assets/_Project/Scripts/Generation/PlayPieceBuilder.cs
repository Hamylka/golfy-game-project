using UnityEngine;

namespace IsoGolf.Generation
{
    /// <summary>Builds the ball and the flag as flat-shaded triangles, for the terrain's vertex color material.</summary>
    public static class PlayPieceBuilder
    {
        private const int PoleSides = 6;

        /// <summary>A faceted ball around the origin: a once-divided icosahedron, so it has 80 facets.</summary>
        public static TerrainMeshData BuildBall(PlayPieceSettings settings, PaletteSettings palette)
        {
            var mesh = new TerrainMeshData();
            Color32 color = TerrainBuilder.ToVertexColor(palette.Ball);
            float radius = settings.BallRadius;

            for (int i = 0; i < Icosahedron.Faces.Length; i += 3)
            {
                Vector3 a = Icosahedron.Corners[Icosahedron.Faces[i]].normalized;
                Vector3 b = Icosahedron.Corners[Icosahedron.Faces[i + 1]].normalized;
                Vector3 c = Icosahedron.Corners[Icosahedron.Faces[i + 2]].normalized;
                Vector3 ab = (a + b).normalized;
                Vector3 bc = (b + c).normalized;
                Vector3 ca = (c + a).normalized;

                AddOutward(mesh, a * radius, ab * radius, ca * radius, color);
                AddOutward(mesh, ab * radius, b * radius, bc * radius, color);
                AddOutward(mesh, ca * radius, bc * radius, c * radius, color);
                AddOutward(mesh, ab * radius, bc * radius, ca * radius, color);
            }

            return mesh;
        }

        /// <summary>
        /// A pole standing on the origin, with a pennant blowing toward -Z, which the camera sees side on and
        /// pointing to the right of the screen. The pole's foot sits at the origin, in the middle of the hole.
        /// </summary>
        public static TerrainMeshData BuildFlag(PlayPieceSettings settings, PaletteSettings palette)
        {
            var mesh = new TerrainMeshData();
            AddPole(mesh, settings, TerrainBuilder.ToVertexColor(palette.FlagPole));
            AddPennant(mesh, settings, TerrainBuilder.ToVertexColor(palette.Flag));
            return mesh;
        }

        private static void AddPole(TerrainMeshData mesh, PlayPieceSettings settings, Color32 color)
        {
            float radius = settings.PoleRadius;
            Vector3 top = Vector3.up * settings.PoleHeight;

            for (int i = 0; i < PoleSides; i++)
            {
                float start = i * Mathf.PI * 2f / PoleSides;
                float end = (i + 1) * Mathf.PI * 2f / PoleSides;
                var near = new Vector3(Mathf.Cos(start) * radius, 0f, Mathf.Sin(start) * radius);
                var far = new Vector3(Mathf.Cos(end) * radius, 0f, Mathf.Sin(end) * radius);
                Vector3 facing = near + far;

                mesh.Add(near, far, far + top, facing, color);
                mesh.Add(near, far + top, near + top, facing, color);
                mesh.Add(near + top, far + top, top, Vector3.up, color);
            }
        }

        private static void AddPennant(TerrainMeshData mesh, PlayPieceSettings settings, Color32 color)
        {
            float top = settings.PoleHeight;
            float bottom = top - settings.FlagHeight;
            float middle = (top + bottom) * 0.5f;

            var upper = new Vector3(0f, top, 0f);
            var lower = new Vector3(0f, bottom, 0f);
            var crease = new Vector3(settings.FlagFold, middle, 0f);
            var tip = new Vector3(0f, middle, -settings.FlagLength);

            // Each facet twice, once per side, because the terrain's material culls back faces.
            mesh.Add(upper, crease, tip, Vector3.left, color);
            mesh.Add(crease, lower, tip, Vector3.left, color);
            mesh.Add(upper, crease, tip, Vector3.right, color);
            mesh.Add(crease, lower, tip, Vector3.right, color);
        }

        private static void AddOutward(TerrainMeshData mesh, Vector3 a, Vector3 b, Vector3 c, Color32 color)
        {
            mesh.Add(a, b, c, a + b + c, color);
        }
    }
}
