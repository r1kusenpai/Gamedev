using System;
using UnityEngine;
namespace StarterAssets
{
    [PreferBinarySerialization]
    public class ForestGroundCoverData : ScriptableObject
    {
        [Serializable]
        public class Batch
        {
            public Mesh Mesh;
            public Mesh DistantMesh;
            public Material Material;
            public int Submesh;
            public Matrix4x4[] Matrices;
            public Bounds Bounds;
        }
        public Batch[] Batches;
        public int OriginalPlantsPerTile;
        public int TotalPlants;
    }
}
