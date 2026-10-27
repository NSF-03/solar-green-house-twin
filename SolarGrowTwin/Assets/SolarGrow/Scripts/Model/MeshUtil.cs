using System.Collections.Generic;
using UnityEngine;

namespace SolarGrow
{
    /// <summary>
    /// Builds simple meshes in code: flat convex faces and thin slabs (glass panes) with outward-facing
    /// triangles. Unity treats a triangle (a, b, c) as front-facing on the side of Cross(b - a, c - a).
    /// </summary>
    public static class MeshUtil
    {
        /// <summary>Name prefix of meshes made here; the builder destroys them on rebuild.</summary>
        public const string GeneratedPrefix = "SolarGrowGen:";

        /// <summary>Area-weighted normal of a convex polygon (fan from point 0); zero if degenerate.</summary>
        public static Vector3 PolygonNormal(IList<Vector3> poly)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 1; i < poly.Count - 1; i++)
                sum += Vector3.Cross(poly[i] - poly[0], poly[i + 1] - poly[0]);
            return sum;
        }

        static Vector3 Centroid(IList<Vector3> poly)
        {
            Vector3 c = Vector3.zero;
            for (int i = 0; i < poly.Count; i++) c += poly[i];
            return poly.Count > 0 ? c / poly.Count : c;
        }

        /// <summary>Add one flat convex face whose front side points along 'outward'.</summary>
        public static void AddFace(List<Vector3> verts, List<int> tris, IList<Vector3> poly, Vector3 outward)
        {
            int n = poly.Count;
            if (n < 3) return;
            bool flip = Vector3.Dot(PolygonNormal(poly), outward) < 0f;
            int b = verts.Count;
            for (int i = 0; i < n; i++) verts.Add(poly[i]);
            for (int i = 1; i < n - 1; i++)
            {
                tris.Add(b);
                if (flip) { tris.Add(b + i + 1); tris.Add(b + i); }
                else { tris.Add(b + i); tris.Add(b + i + 1); }
            }
        }

        /// <summary>Add a closed slab of 'thickness' centred on the convex polygon; its outer face points along 'outwardHint'.</summary>
        public static void AddSlab(List<Vector3> verts, List<int> tris, IList<Vector3> poly, float thickness, Vector3 outwardHint)
        {
            int n = poly.Count;
            if (n < 3) return;
            Vector3 normal = PolygonNormal(poly);
            if (normal.sqrMagnitude < 1e-12f) return;
            normal.Normalize();
            if (Vector3.Dot(normal, outwardHint) < 0f) normal = -normal;
            Vector3 half = normal * (thickness * 0.5f);
            Vector3 centre = Centroid(poly);

            Vector3[] front = new Vector3[n];
            Vector3[] back = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                front[i] = poly[i] + half;
                back[i] = poly[i] - half;
            }
            AddFace(verts, tris, front, normal);
            AddFace(verts, tris, back, -normal);

            Vector3[] quad = new Vector3[4];
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector3 mid = (poly[i] + poly[j]) * 0.5f;
                Vector3 side = mid - centre;
                side -= normal * Vector3.Dot(side, normal);
                quad[0] = front[i];
                quad[1] = front[j];
                quad[2] = back[j];
                quad[3] = back[i];
                AddFace(verts, tris, quad, side);
            }
        }

        public static Mesh Build(string name, List<Vector3> verts, List<int> tris)
        {
            Mesh mesh = new Mesh();
            mesh.name = GeneratedPrefix + name;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
