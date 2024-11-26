using System;
using UnityEngine;

namespace Obi{

	public class ObiCircleShapeTracker2D : ObiShapeTracker
	{

		public ObiCircleShapeTracker2D(ObiCollider2D source, CircleCollider2D collider)
        {
            this.source = source;
			this.collider = collider;
		}	

		public override void UpdateIfNeeded ()
        {

			CircleCollider2D sphere = collider as CircleCollider2D;

            // retrieve collision world and index:
            var world = ObiColliderWorld.GetInstance();
            int index = source.Handle.index;

            // update collider:
            var shape = world.colliderShapes[index];
            shape.is2D = true;
            shape.type = ColliderShape.ShapeType.Sphere;
            shape.filter = source.Filter;
            shape.SetSign(source.Inverted);
            shape.isTrigger = sphere.isTrigger;
            shape.rigidbodyIndex = source.Rigidbody != null ? source.Rigidbody.handle.index : -1;
            shape.materialIndex = source.CollisionMaterial != null ? source.CollisionMaterial.handle.index : -1;
            shape.forceZoneIndex = source.ForceZone != null ? source.ForceZone.handle.index : -1;
            shape.contactOffset = source.Thickness;
            shape.center = sphere.offset;
            shape.size = Vector3.one * sphere.radius;
            world.colliderShapes[index] = shape;

            // update bounds:
            var aabb = world.colliderAabbs[index];
            aabb.FromBounds(sphere.bounds, shape.contactOffset, true);
            world.colliderAabbs[index] = aabb;

            // update transform:
            var trfm = world.colliderTransforms[index];
            trfm.FromTransform2D(sphere.transform, source.Rigidbody as ObiRigidbody2D);
            world.colliderTransforms[index] = trfm;
        }

	}
}

