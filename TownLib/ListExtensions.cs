using System.Collections.Generic;
using System.Linq;
using Town.Geom;

namespace Town
{
    public static class ListExtensions
    {
        public static void RemoveFirstIfPossible<T>(this List<T> list)
        {
            if (list.Any())
            {
                list.RemoveAt(0);
            }
        }

        public static List<Vector2> SmoothVertexList(this List<Vector2> list, float amount = 1f)
        {
            var v1 = list[0];
            var v2 = list[1];

            var newVertices = new List<Vector2> { list[0] };

            for (var v = 1; v < list.Count - 1; v++)
            {
                var v0 = v1;
                v1 = v2;
                v2 = list[v + 1];
                newVertices.Add(new Vector2((v0.x + v1.x * amount + v2.x) / (2 + amount), (v0.y + v1.y * amount + v2.y) / (2 + amount)));
            }

            newVertices.Add(list.Last());

            return newVertices;
        }

        public static List<Vector2> Complexify(this List<Vector2> list)
        {
            var newVertices = new List<Vector2> { list[0] };

            for (var v = 1; v < list.Count; v++)
            {
                var v1 = list[v - 1];
                var v2 = list[v];

                var middle = new Vector2((v1.x + v2.x) / 2f, (v1.y + v2.y) / 2f);
                newVertices.Add(middle);
                newVertices.Add(v2);
            }
            
            return newVertices;
        }
    }
}