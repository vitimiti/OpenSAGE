﻿// From https://github.com/nickgravelyn/Triangulator
// Licensed under the MIT license

//The MIT License(MIT)

//Copyright(c) 2017, Nick Gravelyn

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in
//all copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Text;
using OpenSage.Mathematics;

namespace OpenSage.Utilities;

/// <summary>
/// A static class exposing methods for triangulating 2D polygons. This is the sole public
/// class in the entire library; all other classes/structures are intended as internal-only
/// objects used only to assist in triangulation.
/// 
/// This class makes use of the DEBUG conditional and produces quite verbose output when built
/// in Debug mode. This is quite useful for debugging purposes, but can slow the process down
/// quite a bit. For optimal performance, build the library in Release mode.
/// 
/// The triangulation is also not optimized for garbage sensitive processing. The point of the
/// library is a robust, yet simple, system for triangulating 2D shapes. It is intended to be
/// used as part of your content pipeline or at load-time. It is not something you want to be
/// using each and every frame unless you really don't care about garbage.
/// </summary>
public static class Triangulator
{
    #region Fields

    private static readonly IndexableCyclicalLinkedList<Vertex> PolygonVertices = new IndexableCyclicalLinkedList<Vertex>();
    private static readonly IndexableCyclicalLinkedList<Vertex> EarVertices = new IndexableCyclicalLinkedList<Vertex>();
    private static readonly CyclicalList<Vertex> ConvexVertices = new CyclicalList<Vertex>();
    private static readonly CyclicalList<Vertex> ReflexVertices = new CyclicalList<Vertex>();

    #endregion

    #region Public Methods

    #region Triangulate

    /// <summary>
    /// Triangulates a 2D polygon produced the indexes required to render the points as a triangle list.
    /// </summary>
    /// <param name="inputVertices">The polygon vertices in counter-clockwise winding order.</param>
    /// <param name="desiredWindingOrder">The desired output winding order.</param>
    /// <param name="outputVertices">The resulting vertices that include any reversals of winding order and holes.</param>
    /// <param name="indices">The resulting indices for rendering the shape as a triangle list.</param>
    public static void Triangulate(
        Vector2[] inputVertices,
        WindingOrder desiredWindingOrder,
        out Vector2[] outputVertices,
        out ushort[] indices)
    {
        Log("Beginning triangulation...");

        List<Triangle> triangles = new List<Triangle>();

        //make sure we have our vertices wound properly
        if (DetermineWindingOrder(inputVertices) == WindingOrder.Clockwise)
            outputVertices = ReverseWindingOrder(inputVertices);
        else
            outputVertices = (Vector2[])inputVertices.Clone();

        //clear all of the lists
        PolygonVertices.Clear();
        EarVertices.Clear();
        ConvexVertices.Clear();
        ReflexVertices.Clear();

        //generate the cyclical list of vertices in the polygon
        for (int i = 0; i < outputVertices.Length; i++)
            PolygonVertices.AddLast(new Vertex(outputVertices[i], (ushort)i));

        //categorize all of the vertices as convex, reflex, and ear
        FindConvexAndReflexVertices();
        FindEarVertices();

        //clip all the ear vertices
        while (PolygonVertices.Count > 3 && EarVertices.Count > 0)
            ClipNextEar(triangles);

        //if there are still three points, use that for the last triangle
        if (PolygonVertices.Count == 3)
            triangles.Add(new Triangle(
                PolygonVertices[0].Value,
                PolygonVertices[1].Value,
                PolygonVertices[2].Value));

        //add all of the triangle indices to the output array
        indices = new ushort[triangles.Count * 3];

        //move the if statement out of the loop to prevent all the
        //redundant comparisons
        if (desiredWindingOrder == WindingOrder.CounterClockwise)
        {
            for (int i = 0; i < triangles.Count; i++)
            {
                indices[(i * 3)] = triangles[i].A.Index;
                indices[(i * 3) + 1] = triangles[i].B.Index;
                indices[(i * 3) + 2] = triangles[i].C.Index;
            }
        }
        else
        {
            for (int i = 0; i < triangles.Count; i++)
            {
                indices[(i * 3)] = triangles[i].C.Index;
                indices[(i * 3) + 1] = triangles[i].B.Index;
                indices[(i * 3) + 2] = triangles[i].A.Index;
            }
        }
    }

    #endregion

    #region CutHoleInShape

    /// <summary>
    /// Cuts a hole into a shape.
    /// </summary>
    /// <param name="shapeVerts">An array of vertices for the primary shape.</param>
    /// <param name="holeVerts">An array of vertices for the hole to be cut. It is assumed that these vertices lie completely within the shape verts.</param>
    /// <returns>The new array of vertices that can be passed to Triangulate to properly triangulate the shape with the hole.</returns>
    public static Vector2[] CutHoleInShape(Vector2[] shapeVerts, Vector2[] holeVerts)
    {
        Log("Cutting hole into shape...");

        //make sure the shape vertices are wound counter clockwise and the hole vertices clockwise
        shapeVerts = EnsureWindingOrder(shapeVerts, WindingOrder.CounterClockwise);
        holeVerts = EnsureWindingOrder(holeVerts, WindingOrder.Clockwise);

        //clear all of the lists
        PolygonVertices.Clear();
        EarVertices.Clear();
        ConvexVertices.Clear();
        ReflexVertices.Clear();

        //generate the cyclical list of vertices in the polygon
        for (int i = 0; i < shapeVerts.Length; i++)
            PolygonVertices.AddLast(new Vertex(shapeVerts[i], (ushort)i));

        CyclicalList<Vertex> holePolygon = new CyclicalList<Vertex>();
        for (int i = 0; i < holeVerts.Length; i++)
            holePolygon.Add(new Vertex(holeVerts[i], (ushort)(i + PolygonVertices.Count)));

#if DEBUG
        StringBuilder vString = new StringBuilder();
        foreach (Vertex v in PolygonVertices)
            vString.Append(string.Format("{0}, ", v));
        Log("Shape Vertices: {0}", vString);

        vString = new StringBuilder();
        foreach (Vertex v in holePolygon)
            vString.Append(string.Format("{0}, ", v));
        Log("Hole Vertices: {0}", vString);
#endif

        FindConvexAndReflexVertices();
        FindEarVertices();

        //find the hole vertex with the largest X value
        Vertex rightMostHoleVertex = holePolygon[0];
        foreach (Vertex v in holePolygon)
            if (v.Position.X > rightMostHoleVertex.Position.X)
                rightMostHoleVertex = v;

        //construct a list of all line segments where at least one vertex
        //is to the right of the rightmost hole vertex with one vertex
        //above the hole vertex and one below
        List<LineSegment> segmentsToTest = new List<LineSegment>();
        for (int i = 0; i < PolygonVertices.Count; i++)
        {
            Vertex a = PolygonVertices[i].Value;
            Vertex b = PolygonVertices[i + 1].Value;

            if ((a.Position.X > rightMostHoleVertex.Position.X || b.Position.X > rightMostHoleVertex.Position.X) &&
                ((a.Position.Y >= rightMostHoleVertex.Position.Y && b.Position.Y <= rightMostHoleVertex.Position.Y) ||
                (a.Position.Y <= rightMostHoleVertex.Position.Y && b.Position.Y >= rightMostHoleVertex.Position.Y)))
                segmentsToTest.Add(new LineSegment(a, b));
        }

        //now we try to find the closest intersection point heading to the right from
        //our hole vertex.
        float? closestPoint = null;
        LineSegment closestSegment = new LineSegment();
        foreach (LineSegment segment in segmentsToTest)
        {
            float? intersection = segment.IntersectsWithRay(rightMostHoleVertex.Position, Vector2.UnitX);
            if (intersection != null)
            {
                if (closestPoint == null || closestPoint.Value > intersection.Value)
                {
                    closestPoint = intersection;
                    closestSegment = segment;
                }
            }
        }

        //if closestPoint is null, there were no collisions (likely from improper input data),
        //but we'll just return without doing anything else
        if (closestPoint == null)
            return shapeVerts;

        //otherwise we can find our mutually visible vertex to split the polygon
        Vector2 I = rightMostHoleVertex.Position + Vector2.UnitX * closestPoint.Value;
        Vertex P = (closestSegment.A.Position.X > closestSegment.B.Position.X)
            ? closestSegment.A
            : closestSegment.B;

        //construct triangle MIP
        Triangle mip = new Triangle(rightMostHoleVertex, new Vertex(I, 1), P);

        //see if any of the reflex vertices lie inside of the MIP triangle
        List<Vertex> interiorReflexVertices = new List<Vertex>();
        foreach (Vertex v in ReflexVertices)
            if (mip.ContainsPoint(v))
                interiorReflexVertices.Add(v);

        //if there are any interior reflex vertices, find the one that, when connected
        //to our rightMostHoleVertex, forms the line closest to Vector2.UnitX
        if (interiorReflexVertices.Count > 0)
        {
            float closestDot = -1f;
            foreach (Vertex v in interiorReflexVertices)
            {
                //compute the dot product of the vector against the UnitX
                Vector2 d = Vector2.Normalize(v.Position - rightMostHoleVertex.Position);
                float dot = Vector2.Dot(Vector2.UnitX, d);

                //if this line is the closest we've found
                if (dot > closestDot)
                {
                    //save the value and save the vertex as P
                    closestDot = dot;
                    P = v;
                }
            }
        }

        //now we just form our output array by injecting the hole vertices into place
        //we know we have to inject the hole into the main array after point P going from
        //rightMostHoleVertex around and then back to P.
        int mIndex = holePolygon.IndexOf(rightMostHoleVertex);
        int injectPoint = PolygonVertices.IndexOf(P);

        Log("Inserting hole at injection point {0} starting at hole vertex {1}.",
            P,
            rightMostHoleVertex);
        for (int i = mIndex; i <= mIndex + holePolygon.Count; i++)
        {
            Log("Inserting vertex {0} after vertex {1}.", holePolygon[i], PolygonVertices[injectPoint].Value);
            PolygonVertices.AddAfter(PolygonVertices[injectPoint++], holePolygon[i]);
        }
        PolygonVertices.AddAfter(PolygonVertices[injectPoint], P);

#if DEBUG
        vString = new StringBuilder();
        foreach (Vertex v in PolygonVertices)
            vString.Append(string.Format("{0}, ", v));
        Log("New Shape Vertices: {0}\n", vString);
#endif

        //finally we write out the new polygon vertices and return them out
        Vector2[] newShapeVerts = new Vector2[PolygonVertices.Count];
        for (int i = 0; i < PolygonVertices.Count; i++)
            newShapeVerts[i] = PolygonVertices[i].Value.Position;

        return newShapeVerts;
    }

    #endregion

    #region EnsureWindingOrder

    /// <summary>
    /// Ensures that a set of vertices are wound in a particular order, reversing them if necessary.
    /// </summary>
    /// <param name="vertices">The vertices of the polygon.</param>
    /// <param name="windingOrder">The desired winding order.</param>
    /// <returns>A new set of vertices if the winding order didn't match; otherwise the original set.</returns>
    public static Vector2[] EnsureWindingOrder(Vector2[] vertices, WindingOrder windingOrder)
    {
        Log("Ensuring winding order of {0}...", windingOrder);
        if (DetermineWindingOrder(vertices) != windingOrder)
        {
            Log("Reversing vertices...");
            return ReverseWindingOrder(vertices);
        }

        Log("No reversal needed.");
        return vertices;
    }

    #endregion

    #region ReverseWindingOrder

    /// <summary>
    /// Reverses the winding order for a set of vertices.
    /// </summary>
    /// <param name="vertices">The vertices of the polygon.</param>
    /// <returns>The new vertices for the polygon with the opposite winding order.</returns>
    public static Vector2[] ReverseWindingOrder(Vector2[] vertices)
    {
        Log("Reversing winding order...");
        Vector2[] newVerts = new Vector2[vertices.Length];

#if DEBUG
        StringBuilder vString = new StringBuilder();
        foreach (Vector2 v in vertices)
            vString.Append(string.Format("{0}, ", v));
        Log("Original Vertices: {0}", vString);
#endif

        newVerts[0] = vertices[0];
        for (int i = 1; i < newVerts.Length; i++)
            newVerts[i] = vertices[vertices.Length - i];

#if DEBUG
        vString = new StringBuilder();
        foreach (Vector2 v in newVerts)
            vString.Append(string.Format("{0}, ", v));
        Log("New Vertices After Reversal: {0}\n", vString);
#endif

        return newVerts;
    }

    #endregion

    #region DetermineWindingOrder

    /// <summary>
    /// Determines the winding order of a polygon given a set of vertices.
    /// </summary>
    /// <param name="vertices">The vertices of the polygon.</param>
    /// <returns>The calculated winding order of the polygon.</returns>
    public static WindingOrder DetermineWindingOrder(Vector2[] vertices)
    {
        int clockWiseCount = 0;
        int counterClockWiseCount = 0;
        Vector2 p1 = vertices[0];

        for (int i = 1; i < vertices.Length; i++)
        {
            Vector2 p2 = vertices[i];
            Vector2 p3 = vertices[(i + 1) % vertices.Length];

            Vector2 e1 = p1 - p2;
            Vector2 e2 = p3 - p2;

            if (e1.X * e2.Y - e1.Y * e2.X >= 0)
                clockWiseCount++;
            else
                counterClockWiseCount++;

            p1 = p2;
        }

        return (clockWiseCount > counterClockWiseCount)
            ? WindingOrder.Clockwise
            : WindingOrder.CounterClockwise;
    }

    #endregion

    #endregion

    #region Private Methods

    #region ClipNextEar

    private static void ClipNextEar(ICollection<Triangle> triangles)
    {
        //find the triangle
        Vertex ear = EarVertices[0].Value;
        Vertex prev = PolygonVertices[PolygonVertices.IndexOf(ear) - 1].Value;
        Vertex next = PolygonVertices[PolygonVertices.IndexOf(ear) + 1].Value;
        triangles.Add(new Triangle(ear, next, prev));

        //remove the ear from the shape
        EarVertices.RemoveAt(0);
        PolygonVertices.RemoveAt(PolygonVertices.IndexOf(ear));
        Log("Removed Ear: {0}", ear);

        //validate the neighboring vertices
        ValidateAdjacentVertex(prev);
        ValidateAdjacentVertex(next);

        //write out the states of each of the lists
#if DEBUG
        StringBuilder rString = new StringBuilder();
        foreach (Vertex v in ReflexVertices)
            rString.Append(string.Format("{0}, ", v.Index));
        Log("Reflex Vertices: {0}", rString);

        StringBuilder cString = new StringBuilder();
        foreach (Vertex v in ConvexVertices)
            cString.Append(string.Format("{0}, ", v.Index));
        Log("Convex Vertices: {0}", cString);

        StringBuilder eString = new StringBuilder();
        foreach (Vertex v in EarVertices)
            eString.Append(string.Format("{0}, ", v.Index));
        Log("Ear Vertices: {0}", eString);
#endif
    }

    #endregion

    #region ValidateAdjacentVertex

    private static void ValidateAdjacentVertex(Vertex vertex)
    {
        Log("Validating: {0}...", vertex);

        if (ReflexVertices.Contains(vertex))
        {
            if (IsConvex(vertex))
            {
                ReflexVertices.Remove(vertex);
                ConvexVertices.Add(vertex);
                Log("Vertex: {0} now convex", vertex);
            }
            else
            {
                Log("Vertex: {0} still reflex", vertex);
            }
        }

        if (ConvexVertices.Contains(vertex))
        {
            bool wasEar = EarVertices.Contains(vertex);
            bool isEar = IsEar(vertex);

            if (wasEar && !isEar)
            {
                EarVertices.Remove(vertex);
                Log("Vertex: {0} no longer ear", vertex);
            }
            else if (!wasEar && isEar)
            {
                EarVertices.AddFirst(vertex);
                Log("Vertex: {0} now ear", vertex);
            }
            else
            {
                Log("Vertex: {0} still ear", vertex);
            }
        }
    }

    #endregion

    #region FindConvexAndReflexVertices

    private static void FindConvexAndReflexVertices()
    {
        for (int i = 0; i < PolygonVertices.Count; i++)
        {
            Vertex v = PolygonVertices[i].Value;

            if (IsConvex(v))
            {
                ConvexVertices.Add(v);
                Log("Convex: {0}", v);
            }
            else
            {
                ReflexVertices.Add(v);
                Log("Reflex: {0}", v);
            }
        }
    }

    #endregion

    #region FindEarVertices

    private static void FindEarVertices()
    {
        for (int i = 0; i < ConvexVertices.Count; i++)
        {
            Vertex c = ConvexVertices[i];

            if (IsEar(c))
            {
                EarVertices.AddLast(c);
                Log("Ear: {0}", c);
            }
        }
    }

    #endregion

    #region IsEar

    private static bool IsEar(Vertex c)
    {
        Vertex p = PolygonVertices[PolygonVertices.IndexOf(c) - 1].Value;
        Vertex n = PolygonVertices[PolygonVertices.IndexOf(c) + 1].Value;

        Log("Testing vertex {0} as ear with triangle {1}, {0}, {2}...", c, p, n);

        foreach (Vertex t in ReflexVertices)
        {
            if (t.Equals(p) || t.Equals(c) || t.Equals(n))
                continue;

            if (Triangle.ContainsPoint(p, c, n, t))
            {
                Log("\tTriangle contains vertex {0}...", t);
                return false;
            }
        }

        return true;
    }

    #endregion

    #region IsConvex

    private static bool IsConvex(Vertex c)
    {
        Vertex p = PolygonVertices[PolygonVertices.IndexOf(c) - 1].Value;
        Vertex n = PolygonVertices[PolygonVertices.IndexOf(c) + 1].Value;

        Vector2 d1 = Vector2.Normalize(c.Position - p.Position);
        Vector2 d2 = Vector2.Normalize(n.Position - c.Position);
        Vector2 n2 = new Vector2(-d2.Y, d2.X);

        return (Vector2.Dot(d1, n2) <= 0f);
    }

    #endregion

    #region IsReflex

    private static bool IsReflex(Vertex c)
    {
        return !IsConvex(c);
    }

    #endregion

    #region Log


    private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    [Conditional("DEBUG")]
    private static void Log(string format, params object[] parameters)
    {
        Logger.Debug(format, parameters);
        //System.Console.WriteLine(format, parameters);
    }

    #endregion

    #endregion

    /// <summary>
    /// Implements a List structure as a cyclical list where indices are wrapped.
    /// </summary>
    /// <typeparam name="T">The Type to hold in the list.</typeparam>
    private class CyclicalList<T> : List<T>
    {
        public new T this[int index]
        {
            get
            {
                //perform the index wrapping
                while (index < 0)
                    index = Count + index;
                if (index >= Count)
                    index %= Count;

                return base[index];
            }
            set
            {
                //perform the index wrapping
                while (index < 0)
                    index = Count + index;
                if (index >= Count)
                    index %= Count;

                base[index] = value;
            }
        }

        public CyclicalList() { }

        public CyclicalList(IEnumerable<T> collection)
            : base(collection)
        {
        }

        public new void RemoveAt(int index)
        {
            Remove(this[index]);
        }
    }

    /// <summary>
    /// Implements a LinkedList that is both indexable as well as cyclical. Thus
    /// indexing into the list with an out-of-bounds index will automatically cycle
    /// around the list to find a valid node.
    /// </summary>
    private class IndexableCyclicalLinkedList<T> : LinkedList<T>
    {
        /// <summary>
        /// Gets the LinkedListNode at a particular index.
        /// </summary>
        /// <param name="index">The index of the node to retrieve.</param>
        /// <returns>The LinkedListNode found at the index given.</returns>
        public LinkedListNode<T> this[int index]
        {
            get
            {
                //perform the index wrapping
                while (index < 0)
                    index = Count + index;
                if (index >= Count)
                    index %= Count;

                //find the proper node
                LinkedListNode<T> node = First;
                for (int i = 0; i < index; i++)
                    node = node.Next;

                return node;
            }
        }

        /// <summary>
        /// Removes the node at a given index.
        /// </summary>
        /// <param name="index">The index of the node to remove.</param>
        public void RemoveAt(int index)
        {
            Remove(this[index]);
        }

        /// <summary>
        /// Finds the index of a given item.
        /// </summary>
        /// <param name="item">The item to find.</param>
        /// <returns>The index of the item if found; -1 if the item is not found.</returns>
        public int IndexOf(T item)
        {
            for (int i = 0; i < Count; i++)
                if (this[i].Value.Equals(item))
                    return i;

            return -1;
        }
    }

    private struct LineSegment
    {
        public Vertex A;
        public Vertex B;

        public LineSegment(Vertex a, Vertex b)
        {
            A = a;
            B = b;
        }

        public float? IntersectsWithRay(Vector2 origin, Vector2 direction)
        {
            float largestDistance = Math.Max(A.Position.X - origin.X, B.Position.X - origin.X) * 2f;
            LineSegment raySegment = new LineSegment(new Vertex(origin, 0), new Vertex(origin + (direction * largestDistance), 0));

            Vector2? intersection = FindIntersection(this, raySegment);
            float? value = null;

            if (intersection != null)
                value = Vector2.Distance(origin, intersection.Value);

            return value;
        }

        public static Vector2? FindIntersection(LineSegment a, LineSegment b)
        {
            float x1 = a.A.Position.X;
            float y1 = a.A.Position.Y;
            float x2 = a.B.Position.X;
            float y2 = a.B.Position.Y;
            float x3 = b.A.Position.X;
            float y3 = b.A.Position.Y;
            float x4 = b.B.Position.X;
            float y4 = b.B.Position.Y;

            float denom = (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1);

            float uaNum = (x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3);
            float ubNum = (x2 - x1) * (y1 - y3) - (y2 - y1) * (x1 - x3);

            float ua = uaNum / denom;
            float ub = ubNum / denom;

            if (Math.Clamp(ua, 0f, 1f) != ua || Math.Clamp(ub, 0f, 1f) != ub)
                return null;

            return a.A.Position + (a.B.Position - a.A.Position) * ua;
        }
    }

    /// <summary>
    /// A basic triangle structure that holds the three vertices that make up a given triangle.
    /// </summary>
    private readonly struct Triangle
    {
        public readonly Vertex A;
        public readonly Vertex B;
        public readonly Vertex C;

        public Triangle(Vertex a, Vertex b, Vertex c)
        {
            A = a;
            B = b;
            C = c;
        }

        public bool ContainsPoint(Vertex point)
        {
            //return true if the point to test is one of the vertices
            if (point.Equals(A) || point.Equals(B) || point.Equals(C))
                return true;

            bool oddNodes = false;

            if (checkPointToSegment(C, A, point))
                oddNodes = !oddNodes;
            if (checkPointToSegment(A, B, point))
                oddNodes = !oddNodes;
            if (checkPointToSegment(B, C, point))
                oddNodes = !oddNodes;

            return oddNodes;
        }

        public static bool ContainsPoint(Vertex a, Vertex b, Vertex c, Vertex point)
        {
            return new Triangle(a, b, c).ContainsPoint(point);
        }

        static bool checkPointToSegment(Vertex sA, Vertex sB, Vertex point)
        {
            if ((sA.Position.Y < point.Position.Y && sB.Position.Y >= point.Position.Y) ||
                (sB.Position.Y < point.Position.Y && sA.Position.Y >= point.Position.Y))
            {
                float x =
                    sA.Position.X +
                    (point.Position.Y - sA.Position.Y) /
                    (sB.Position.Y - sA.Position.Y) *
                    (sB.Position.X - sA.Position.X);

                if (x < point.Position.X)
                    return true;
            }

            return false;
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(Triangle))
                return false;
            return Equals((Triangle)obj);
        }

        public bool Equals(Triangle obj)
        {
            return obj.A.Equals(A) && obj.B.Equals(B) && obj.C.Equals(C);
        }

        public override int GetHashCode() => HashCode.Combine(A, B, C);
    }

    private struct Vertex
    {
        public readonly Vector2 Position;
        public readonly ushort Index;

        public Vertex(Vector2 position, ushort index)
        {
            Position = position;
            Index = index;
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(Vertex))
                return false;
            return Equals((Vertex)obj);
        }

        public bool Equals(Vertex obj)
        {
            return obj.Position.Equals(Position) && obj.Index == Index;
        }

        public override int GetHashCode() => HashCode.Combine(Position, Index);

        public override string ToString()
        {
            return string.Format("{0} ({1})", Position, Index);
        }
    }
}

/// <summary>
/// Specifies a desired winding order for the shape vertices.
/// </summary>
public enum WindingOrder
{
    Clockwise,
    CounterClockwise
}
