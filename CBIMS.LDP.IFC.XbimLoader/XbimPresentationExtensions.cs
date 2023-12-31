﻿// Copyright (C) 2023  Liu, Han; School of Software, Tsinghua University
//
// This file is part of CBIMS.LDP.
// CBIMS.LDP is free software: you can redistribute it and/or modify it under the terms of the GNU Lesser General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.
// CBIMS.LDP is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU Lesser General Public License for more details.
// You should have received a copy of the GNU Lesser General Public License along with CBIMS.LDP. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Xbim.Common;
using Xbim.Common.Geometry;
using Xbim.Common.Metadata;
using Xbim.Ifc4.Interfaces;

namespace CBIMS.LDP.IFC.XbimLoader
{
    internal static class XbimPresentationExtensions
    {
        public static HashSet<short> DefaultExclusions(this IModel model, List<Type> exclude)
        {
            var excludedTypes = new HashSet<short>();
            if (exclude == null)
                exclude = new List<Type>()
                {
                    typeof(IIfcSpace),
                    typeof(IIfcFeatureElement)
                };
            foreach (var excludedT in exclude)
            {
                ExpressType ifcT;
                if (excludedT.IsInterface && excludedT.Name.StartsWith("IIfc"))
                {
                    var concreteTypename = excludedT.Name.Substring(1).ToUpper();
                    ifcT = model.Metadata.ExpressType(concreteTypename);
                }
                else
                    ifcT = model.Metadata.ExpressType(excludedT);
                if (ifcT == null) // it could be a type that does not belong in the model schema
                    continue;
                foreach (var exIfcType in ifcT.NonAbstractSubTypes)
                {
                    excludedTypes.Add(exIfcType.TypeId);
                }
            }
            return excludedTypes;
        }
        public static Matrix3D ToMatrix3D(this XbimMatrix3D m)
        {
            return new Matrix3D(m.M11, m.M12, m.M13, m.M14,
                    m.M21, m.M22, m.M23, m.M24,
                    m.M31, m.M32, m.M33, m.M34,
                    m.OffsetX, m.OffsetY, m.OffsetZ, m.M44);
        }

        public static MatrixTransform3D ToMatrixTransform3D(this XbimMatrix3D m)
        {
            return new MatrixTransform3D(m.ToMatrix3D());
        }
        public static void Read(this MeshGeometry3D m3D, byte[] mesh, XbimMatrix3D? transform = null)
        {
            int indexBase = m3D.Positions.Count;
            var qrd = new RotateTransform3D();
            Matrix3D? matrix3D = null;
            if (transform.HasValue)
            {
                var xq = transform.Value.GetRotationQuaternion();
                var quaternion = new Quaternion(xq.X, xq.Y, xq.Z, xq.W);
                if (!quaternion.IsIdentity)
                    qrd.Rotation = new QuaternionRotation3D(quaternion);
                else
                    qrd = null;
                matrix3D = transform.Value.ToMatrix3D();
            }
            using (var ms = new MemoryStream(mesh))
            {
                using (var br = new BinaryReader(ms))
                {
                    var version = br.ReadByte(); //stream format version
                    var numVertices = br.ReadInt32();
                    var numTriangles = br.ReadInt32();

                    var uniqueVertices = new List<Point3D>(numVertices);
                    var vertices = new List<Point3D>(numVertices * 4); //approx the size
                    var triangleIndices = new List<Int32>(numTriangles * 3);
                    var normals = new List<Vector3D>(numVertices * 4);
                    for (var i = 0; i < numVertices; i++)
                    {
                        double x = br.ReadSingle();
                        double y = br.ReadSingle();
                        double z = br.ReadSingle();
                        var p = new Point3D(x, y, z);
                        if (matrix3D.HasValue)
                            p = matrix3D.Value.Transform(p);
                        uniqueVertices.Add(p);
                    }
                    var numFaces = br.ReadInt32();

                    for (var i = 0; i < numFaces; i++)
                    {
                        var numTrianglesInFace = br.ReadInt32();
                        if (numTrianglesInFace == 0) continue;
                        var isPlanar = numTrianglesInFace > 0;
                        numTrianglesInFace = Math.Abs(numTrianglesInFace);
                        if (isPlanar)
                        {
                            var normal = br.ReadPackedNormal().Normal;
                            var wpfNormal = new Vector3D(normal.X, normal.Y, normal.Z);
                            if (qrd != null) //transform the normal if we have to
                                wpfNormal = qrd.Transform(wpfNormal);
                            var uniqueIndices = new Dictionary<int, int>();
                            for (var j = 0; j < numTrianglesInFace; j++)
                            {
                                for (int k = 0; k < 3; k++)
                                {
                                    int idx = ReadIndex(br, numVertices);
                                    int writtenIdx;
                                    if (!uniqueIndices.TryGetValue(idx, out writtenIdx)) //we haven't got it, so add it
                                    {
                                        writtenIdx = vertices.Count;
                                        vertices.Add(uniqueVertices[idx]);
                                        uniqueIndices.Add(idx, writtenIdx);
                                        //add a matching normal
                                        normals.Add(wpfNormal);
                                    }
                                    triangleIndices.Add(indexBase + writtenIdx);
                                }
                            }
                        }
                        else
                        {
                            var uniqueIndices = new Dictionary<int, int>();
                            for (var j = 0; j < numTrianglesInFace; j++)
                            {
                                for (int k = 0; k < 3; k++)
                                {
                                    int idx = ReadIndex(br, numVertices);
                                    var normal = br.ReadPackedNormal().Normal;
                                    int writtenIdx;
                                    var wpfNormal = new Vector3D(normal.X, normal.Y, normal.Z);
                                    if (qrd != null) //transform the normal if we have to
                                        wpfNormal = qrd.Transform(wpfNormal);
                                    if (!uniqueIndices.TryGetValue(idx, out writtenIdx)) //we haven't got it, so add it
                                    {
                                        writtenIdx = vertices.Count;
                                        vertices.Add(uniqueVertices[idx]);
                                        uniqueIndices.Add(idx, writtenIdx);
                                        normals.Add(wpfNormal);
                                    }
                                    else
                                    {
                                        if (normals[writtenIdx] != wpfNormal) //deal with normals that vary at a node
                                        {
                                            writtenIdx = vertices.Count;
                                            vertices.Add(uniqueVertices[idx]);
                                            normals.Add(wpfNormal);
                                        }
                                    }

                                    triangleIndices.Add(indexBase + writtenIdx);
                                }
                            }
                        }
                    }

                    m3D.Positions = new Point3DCollection(m3D.Positions.Concat(vertices));
                    m3D.TriangleIndices = new Int32Collection(m3D.TriangleIndices.Concat(triangleIndices));
                    m3D.Normals = new Vector3DCollection(m3D.Normals.Concat(normals));
                }
                // if(m3D.CanFreeze) m3D.Freeze(); //freeze the mesh to improve performance
            }
        }


        public static void Read(this MeshGeometry3D m3D, string shapeData, XbimMatrix3D? transform = null)
        {

            RotateTransform3D qrd = new RotateTransform3D();
            Matrix3D? matrix3D = null;
            if (transform.HasValue)
            {
                XbimQuaternion xq = transform.Value.GetRotationQuaternion();
                qrd.Rotation = new QuaternionRotation3D(new Quaternion(xq.X, xq.Y, xq.Z, xq.W));
                matrix3D = transform.Value.ToMatrix3D();
            }

            using (StringReader sr = new StringReader(shapeData))
            {
                int version = 1;
                List<Point3D> vertexList = new List<Point3D>(512); //holds the actual unique positions of the vertices in this data set in the mesh
                List<Vector3D> normalList = new List<Vector3D>(512); //holds the actual unique normals of the vertices in this data set in the mesh

                List<Point3D> positions = new List<Point3D>(1024); //holds the actual positions of the vertices in this data set in the mesh
                List<Vector3D> normals = new List<Vector3D>(1024); //holds the actual normals of the vertices in this data set in the mesh
                List<int> triangleIndices = new List<int>(2048);
                String line;
                // Read and display lines from the data until the end of
                // the data is reached.

                while ((line = sr.ReadLine()) != null)
                {

                    string[] tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length > 0) //we need a command
                    {
                        string command = tokens[0].Trim().ToUpper();
                        switch (command)
                        {
                            case "P":
                                vertexList = new List<Point3D>(512);
                                normalList = new List<Vector3D>(512);
                                if (tokens.Length > 0)
                                    version = Int32.Parse(tokens[1]);
                                break;
                            case "V": //process vertices
                                for (int i = 1; i < tokens.Length; i++)
                                {
                                    string[] xyz = tokens[i].Split(',');
                                    Point3D p = new Point3D(Convert.ToDouble(xyz[0], CultureInfo.InvariantCulture),
                                                                      Convert.ToDouble(xyz[1], CultureInfo.InvariantCulture),
                                                                      Convert.ToDouble(xyz[2], CultureInfo.InvariantCulture));
                                    if (matrix3D.HasValue)
                                        p = matrix3D.Value.Transform(p);
                                    vertexList.Add(p);
                                }
                                break;
                            case "N": //processes normals
                                for (int i = 1; i < tokens.Length; i++)
                                {
                                    string[] xyz = tokens[i].Split(',');
                                    Vector3D v = new Vector3D(Convert.ToDouble(xyz[0], CultureInfo.InvariantCulture),
                                                                       Convert.ToDouble(xyz[1], CultureInfo.InvariantCulture),
                                                                       Convert.ToDouble(xyz[2], CultureInfo.InvariantCulture));
                                    normalList.Add(v);
                                }
                                break;
                            case "T": //process triangulated meshes
                                Vector3D currentNormal = new Vector3D(0, 0, 0);
                                //each time we start a new mesh face we have to duplicate the vertices to ensure that we get correct shading of planar and non planar faces
                                var writtenVertices = new Dictionary<int, int>();

                                for (int i = 1; i < tokens.Length; i++)
                                {
                                    string[] indices = tokens[i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (indices.Length != 3) throw new Exception("Invalid triangle definition");
                                    for (int t = 0; t < 3; t++)
                                    {
                                        string[] indexNormalPair = indices[t].Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                                        if (indexNormalPair.Length > 1) //we have a normal defined
                                        {
                                            if (version == 1)
                                            {
                                                string normalStr = indexNormalPair[1].Trim();
                                                switch (normalStr)
                                                {
                                                    case "F": //Front
                                                        currentNormal = new Vector3D(0, -1, 0);
                                                        break;
                                                    case "B": //Back
                                                        currentNormal = new Vector3D(0, 1, 0);
                                                        break;
                                                    case "L": //Left
                                                        currentNormal = new Vector3D(-1, 0, 0);
                                                        break;
                                                    case "R": //Right
                                                        currentNormal = new Vector3D(1, 0, 0);
                                                        break;
                                                    case "U": //Up
                                                        currentNormal = new Vector3D(0, 0, 1);
                                                        break;
                                                    case "D": //Down
                                                        currentNormal = new Vector3D(0, 0, -1);
                                                        break;
                                                    default: //it is an index number
                                                        int normalIndex = int.Parse(indexNormalPair[1]);
                                                        currentNormal = normalList[normalIndex];
                                                        break;
                                                }
                                            }
                                            else //we have support for packed normals
                                            {
                                                var packedNormal = new XbimPackedNormal(ushort.Parse(indexNormalPair[1]));
                                                var n = packedNormal.Normal;
                                                currentNormal = new Vector3D(n.X, n.Y, n.Z);
                                            }
                                            if (matrix3D.HasValue)
                                            {
                                                currentNormal = qrd.Transform(currentNormal);
                                            }
                                        }

                                        //now add the index
                                        int index = int.Parse(indexNormalPair[0]);

                                        int alreadyWrittenAt; //in case it is the first mesh
                                        if (!writtenVertices.TryGetValue(index, out alreadyWrittenAt)) //if we haven't  written it in this mesh pass, add it again unless it is the first one which we know has been written
                                        {
                                            //all vertices will be unique and have only one normal
                                            writtenVertices.Add(index, positions.Count);
                                            triangleIndices.Add(positions.Count + m3D.TriangleIndices.Count);
                                            positions.Add(vertexList[index]);
                                            normals.Add(currentNormal);
                                        }
                                        else //just add the index reference
                                        {
                                            if (normals[alreadyWrittenAt] == currentNormal)
                                                triangleIndices.Add(alreadyWrittenAt);
                                            else //we need another
                                            {
                                                triangleIndices.Add(positions.Count + m3D.TriangleIndices.Count);
                                                positions.Add(vertexList[index]);
                                                normals.Add(currentNormal);
                                            }
                                        }
                                    }
                                }
                                break;
                            case "F": //skip faces for now, can be used to draw edges
                                break;
                            default:
                                throw new Exception("Invalid Geometry Command");

                        }
                    }
                }
                m3D.Positions = new Point3DCollection(m3D.Positions.Concat(positions)); //we do this for wpf performance issues
                m3D.Normals = new Vector3DCollection(m3D.Normals.Concat(normals)); //we do this for wpf performance issues
                m3D.TriangleIndices = new Int32Collection(m3D.TriangleIndices.Concat(triangleIndices)); //we do this for wpf performance issues
            }
        }

        private static int ReadIndex(BinaryReader br, int maxVertexCount)
        {
            if (maxVertexCount <= 0xFF)
                return br.ReadByte();
            if (maxVertexCount <= 0xFFFF)
                return br.ReadUInt16();
            return (int)br.ReadUInt32(); //this should never go over int32
        }

        public static XbimPackedNormal ReadPackedNormal(this BinaryReader br)
        {
            byte u = br.ReadByte();
            byte v = br.ReadByte();
            return new XbimPackedNormal(u, v);
        }
    }
}
