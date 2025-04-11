using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Obi
{
    public class ObiResourceHandle<T> where T : class
    {
        public T owner = null;               /**< reference to the owner instance*/
        public int index = -1;               /**< index of this resource in the collision world.*/
        private int referenceCount = 0;      /**< amount of references to this handle. Can be used to clean up any associated resources after it reaches zero.*/

        public bool isValid
        {
            get { return index >= 0; }
        }

        public void Invalidate()
        {
            index = -1;
            referenceCount = 0;
        }

        public void Reference()
        {
            referenceCount++;
        }

        public bool Dereference()
        {
            return --referenceCount == 0;
        }

        public ObiResourceHandle(int index = -1)
        {
            this.index = index;
            owner = null;
        }
    }

    public class ObiColliderHandle : ObiResourceHandle<ObiColliderBase>
    {
        public ObiColliderHandle(int index = -1) : base(index) { }
    }
    public class ObiForceZoneHandle : ObiResourceHandle<ObiForceZone>
    {
        public ObiForceZoneHandle(int index = -1) : base(index) { }
    }
    public class ObiCollisionMaterialHandle : ObiResourceHandle<ObiCollisionMaterial>
    {
        public ObiCollisionMaterialHandle(int index = -1) : base(index) { }
    }
    public class ObiRigidbodyHandle : ObiResourceHandle<ObiRigidbodyBase>
    {
        public ObiRigidbodyHandle(int index = -1) : base(index) { }
    }

    public class ObiColliderWorld
    {
        [NonSerialized] public List<IColliderWorldImpl> implementations;

        [NonSerialized] public List<ObiColliderHandle> colliderHandles;           // list of collider handles, used by ObiCollider components to retrieve them.
        [NonSerialized] public ObiNativeColliderShapeList colliderShapes;         // list of collider shapes.
        [NonSerialized] public ObiNativeAabbList colliderAabbs;                   // list of collider bounds.
        [NonSerialized] public ObiNativeAffineTransformList colliderTransforms;   // list of collider transforms.

        [NonSerialized] public List<ObiForceZoneHandle> forceZoneHandles;         // list of collider handles, used by ObiForceZone components to retrieve them.
        [NonSerialized] public ObiNativeForceZoneList forceZones;                 // list of collider force zones.

        [NonSerialized] public List<ObiCollisionMaterialHandle> materialHandles;  // list of material handles, used by ObiCollisionMaterial components to retrieve them.
        [NonSerialized] public ObiNativeCollisionMaterialList collisionMaterials; // list of collision materials.

        [NonSerialized] public List<ObiRigidbodyHandle> rigidbodyHandles;         // list of rigidbody handles, used by ObiRigidbody components to retrieve them.
        [NonSerialized] public ObiNativeRigidbodyList rigidbodies;                // list of rigidbodies.

        [NonSerialized] public ObiTriangleMeshContainer triangleMeshContainer;
        [NonSerialized] public ObiEdgeMeshContainer edgeMeshContainer;
        [NonSerialized] public ObiDistanceFieldContainer distanceFieldContainer;
        [NonSerialized] public ObiHeightFieldContainer heightFieldContainer;

        private List<ObiColliderHandle> collidersToCreate;
        private List<ObiColliderHandle> collidersToDestroy;

        private List<ObiForceZoneHandle> forceZonesToCreate;
        private List<ObiForceZoneHandle> forceZonesToDestroy;

        private List<ObiRigidbodyHandle> rigidbodiesToCreate;
        private List<ObiRigidbodyHandle> rigidbodiesToDestroy;

        private bool updatedThisFrame = false;

        private static ObiColliderWorld instance;

        public static ObiColliderWorld GetInstance()
        {
            if (instance == null)
            {
                instance = new ObiColliderWorld();
                instance.Initialize();
            }
            return instance;
        }

        private void Initialize()
        {
            // Allocate all lists:
            if (implementations == null)
                implementations = new List<IColliderWorldImpl>();

            if (colliderHandles == null)
                colliderHandles = new List<ObiColliderHandle>();
            if (colliderShapes == null)
                colliderShapes = new ObiNativeColliderShapeList();
            if (colliderAabbs == null)
                colliderAabbs = new ObiNativeAabbList();
            if (colliderTransforms == null)
                colliderTransforms = new ObiNativeAffineTransformList();

            if (forceZoneHandles == null)
                forceZoneHandles = new List<ObiForceZoneHandle>();
            if (forceZones == null)
                forceZones = new ObiNativeForceZoneList();

            if (materialHandles == null)
                materialHandles = new List<ObiCollisionMaterialHandle>();
            if (collisionMaterials == null)
                collisionMaterials = new ObiNativeCollisionMaterialList();

            if (rigidbodyHandles == null)
                rigidbodyHandles = new List<ObiRigidbodyHandle>();
            if (rigidbodies == null)
                rigidbodies = new ObiNativeRigidbodyList();

            if (triangleMeshContainer == null)
                triangleMeshContainer = new ObiTriangleMeshContainer();
            if (edgeMeshContainer == null)
                edgeMeshContainer = new ObiEdgeMeshContainer();
            if (distanceFieldContainer == null)
                distanceFieldContainer = new ObiDistanceFieldContainer();
            if (heightFieldContainer == null)
                heightFieldContainer = new ObiHeightFieldContainer();

            if (collidersToCreate == null)
                collidersToCreate = new List<ObiColliderHandle>();
            if (collidersToDestroy == null)
                collidersToDestroy = new List<ObiColliderHandle>();

            if (forceZonesToCreate == null)
                forceZonesToCreate = new List<ObiForceZoneHandle>();
            if (forceZonesToDestroy == null)
                forceZonesToDestroy = new List<ObiForceZoneHandle>();

            if (rigidbodiesToCreate == null)
                rigidbodiesToCreate = new List<ObiRigidbodyHandle>();
            if (rigidbodiesToDestroy == null)
                rigidbodiesToDestroy = new List<ObiRigidbodyHandle>();
        }

        private void Destroy()
        {
            updatedThisFrame = false;
            for (int i = 0; i < implementations.Count; ++i)
            {
                implementations[i].SetColliders(colliderShapes, colliderAabbs, colliderTransforms);
                implementations[i].UpdateWorld(0);
            }

            // Invalidate all handles:
            if (colliderHandles != null)
                foreach (var handle in colliderHandles)
                    handle.Invalidate();

            if (rigidbodyHandles != null)
                foreach (var handle in rigidbodyHandles)
                    handle.Invalidate();

            if (materialHandles != null)
                foreach (var handle in materialHandles)
                    handle.Invalidate();

            if (forceZoneHandles != null)
                foreach (var handle in forceZoneHandles)
                    handle.Invalidate();

            // Dispose of all lists:
            implementations = null;
            colliderHandles = null;
            rigidbodyHandles = null;
            materialHandles = null;
            forceZoneHandles = null;

            collidersToCreate = null;
            collidersToDestroy = null;
            forceZonesToCreate = null;
            forceZonesToDestroy = null;
            rigidbodiesToCreate = null;
            rigidbodiesToDestroy = null;

            colliderShapes?.Dispose();
            colliderAabbs?.Dispose();
            colliderTransforms?.Dispose();
            forceZones?.Dispose();
            collisionMaterials?.Dispose();
            rigidbodies?.Dispose();

            triangleMeshContainer?.Dispose();
            edgeMeshContainer?.Dispose();
            distanceFieldContainer?.Dispose();
            heightFieldContainer?.Dispose();

            instance = null;
        }

        private void DestroyIfUnused()
        {
            // when there are no data and no implementations, the world gets destroyed.
            if (colliderHandles.Count == 0 &&
                rigidbodyHandles.Count == 0 &&
                forceZoneHandles.Count == 0 &&
                materialHandles.Count == 0 &&
                implementations.Count == 0)

                Destroy();
        }

        public void RegisterImplementation(IColliderWorldImpl impl)
        {
            if (!implementations.Contains(impl))
                implementations.Add(impl);
        }

        public void UnregisterImplementation(IColliderWorldImpl impl)
        {
            implementations.Remove(impl);
            DestroyIfUnused();
        }

        public ObiColliderHandle CreateCollider()
        {
            var handle = new ObiColliderHandle();

            // in-editor, we create data right away since the simulation is not running.
            if (!Application.isPlaying)
                CreateColliderData(handle);
            else
                collidersToCreate.Add(handle);

            return handle;
        }

        public ObiForceZoneHandle CreateForceZone()
        {
            var handle = new ObiForceZoneHandle();

            // in-editor, we create data right away since the simulation is not running.
            if (!Application.isPlaying)
                CreateForceZoneData(handle);
            else
                forceZonesToCreate.Add(handle);

            return handle;
        }

        public ObiRigidbodyHandle CreateRigidbody()
        {
            var handle = new ObiRigidbodyHandle();

            // in-editor, we create data right away since the simulation is not running.
            if (!Application.isPlaying)
                CreateRigidbodyData(handle);
            else
                rigidbodiesToCreate.Add(handle);

            return handle;
        }

        public ObiCollisionMaterialHandle CreateCollisionMaterial()
        {
            var handle = new ObiCollisionMaterialHandle(materialHandles.Count);
            materialHandles.Add(handle);

            collisionMaterials.Add(new CollisionMaterial());

            return handle;
        }

        public ObiTriangleMeshHandle GetOrCreateTriangleMesh(Mesh mesh)
        {
            return triangleMeshContainer.GetOrCreateTriangleMesh(mesh);
        }

        public void DestroyTriangleMesh(ObiTriangleMeshHandle meshHandle)
        {
            triangleMeshContainer.DestroyTriangleMesh(meshHandle);
        }

        public ObiEdgeMeshHandle GetOrCreateEdgeMesh(EdgeCollider2D collider)
        {
            return edgeMeshContainer.GetOrCreateEdgeMesh(collider);
        }

        public void DestroyEdgeMesh(ObiEdgeMeshHandle meshHandle)
        {
            edgeMeshContainer.DestroyEdgeMesh(meshHandle);
        }

        public ObiDistanceFieldHandle GetOrCreateDistanceField(ObiDistanceField df)
        {
            return distanceFieldContainer.GetOrCreateDistanceField(df);
        }

        public void DestroyDistanceField(ObiDistanceFieldHandle dfHandle)
        {
            distanceFieldContainer.DestroyDistanceField(dfHandle);
        }

        public ObiHeightFieldHandle GetOrCreateHeightField(TerrainData hf)
        {
            return heightFieldContainer.GetOrCreateHeightField(hf);
        }

        public void DestroyHeightField(ObiHeightFieldHandle hfHandle)
        {
            heightFieldContainer.DestroyHeightField(hfHandle);
        }

        public void DestroyCollider(ObiColliderHandle handle)
        {
            // Destroy data right away if no simulation is running.
            if (!Application.isPlaying || implementations.Count == 0)
                DestroyColliderData(handle);
            else
            {
                // In case the handle is in the creation queue, just remove it.
                if (!collidersToCreate.Remove(handle))
                    collidersToDestroy.Add(handle);
            }
        }

        public void DestroyForceZone(ObiForceZoneHandle handle)
        {
            // Destroy data right away if no simulation is running.
            if (!Application.isPlaying || implementations.Count == 0)
                DestroyForceZoneData(handle);
            else
            {
                // In case the handle is in the creation queue, just remove it.
                if (!forceZonesToCreate.Remove(handle))
                    forceZonesToDestroy.Add(handle);
            }
        }

        public void DestroyRigidbody(ObiRigidbodyHandle handle)
        {
            // Destroy data right away if no simulation is running.
            if (!Application.isPlaying || implementations.Count == 0)
                DestroyRigidbodyData(handle);
            else
            {
                // In case the handle is in the creation queue, just remove it.
                if (!rigidbodiesToCreate.Remove(handle))
                    rigidbodiesToDestroy.Add(handle);
            }
        }

        public void DestroyCollisionMaterial(ObiCollisionMaterialHandle handle)
        {
            if (collisionMaterials != null && handle != null && handle.isValid && handle.index < materialHandles.Count)
            {
                int index = handle.index;
                int lastIndex = materialHandles.Count - 1;

                // swap all collider info:
                materialHandles.Swap(index, lastIndex);
                collisionMaterials.Swap(index, lastIndex);

                // update the index of the handle we swapped with:
                materialHandles[index].index = index;

                // invalidate our handle:
                // (after updating the swapped one!
                // in case there's just one handle in the array,
                // we need to write -1 after 0)
                handle.Invalidate();

                // remove last index:
                materialHandles.RemoveAt(lastIndex);
                collisionMaterials.count--;

                DestroyIfUnused();
            }
        }

        private void DestroyColliderData (ObiColliderHandle handle)
        {
            if (colliderShapes != null && handle != null && handle.isValid && handle.index < colliderHandles.Count)
            {
                int index = handle.index;
                int lastIndex = colliderHandles.Count - 1;

                // swap all collider info:
                colliderHandles.Swap(index, lastIndex);
                colliderShapes.Swap(index, lastIndex);
                colliderAabbs.Swap(index, lastIndex);
                colliderTransforms.Swap(index, lastIndex);

                // update the index of the handle we swapped with:
                colliderHandles[index].index = index;

                // force other colliders to update next frame, as the index of the data they reference
                // (eg the mesh in a MeshCollider) may have changed as a result of deleting this collider's data.
                for (int i = 0; i < colliderHandles.Count; ++i)                    colliderHandles[i].owner.ForceUpdate();

                // invalidate our handle:
                // (after updating the swapped one!
                // in case there's just one handle in the array,
                // we need to write -1 after 0)
                handle.Invalidate();

                // remove last index:
                colliderHandles.RemoveAt(lastIndex);
                colliderShapes.count--;
                colliderAabbs.count--;
                colliderTransforms.count--;

                DestroyIfUnused();
            }
        }

        private void DestroyForceZoneData(ObiForceZoneHandle handle)
        {
            if (forceZones != null && handle != null && handle.isValid && handle.index < forceZoneHandles.Count)
            {
                int index = handle.index;
                int lastIndex = forceZoneHandles.Count - 1;

                // swap all force zone info:
                forceZoneHandles.Swap(index, lastIndex);
                forceZones.Swap(index, lastIndex);

                // update the index of the handle we swapped with:
                forceZoneHandles[index].index = index;

                // invalidate our handle:
                // (after updating the swapped one!
                // in case there's just one handle in the array,
                // we need to write -1 after 0)
                handle.Invalidate();

                // remove last index:
                forceZoneHandles.RemoveAt(lastIndex);
                forceZones.count--;

                DestroyIfUnused();
            }
        }

        private void DestroyRigidbodyData(ObiRigidbodyHandle handle)
        {
            if (rigidbodies != null && handle != null && handle.isValid && handle.index < rigidbodyHandles.Count)
            {
                int index = handle.index;
                int lastIndex = rigidbodyHandles.Count - 1;

                // swap all collider info:
                rigidbodyHandles.Swap(index, lastIndex);
                rigidbodies.Swap(index, lastIndex);

                // update the index of the handle we swapped with:
                rigidbodyHandles[index].index = index;

                // invalidate our handle:
                // (after updating the swapped one!
                // in case there's just one handle in the array,
                // we need to write -1 after 0)
                handle.Invalidate();

                // remove last index:
                rigidbodyHandles.RemoveAt(lastIndex);
                rigidbodies.count--;

                DestroyIfUnused();
            }

        }

        private void CreateColliderData(ObiColliderHandle handle)
        {
            handle.index = colliderHandles.Count;
            colliderHandles.Add(handle);
            colliderShapes.Add(new ColliderShape { materialIndex = -1, rigidbodyIndex = -1, dataIndex = -1 });
            colliderAabbs.Add(new Aabb());
            colliderTransforms.Add(new AffineTransform());
        }

        private void CreateForceZoneData(ObiForceZoneHandle handle)
        {
            handle.index = forceZoneHandles.Count;
            forceZoneHandles.Add(handle);
            forceZones.Add(new ForceZone());
        }

        private void CreateRigidbodyData(ObiRigidbodyHandle handle)
        {
            handle.index = rigidbodyHandles.Count;
            rigidbodyHandles.Add(handle);
            rigidbodies.Add(new ColliderRigidbody());
        }

        public void FlushHandleBuffers()
        {
            // First process destruction, then process creation.
            // In case we create a handle and then destroy it,
            // we should enqueue it for destruction only if it's not in the creation queue.
            // If it is, just remove if from the creation queue.

            if (collidersToDestroy != null)
            {
                foreach (var handle in collidersToDestroy)
                    DestroyColliderData(handle);
                collidersToDestroy?.Clear();
            }

            if (forceZonesToDestroy != null)
            {
                foreach (var handle in forceZonesToDestroy)
                    DestroyForceZoneData(handle);
                forceZonesToDestroy?.Clear();
            }

            if (rigidbodiesToDestroy != null)
            {
                foreach (var handle in rigidbodiesToDestroy)
                    DestroyRigidbodyData(handle);
                rigidbodiesToDestroy?.Clear();
            }

            if (collidersToCreate != null)
            {
                foreach (var handle in collidersToCreate)
                    CreateColliderData(handle);
                collidersToCreate?.Clear();
            }

            if (forceZonesToCreate != null)
            {
                foreach (var handle in forceZonesToCreate)
                    CreateForceZoneData(handle);
                forceZonesToCreate?.Clear();
            }

            if (rigidbodiesToCreate != null)
            {
                foreach (var handle in rigidbodiesToCreate)
                    CreateRigidbodyData(handle);
                rigidbodiesToCreate?.Clear();
            }
          
        }

        public void UpdateWorld(float deltaTime)
        {
            if (updatedThisFrame)
                return;

            updatedThisFrame = true;

            // ensure all objects have valid handles.
            // May destroy the world if it's empty,
            // so we next check that handle/implementations are not null.
            FlushHandleBuffers();

            // update all colliders:
            if (colliderHandles != null)
                for (int i = 0; i < colliderHandles.Count; ++i)
                    colliderHandles[i].owner.UpdateIfNeeded();

            // update all force zones:
            if (forceZoneHandles != null)
                for (int i = 0; i < forceZoneHandles.Count; ++i)
                    forceZoneHandles[i].owner.UpdateIfNeeded();

            // update rigidbodies:
            if (rigidbodyHandles != null)
                for (int i = 0; i < rigidbodyHandles.Count; ++i)
                    rigidbodyHandles[i].owner.UpdateIfNeeded(deltaTime);

            // update implementations:
            if (implementations != null)
                for (int i = 0; i < implementations.Count; ++i)
                {
                    if (implementations[i].referenceCount > 0)
                    {
                        // set arrays:
                        implementations[i].SetColliders(colliderShapes, colliderAabbs, colliderTransforms);
                        implementations[i].SetForceZones(forceZones);
                        implementations[i].SetRigidbodies(rigidbodies);
                        implementations[i].SetCollisionMaterials(collisionMaterials);
                        implementations[i].SetTriangleMeshData(triangleMeshContainer.headers, triangleMeshContainer.bihNodes, triangleMeshContainer.triangles, triangleMeshContainer.vertices);
                        implementations[i].SetEdgeMeshData(edgeMeshContainer.headers, edgeMeshContainer.bihNodes, edgeMeshContainer.edges, edgeMeshContainer.vertices);
                        implementations[i].SetDistanceFieldData(distanceFieldContainer.headers, distanceFieldContainer.dfNodes);
                        implementations[i].SetHeightFieldData(heightFieldContainer.headers, heightFieldContainer.samples);

                        // update world implementation:
                        implementations[i].UpdateWorld(deltaTime);
                    }
                }
        }

        public void FrameStart()
        {
            updatedThisFrame = false;
        }

        public void UpdateCollisionMaterials()
        {
            if (implementations != null)
                for (int i = 0; i < implementations.Count; ++i)
                {
                    if (implementations[i].referenceCount > 0)
                    {
                        implementations[i].SetCollisionMaterials(collisionMaterials);
                    }
                }
        }

        public void UpdateRigidbodyVelocities(ObiSolver solver)
        {
            if (solver != null && solver.initialized)
            {
                int count = Mathf.Min(rigidbodyHandles.Count, solver.rigidbodyLinearDeltas.count);

                for (int i = 0; i < count; ++i)
                    rigidbodyHandles[i].owner.UpdateVelocities(solver.rigidbodyLinearDeltas[i], solver.rigidbodyAngularDeltas[i]);
            }

            solver.rigidbodyLinearDeltas.WipeToZero();
            solver.rigidbodyAngularDeltas.WipeToZero();
            solver.rigidbodyLinearDeltas.Upload();
            solver.rigidbodyAngularDeltas.Upload();
        }

    }
}
