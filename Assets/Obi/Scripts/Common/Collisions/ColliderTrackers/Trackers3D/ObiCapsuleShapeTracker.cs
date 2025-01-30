using System;
using UnityEngine;

namespace Obi{

	public class ObiCapsuleShapeTracker : ObiShapeTracker
	{

		public ObiCapsuleShapeTracker(ObiCollider source, CapsuleCollider collider){
			this.collider = collider;
            this.source = source;
		}	
	
		public override void UpdateIfNeeded (){

            CapsuleCollider capsule = collider as CapsuleCollider;

            // retrieve collision world and index:
            var world = ObiColliderWorld.GetInstance();
            int index = source.Handle.index;

            // update collider:
            var shape = world.colliderShapes[index];
            shape.type = ColliderShape.ShapeType.Capsule;
            shape.filter = source.Filter;
            shape.SetSign(source.Inverted);
            shape.isTrigger = capsule.isTrigger;
            shape.rigidbodyIndex = source.Rigidbody != null ? source.Rigidbody.handle.index : -1;
            shape.materialIndex = source.CollisionMaterial != null ? source.CollisionMaterial.handle.index : -1;
            shape.forceZoneIndex = source.ForceZone != null ? source.ForceZone.handle.index : -1;
            shape.contactOffset = source.Thickness;
            shape.center = capsule.center;
            shape.size = new Vector4(capsule.radius, capsule.height, capsule.direction, 0);
            world.colliderShapes[index] = shape;

            // update bounds:
            var aabb = world.colliderAabbs[index];
            aabb.FromBounds(capsule.bounds, shape.contactOffset);
            world.colliderAabbs[index] = aabb;

            // update transform:
            var trfm = world.colliderTransforms[index];
            trfm.FromTransform3D(capsule.transform, source.Rigidbody as ObiRigidbody);
            world.colliderTransforms[index] = trfm;
        }

	}
}

