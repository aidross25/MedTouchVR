using System;
using UnityEngine;

namespace Obi{

	public class ObiBoxShapeTracker2D : ObiShapeTracker
	{
		public ObiBoxShapeTracker2D(ObiCollider2D source, BoxCollider2D collider){
            this.source = source;
			this.collider = collider;
		}		
	
		public override void UpdateIfNeeded (){

			BoxCollider2D box = collider as BoxCollider2D;

            var world = ObiColliderWorld.GetInstance();
            int index = source.Handle.index;

            // update collider:
            var shape = world.colliderShapes[index];
            shape.is2D = true;
            shape.type = ColliderShape.ShapeType.Box;
            shape.filter = source.Filter;
            shape.SetSign(source.Inverted);
            shape.isTrigger = box.isTrigger;
            shape.rigidbodyIndex = source.Rigidbody != null ? source.Rigidbody.handle.index : -1;
            shape.materialIndex = source.CollisionMaterial != null ? source.CollisionMaterial.handle.index : -1;
            shape.forceZoneIndex = source.ForceZone != null ? source.ForceZone.handle.index : -1;
            shape.contactOffset = source.Thickness + box.edgeRadius;
            shape.center = box.offset;
            shape.size = box.size;
            world.colliderShapes[index] = shape;

            // update bounds:
            var aabb = world.colliderAabbs[index];
            aabb.FromBounds(box.bounds, shape.contactOffset, true);
            world.colliderAabbs[index] = aabb;

            // update transform:
            var trfm = world.colliderTransforms[index];
            trfm.FromTransform2D(box.transform, source.Rigidbody as ObiRigidbody2D);
            world.colliderTransforms[index] = trfm;
		}

	}
}

