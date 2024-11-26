using System;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Obi{

	public class ObiTerrainShapeTracker : ObiShapeTracker
	{
        ObiHeightFieldHandle handle;

        public ObiTerrainShapeTracker(ObiCollider source, TerrainCollider collider){

            this.source = source;
			this.collider = collider;
		}		

		public void UpdateHeightData()
        {
            ObiColliderWorld.GetInstance().DestroyHeightField(handle);
        }
	
		public override void UpdateIfNeeded ()
        {

            TerrainCollider terrain = collider as TerrainCollider;

            // retrieve collision world and index:
            var world = ObiColliderWorld.GetInstance();
            int index = source.Handle.index;

            int resolution = terrain.terrainData.heightmapResolution;

            // get or create the heightfield:
            if (handle == null || !handle.isValid)
            {
                handle = world.GetOrCreateHeightField(terrain.terrainData);
                handle.Reference();
            }

            // update collider:
            var shape = world.colliderShapes[index];
            shape.type = ColliderShape.ShapeType.Heightmap;
            shape.filter = source.Filter;
            shape.SetSign(source.Inverted);
            shape.isTrigger = terrain.isTrigger;
            shape.rigidbodyIndex = source.Rigidbody != null ? source.Rigidbody.handle.index : -1;
            shape.materialIndex = source.CollisionMaterial != null ? source.CollisionMaterial.handle.index : -1;
            shape.forceZoneIndex = source.ForceZone != null ? source.ForceZone.handle.index : -1;
            shape.contactOffset = source.Thickness;
            shape.dataIndex = handle.index;
            shape.size = terrain.terrainData.size;
            shape.center = new Vector4(resolution, resolution, resolution, resolution);
            world.colliderShapes[index] = shape;

            // update bounds:
            var aabb = world.colliderAabbs[index];
            aabb.FromBounds(terrain.bounds, shape.contactOffset);
            world.colliderAabbs[index] = aabb;

            // update transform:
            var trfm = world.colliderTransforms[index];
            trfm.FromTransform3D(terrain.transform, source.Rigidbody as ObiRigidbody);
            world.colliderTransforms[index] = trfm;
        }

		public override void Destroy()
        {
			base.Destroy();

            if (handle != null && handle.Dereference())
                ObiColliderWorld.GetInstance().DestroyHeightField(handle);
        }
	}
}

