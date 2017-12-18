﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Town.Geom;

namespace Town
{
    public class Town
    {
        public readonly TownOptions Options;
        public static readonly int Width = 1500;
        public static readonly int Height = 1500;
        private const int PatchMultiplier = 8;
        private readonly Rectangle _mapArea = new Rectangle(0, 0, Width, Height);

        public int NumPatches { get; }
        public Vector2 Center { get; }
        public List<Patch> Patches { get; }
        public Castle Castle { get; private set; }
        public Wall CityWall { get; private set; }
        public Patch Market { get; set; }

        public List<List<Vector2>> Roads { get; }
        public List<List<Vector2>> Streets { get; }
        public List<Vector2> Gates { get; }

        public List<Vector2> WaterBorder { get; }
        public List<Polygon> River { get; set; }

        public Town(TownOptions options)
        {
            Options = options;
            Rnd.Seed = options.Seed.Value;

            var ok = false;
            while (!ok)
            {

                NumPatches = options.Patches;
                Roads = new List<List<Vector2>>();
                Streets = new List<List<Vector2>>();
                Gates = new List<Vector2>();
                Center = new Vector2(Width / 2f * (1 + 0.1 * Rnd.NextDouble() - 0.1 * Rnd.NextDouble()), Height / 2f * (1 + 0.1 * Rnd.NextDouble() - 0.1 * Rnd.NextDouble()));
                Patches = new List<Patch>();
                WaterBorder = new List<Vector2>();
                River = new List<Polygon> {new Polygon(new Vector2(Int32.MaxValue, Int32.MaxValue))};

                try
                {
                    BuildPatches();
                    OptimizePatches();
                    BuildWalls();
                    BuildRoads();
                    PopulateTown();

                    ok = Roads.Count > 1;
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                }
            }
        }

        public Rectangle GetBounds()
        {
            var vertices = Patches.SelectMany(p => p.Shape.Vertices);
            var minX = vertices.Min(v => v.x);
            var maxX = vertices.Max(v => v.x);
            var minY = vertices.Min(v => v.y);
            var maxY = vertices.Max(v => v.y);

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        public Rectangle GetCityWallsBounds()
        {
            var vertices = Patches.Where(p => p.WithinCity).SelectMany(p => p.Shape.Vertices);
            var minX = vertices.Min(v => v.x);
            var maxX = vertices.Max(v => v.x);
            var minY = vertices.Min(v => v.y);
            var maxY = vertices.Max(v => v.y);

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        private void BuildPatches()
        {
            var points = GetVoronoiPoints(NumPatches, Center).ToList();

            var v = Voronoi.Build(points);

            for (var i = 0; i < 3; i++)
            {
                var toRelax = v.Points.Take(3).ToList();
                toRelax.Add(v.Points[NumPatches]);
                v = Voronoi.Relax(v, toRelax);
            }

            v = Voronoi.Relax(v);

            var comparer = new Vector2LengthComparer();
            v.Points.Sort(comparer);

            var regions = v.Partitioning();

            Patches.Clear();
            foreach (var region in regions)
            {
                Patches.Add(Patch.FromRegion(this, region));
            }

            var patchesInTown = Patches.OrderBy(p => (Center - p.Center).Length).Take(NumPatches).ToList();

            // Find random patch at the outside of town to place water
            if (Options.Water)
            {
                var firstWaterPatch = patchesInTown.Where(p => p.GetAllNeighbours().Any(n => !n.WithinCity))
                    .OrderBy(p => Rnd.NextDouble()).First();
                firstWaterPatch.Water = true;

                var waterDirection = Vector2.Normalize(firstWaterPatch.Center - Center);

                var toCheck = new List<Patch> { firstWaterPatch };
                while (toCheck.Any())
                {
                    var checking = toCheck[0];
                    toCheck.RemoveAt(0);

                    var waterPatches = checking.GetAllNeighbours().Except(patchesInTown)
                        .Where(n => Math.Abs((Center - n.Center).AngleComparedTo(waterDirection)) < Math.PI / 4)
                        .Where(n => !n.Water).ToList();
                    foreach (var waterPatch in waterPatches)
                    {
                        waterPatch.Water = true;
                        toCheck.Add(waterPatch);
                    }
                }

            }

            if (Options.River)
            {
                River.AddRange(CreateRiver());
            }

            patchesInTown = Patches.Where(p => !p.Water).OrderBy(p => (Center - p.Center).Length).Take(NumPatches).ToList();

            var numPatchesWithRiver = patchesInTown.Count(p => River.Any(r => r.OverlapsWith(p.Shape)));

            // Add space in town to accomodate for the river
            patchesInTown = Patches.Where(p => !p.Water).OrderBy(p => (Center - p.Center).Length).Take(NumPatches + numPatchesWithRiver).ToList();


            foreach (var patch in patchesInTown)
            {
                patch.WithinCity = true;
                patch.WithinWalls = true;
            }

            Castle = new Castle(patchesInTown.Last());

            Market = patchesInTown.First();

            var circumference = FindCircumference(patchesInTown);
            var smoothAmount = Math.Min(1f, 40f / circumference.Vertices.Count);
            if (Options.Walls)
            {
                SmoothPatches(circumference, smoothAmount);
            }

            if (Options.Water)
            {
                var waterCircumference = FindCircumference(Patches.Where(p => p.Water));
                SmoothPatches(waterCircumference, 0.2f);
                WaterBorder.Clear();
                WaterBorder.AddRange(waterCircumference.Vertices);
            }
        }

        private IEnumerable<Polygon> CreateRiver()
        {
            var river = new List<Vector2>();

            if (Options.Water)
            {
                var closestWater = Patches.Where(p => p.Water && p.GetAllNeighbours().All(n => n.Water)).OrderBy(p => (Center - p.Center).Length).First();
                var farthestWater = Patches.Where(p => p.Water).OrderByDescending(p => (Center - p.Center).Length).First();

                var riverStart = closestWater.Center;
                var riverDirection = Vector2.Normalize(Center - farthestWater.Center);

                var riverPoint = riverStart;
                river.Add(riverStart);

                while (_mapArea.Contains(riverPoint))
                {
                    riverPoint = riverPoint + Vector2.Scale(riverDirection, 30f);
                    river.Add(riverPoint);
                }
            }
            else
            {
                var riverStart = new Vector2(Center.x + Rnd.Next(100) - Rnd.Next(100), Center.y + Rnd.Next(100) - Rnd.Next(100));
                var riverDirection = Vector2.Normalize(new Vector2(Rnd.NextDouble() * Math.PI * 2));

                river.Add(riverStart);
                var riverPoint = riverStart;
                while (_mapArea.Contains(riverPoint))
                {
                    riverPoint = riverPoint + Vector2.Scale(riverDirection, 30f);
                    river.Add(riverPoint);
                }

                riverDirection = new Vector2(-riverDirection.x, -riverDirection.y);
                riverPoint = riverStart;
                while (_mapArea.Contains(riverPoint))
                {
                    riverPoint = riverPoint + Vector2.Scale(riverDirection, 30f);
                    river.Insert(0, riverPoint);
                }
            }

            var pointOffset = Vector2.Normalize(new Vector2(river[1].x - river[0].x, river[1].y - river[0].y).Rotate90());

            for (var i = 1; i < river.Count; i++)
            {
                river[i] = river[i] + Vector2.Scale(pointOffset, Rnd.Next(50) - Rnd.Next(50));
            }
            river = river.Complexify().SmoothVertexList(2);

            return RiverPolyFromPointList(river);
        }

        private IEnumerable<Polygon> RiverPolyFromPointList(List<Vector2> riverPoints)
        {
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            var width = (float)(12 + Rnd.NextDouble() * 7 - Rnd.NextDouble() * 7);

            var prev = riverPoints[0];

            for (var i = 1; i < riverPoints.Count; i++)
            {
                var point = riverPoints[i];
                var line = (point - prev).Normalize().Scale(width);
                var leftWidth = line.Rotate90();
                var rightWidth = new Vector2(-leftWidth.x, -leftWidth.y);

                left.Add(prev + rightWidth);
                right.Add(prev + leftWidth);

                prev = point;
            }
            
            right = right.Complexify().SmoothVertexList(3);
            left = left.Complexify().SmoothVertexList(3);

            for (var i = 0; i < left.Count - 1; i++)
            {
                yield return new Polygon(left[i], right[i], right[i+1], left[i+1]);
            }
        }

        private void SmoothPatches(Polygon vertices, float smoothAmount)
        {
            for (var i = 0; i < vertices.Vertices.Count; i++)
            {
                var point = vertices.Vertices[i];
                var prev = vertices.GetPreviousVertex(point);
                var next = vertices.GetNextVertex(point);
                var smoothed = Vector2.SmoothVertex(point, prev, next, smoothAmount);

                var affected = Patches.Where(p => p.Shape.Vertices.Contains(point)).ToList();
                foreach (var patch in affected)
                {
                    patch.Shape.ReplaceVertex(point, smoothed);
                }

                vertices.Vertices[i] = smoothed;
            }
        }

        private void OptimizePatches()
        {
            var patchesToOptimize = Patches.Where(p => p.WithinCity).ToList();
            var shapesToClean = new List<Polygon>();

            foreach (var patch in patchesToOptimize)
            {
                for (var i = 0; i < patch.Shape.Vertices.Count; i++)
                {
                    var p0 = patch.Shape.Vertices[i];
                    var p1 = patch.Shape.Vertices[(i + 1) % patch.Shape.Vertices.Count];

                    if (p0 != p1 && (p0 - p1).Length < 8)
                    {
                        var shapesWithPoint = Patches.Where(p => p.Shape.Vertices.Contains(p1)).Select(p => p.Shape);

                        var newP0 = Vector2.Scale(p0 + p1, 0.5f);

                        foreach (var polygon in shapesWithPoint)
                        {
                            shapesToClean.Add(polygon);
                            polygon.ReplaceVertex(p1, newP0);
                        }

                    }
                }
            }

            foreach (var polygon in shapesToClean.Distinct())
            {
                var optimizedVertices = polygon.Vertices.Distinct().ToList();
                polygon.Vertices.Clear();
                polygon.Vertices.AddRange(optimizedVertices);
            }

        }

        private void BuildWalls()
        {
            var circumference = FindCircumference(Patches.Where(p => p.WithinCity)).Vertices;
            var allowedTowerPositions = circumference.ToList();
            var castleVertices = Castle.Patch.Shape.Vertices.ToList();

            foreach (var position in allowedTowerPositions.Intersect(WaterBorder).ToList())
            {
                var neighbours = Patches.Where(p => p.Shape.Vertices.Contains(position));
                if (!neighbours.Any(n => !n.WithinCity && !n.Water))
                {
                    allowedTowerPositions.Remove(position);
                }
            }

            // Make sure the wall starts and ends at the sea
            if (Options.Water)
            {
                var seaPoints = allowedTowerPositions.Where(t => Patches.Any(p => p.Water && p.Shape.Vertices.Contains(t))).ToList();

                var lastSeaPoint = seaPoints[1];
                var firstSeaPoint = seaPoints[0];

                var minIndex = Math.Min(allowedTowerPositions.IndexOf(lastSeaPoint), allowedTowerPositions.IndexOf(firstSeaPoint));
                var maxIndex = Math.Max(allowedTowerPositions.IndexOf(lastSeaPoint), allowedTowerPositions.IndexOf(firstSeaPoint));

                if (minIndex != 0 && maxIndex != allowedTowerPositions.Count - 1)
                {
                    var seaPointIndex = allowedTowerPositions.IndexOf(lastSeaPoint);

                    while (seaPointIndex > 0)
                    {
                        var point = allowedTowerPositions[0];
                        allowedTowerPositions.RemoveAt(0);
                        allowedTowerPositions.Add(point);
                        seaPointIndex = allowedTowerPositions.IndexOf(lastSeaPoint);
                    }
                }
            }

            CityWall = new Wall(allowedTowerPositions, this, 2, 10, castleVertices);

            var allowedCastleWallPositions = castleVertices.Except(allowedTowerPositions).ToList();
            var firstIndex = castleVertices.IndexOf(allowedCastleWallPositions.First()) - 1;
            if (firstIndex < 0)
            {
                firstIndex = castleVertices.Count - 1;
            }
            var lastIndex = castleVertices.IndexOf(allowedCastleWallPositions.Last()) + 1;
            if (lastIndex > castleVertices.Count - 1)
            {
                lastIndex = 0;
            }
            allowedCastleWallPositions.Insert(0, castleVertices[firstIndex]);
            allowedCastleWallPositions.Add(castleVertices[lastIndex]);

            Castle.Wall = new Wall(allowedCastleWallPositions, this, 1, 1, circumference);
        }

        private void BuildRoads()
        {
            var topology = new Topology(this);

            var roads = new List<List<Vector2>>();
            var streets = new List<List<Vector2>>();

            foreach (var gate in Gates.ToList())
            {
                var endPoint = Market.Shape.Vertices.OrderBy(v => (gate - v).Length).First();

                var street = topology.BuildPath(gate, endPoint, topology.Outer);
                if (street != null)
                {
                    streets.Add(street);

                    if (CityWall.Gates.Contains(gate))
                    {
                        var direction = Vector2.Scale(gate - Center, 100000);

                        var start = topology.Node2V.Values.OrderBy(v => (v - direction).Length).First();

                        var road = topology.BuildPath(start, gate, topology.Inner);
                        if (road == null)
                        {
                            CityWall.Gates.Remove(gate);
                            CityWall.Towers.Add(gate);
                            Gates.Remove(gate);
                        }
                        else
                        {
                            roads.Add(road);
                        }
                    }
                }
            }

            if (!roads.Any())
            {
                throw new InvalidOperationException("No roads into the town");
            }

            Roads.AddRange(TidyUpRoads(roads));
            Streets.AddRange(TidyUpRoads(streets));

            foreach (var road in Roads)
            {
                var insideVertices = road.Where(rv => Patches.Where(p => p.Shape.ContainsPoint(rv)).All(p => p.WithinCity)).Except(Gates).ToList();
                foreach (var insideVertex in insideVertices)
                {
                    road.Remove(insideVertex);
                }
            }
        }

        private List<List<Vector2>> TidyUpRoads(List<List<Vector2>> roads)
        {
            var roadEdges = roads.SelectMany(RoadToEdges).Distinct().ToList();

            var road = new List<List<Vector2>>();

            while (roadEdges.Any())
            {
                var edge = roadEdges[0];
                roadEdges.RemoveAt(0);

                var attached = false;
                foreach (var artery in road)
                {
                    if (artery[0].Equals(edge.B))
                    {
                        artery.Insert(0, edge.A);
                        attached = true;
                        break;
                    }

                    if (artery.Last().Equals(edge.A))
                    {
                        artery.Add(edge.B);
                        attached = true;
                        break;
                    }
                }

                if (!attached)
                {
                    road.Add(new List<Vector2> { edge.A, edge.B });
                }
            }

            var smoothed = new List<List<Vector2>>();

            foreach (var tidyRoad in road)
            {
                var smoothedRoad = tidyRoad.SmoothVertexList(3);

                for (var i = 0; i < tidyRoad.Count; i++)
                {
                    var originalPoint = tidyRoad[i];

                    if (PointOverWater(originalPoint))
                    {
                        // Extra smoothing for bridges
                        var p0 = smoothedRoad[i > 0 ? i-1: smoothedRoad.Count-1];
                        var p1 = smoothedRoad[i];
                        var p2 = smoothedRoad[i + 1 % smoothedRoad.Count];

                        var s = Vector2.SmoothVertex(p1, p0, p2, 10f);
                        smoothedRoad[i] = s;
                    }

                    var smoothedPoint = smoothedRoad[i];
                    var affectedPatches = Patches.Where(p => p.Shape.Vertices.Contains(originalPoint)).ToList();
                    foreach (var affectedPatch in affectedPatches)
                    {
                        affectedPatch.Shape.ReplaceVertex(originalPoint, smoothedPoint);
                    }

                    if (CityWall.Circumference.Contains(originalPoint))
                    {
                        CityWall.ReplaceWallPoint(originalPoint, smoothedPoint);
                    }

                    if (Castle.Wall.Circumference.Contains(originalPoint))
                    {
                        Castle.Wall.ReplaceWallPoint(originalPoint, smoothedPoint);
                    }

                    var gateIndex = Gates.IndexOf(originalPoint);
                    if (gateIndex >= 0)
                    {
                        Gates[gateIndex] = smoothedPoint;
                    }
                }

                smoothed.Add(smoothedRoad);
            }

            return smoothed;
        }

        private IEnumerable<Edge> RoadToEdges(List<Vector2> road)
        {
            var v1 = road[0];

            for (var i = 1; i < road.Count; i++)
            {
                var v0 = v1;
                v1 = road[i];

                if (!(Market.Shape.ContainsPoint(v0) && Market.Shape.ContainsPoint(v1)))
                {
                    yield return new Edge(v0, v1);
                }
            }
        }

        private void PopulateTown()
        {
            foreach (var patch in Patches)
            {
                if (patch.WithinCity)
                {
                    patch.Area = new TownArea(patch);

                    if (patch.GetAllNeighbours().Any(p => p.HasCastle))
                    {
                        patch.Area = new RichArea(patch);
                    }

                    if (patch.GetAllNeighbours().Any(p => p.Water))
                    {
                        patch.Area = new PoorArea(patch);
                    }
                }
                else
                {
                    if (patch.Water)
                    {
                        patch.Area = new EmptyArea(patch);
                    }
                    else
                    {
                        if (patch.Shape.Vertices.Intersect(CityWall.Circumference).Any())
                        {
                            patch.Area = new OutsideWallArea(patch);
                        }
                        else
                        {
                            patch.Area = new FarmArea(patch);
                        }
                    }
                }
            }

            Castle.Patch.Area = new CastleArea(Castle.Patch);
        }

        private IEnumerable<Vector2> GetVoronoiPoints(int nPatches, Vector2 center)
        {
            var sa = Rnd.NextDouble() * 2 * Math.PI;

            for (var i = 0; i < nPatches * PatchMultiplier; i++)
            {
                var a = sa + Math.Sqrt(i) * 5;
                var r = (i == 0 ? 0 : 10 + i * (2 + Rnd.NextDouble()));
                yield return new Vector2(Math.Cos(a) * r, Math.Sin(a) * r) + center;
            }
        }

        public Polygon FindCircumference(Patch patch)
        {
            return FindCircumference(new List<Patch> { patch });
        }

        public Polygon FindCircumference(IEnumerable<Patch> patches)
        {
            var allEdges = patches.SelectMany(p => p.Edges);
            var edgeCounts = allEdges.Select(e => new { Edge = e, Count = allEdges.Count(e.Equals) }).ToList();
            var edges = edgeCounts.Where(e => e.Count == 1).Select(e => e.Edge).ToList();


            var edgeVertices = JoinEdges(edges);

            return new Polygon(edgeVertices);
        }

        private static IEnumerable<Vector2> JoinEdges(IList<Edge> edges)
        {
            var currentEdges = edges.ToList();
            var first = edges.First();
            currentEdges.Remove(first);

            yield return first.A;

            Edge edge = null, previous = first;
            while (currentEdges.Any() && !first.Equals(edge))
            {
                edge = currentEdges.First(e => e.A == previous.B || e.B == previous.B);
                currentEdges.Remove(edge);

                if (edge.B == previous.B)
                {
                    edge = Edge.Reverse(edge);
                }
                yield return edge.A;
                previous = edge;
            }
        }

        public bool PointOverWater(Vector2 point)
        {
            return River.Any(r => r.ContainsPoint(point));
        }

        public TownGeometry GetTownGeometry(TownOptions options)
        {
            var geometry = new TownGeometry();

            var buildingShapes = new List<Polygon>();
            foreach (var patch in Patches.Where(p => p.Area != null))
            {
                buildingShapes.AddRange(patch.Area.GetGeometry());
            }

            var buildingPlacer = new BuildingPlacer(buildingShapes);
            var buildings = buildingPlacer.PopulateBuildings();

            geometry.Buildings.AddRange(buildings);
            if (options.Walls)
            {
                geometry.Walls.AddRange(CityWall.GetEdges().Union(Castle.Wall.GetEdges()).Distinct());
                geometry.Towers.AddRange(CityWall.Towers.Union(Castle.Wall.Towers));
                geometry.Gates.AddRange(CityWall.Gates.Union(Castle.Wall.Gates));
            }
            else
            {
                var castleWall = CityWall.GetEdges().Union(Castle.Wall.GetEdges()).Distinct().SelectMany(e => new[] { e.A, e.B }).Where(w => Castle.Patch.Shape.Vertices.Contains(w)).ToList();
                var towers = CityWall.Towers.Union(Castle.Wall.Towers).Intersect(castleWall);
                var gates = CityWall.Gates.Union(Castle.Wall.Gates).Intersect(castleWall);
                geometry.Walls.AddRange(Edge.FromPointList(castleWall));
                geometry.Towers.AddRange(towers);
                geometry.Gates.AddRange(gates);
            }
            geometry.Roads.AddRange(Roads);
            geometry.Roads.AddRange(Streets);

            geometry.Overlay.AddRange(Patches);
            geometry.Water.AddRange(Patches.Where(p => p.Water).Select(p => p.Shape));
            geometry.WaterBorder = new Polygon(WaterBorder);
            geometry.River.AddRange(River);

            return geometry;
        }
    }
}