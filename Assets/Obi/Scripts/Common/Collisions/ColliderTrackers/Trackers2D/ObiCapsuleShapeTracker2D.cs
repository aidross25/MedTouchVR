using System;
using UnityEngine;

namespace Obi{

	public class ObiCapsuleShapeTracker2D : ObiShapeTracker
	{
		public ObiCapsuleShapeTracker2D(ObiCollider2D source, CapsuleCollider2D collider)
        {
            this.source = source;
			this.collider = collider;
		}	
	
		public override void UpdateIfNeeded ()
        {
			CapsuleCollider2D capsule = collider as CapsuleCollider2D;

            // retrieve collision world and index:
            var world = ObiColliderWorld.GetInstance();
            int index = source.Handle.index;

            // update collider:
            var shape = world.colliderShapes[index];
            shape.is2D = true;
            shape.type = ColliderShape.ShapeType.Capsule;
            shape.filter = source.Filter;
            shape.SetSign(source.Inverted);
            shape.isTrigger = capsule.isTrigger;
            shape.rigidbodyIndex = source.Rigidbody != null ? source.Rigidbody.handle.index : -1;
            shape.materialIndex = source.CollisionMaterial != null ? source.CollisionMaterial.handle.index : -1;
            shape.forceZoneIndex = source.ForceZone != null ? source.ForceZone.handle.index : -1;
            shape.contactOffset = source.Thickness;
            shape.center = capsule.offset;
            Vector2 size = capsule.size;
            shape.size = new Vector4((capsule.direction == CapsuleDirection2D.Horizontal ? size.y : size.x) * 0.5f,
                                      Mathf.Max(size.x, size.y),
                                      capsule.direction == CapsuleDirection2D.Horizontal ? 0 : 1, 0);
            world.colliderShapes[index] = shape;

            // update bounds:
            var aabb = world.colliderAabbs[index];
            aabb.FromBounds(capsule.bounds, shape.contactOffset,true);
            world.colliderAabbs[index] = aabb;

            // update transform:
            var trfm = world.colliderTransforms[index];
            trfm.FromTransform2D(capsule.transform, source.Rigidbody as ObiRigidbody2D);
            world.colliderTransforms[index] = trfm;
		}

	}
}

