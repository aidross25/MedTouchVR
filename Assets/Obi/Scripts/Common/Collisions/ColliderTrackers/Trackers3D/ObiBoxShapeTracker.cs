using System;
using UnityEngine;

namespace Obi{

	public class ObiBoxShapeTracker : ObiShapeTracker
	{

		public ObiBoxShapeTracker(ObiCollider source, BoxCollider collider)
        {
            this.source = source;
            this.collider = collider;
		}		
	
		public override void UpdateIfNeeded (){

			BoxCollider box = collider as BoxCollider;

            // retrieve collision world and index:
            var world = ObiColliderWorld.GetInstance();
            int index = source.Handle.index;

            // update collider:
            var shape = world.colliderShapes[index];
            shape.type = ColliderShape.ShapeType.Box;
            shape.filter = source.Filter;
            shape.SetSign(source.Inverted);
            shape.isTrigger = box.isTrigger;
            shape.rigidbodyIndex = source.Rigidbody != null ? source.Rigidbody.handle.index : -1;
            shape.materialIndex = source.CollisionMaterial != null ? source.CollisionMaterial.handle.index : -1;
            shape.forceZoneIndex = source.ForceZone != null ? source.ForceZone.handle.index : -1;
            shape.contactOffset = source.Thickness;
            shape.center = box.center;
            shape.size = box.size;
            world.colliderShapes[index] = shape;

            // update bounds:
            var aabb = world.colliderAabbs[index];
            aabb.FromBounds(box.bounds, shape.contactOffset);
            world.colliderAabbs[index] = aabb;

            // update transform:
            var trfm = world.colliderTransforms[index];
            trfm.FromTransform3D(box.transform, source.Rigidbody as ObiRigidbody);
            world.colliderTransforms[index] = trfm;

		}

	}
}

