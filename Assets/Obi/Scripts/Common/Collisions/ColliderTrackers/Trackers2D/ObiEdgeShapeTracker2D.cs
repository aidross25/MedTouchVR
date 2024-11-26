using System;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Obi{

	public class ObiEdgeShapeTracker2D : ObiShapeTracker
	{
        ObiEdgeMeshHandle handle;

        public ObiEdgeShapeTracker2D(ObiCollider2D source, EdgeCollider2D collider)
        {
            this.source = source;
			this.collider = collider;
		}		

		public void UpdateEdgeData()
        {
            ObiColliderWorld.GetInstance().DestroyEdgeMesh(handle);
        }
	
		public override void UpdateIfNeeded (){

			EdgeCollider2D edgeCollider = collider as EdgeCollider2D;

            // retrieve collision world and index:
            var world = ObiColliderWorld.GetInstance();
            int index = source.Handle.index;

            // get or create the mesh:
            if (handle == null || !handle.isValid)
            {
                handle = world.GetOrCreateEdgeMesh(edgeCollider);
                handle.Reference();
            }

            // update collider:
            var shape = world.colliderShapes[index];
            shape.is2D = true;
            shape.type = ColliderShape.ShapeType.EdgeMesh;
            shape.filter = source.Filter;
            shape.SetSign(source.Inverted);
            shape.isTrigger = edgeCollider.isTrigger;
            shape.rigidbodyIndex = source.Rigidbody != null ? source.Rigidbody.handle.index : -1;
            shape.materialIndex = source.CollisionMaterial != null ? source.CollisionMaterial.handle.index : -1;
            shape.forceZoneIndex = source.ForceZone != null ? source.ForceZone.handle.index : -1;
            shape.center = edgeCollider.offset;
            shape.contactOffset = source.Thickness + edgeCollider.edgeRadius;
            shape.dataIndex = handle.index;
            world.colliderShapes[index] = shape;

            // update bounds:
            var aabb = world.colliderAabbs[index];
            aabb.FromBounds(edgeCollider.bounds, shape.contactOffset, true);
            world.colliderAabbs[index] = aabb;

            // update transform:
            var trfm = world.colliderTransforms[index];
            trfm.FromTransform2D(edgeCollider.transform, source.Rigidbody as ObiRigidbody2D);
            world.colliderTransforms[index] = trfm;
		}

        public override void Destroy()
        {
            base.Destroy();

            if (handle != null && handle.Dereference())
                ObiColliderWorld.GetInstance().DestroyEdgeMesh(handle);
        }
    }
}

