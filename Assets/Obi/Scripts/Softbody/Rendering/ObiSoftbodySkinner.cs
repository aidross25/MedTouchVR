using System;
    [ExecuteInEditMode]
        {
            get
            {
                if (customSkinMap != null || softbody == null || softbody.softbodyBlueprint == null)
                    return customSkinMap;
                return softbody.softbodyBlueprint.defaultSkinmap;
            }
            set { customSkinMap = value; }
        }
        public uint meshInstances { get { return 1; } }

        // specify vertex count and layout
        public static VertexAttributeDescriptor[] layout =
        [HideInInspector]

            // In case there's no user-set softbody reference,
            // try to find one in the same object:
            if (softbody == null)

        public void OnEnable()
            {
                if (softbody.isLoaded)
                    Softbody_OnBlueprintLoaded(softbody, softbody.sourceBlueprint);

                softbody.OnBlueprintLoaded += Softbody_OnBlueprintLoaded;
                softbody.OnSimulationStart += Softbody_OnSimulate;
            }

        public void OnDisable()
            {
                softbody.OnBlueprintLoaded -= Softbody_OnBlueprintLoaded;
                softbody.OnSimulationStart -= Softbody_OnSimulate;
            }
        {
            skinnedMeshRenderer.sharedMesh = sourceMesh;
        }
        {
            if (skinnedMeshRenderer == null)
                sourceRenderer = skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();

            // no shared mesh, no custom skinmap, use the blueprint's input mesh:
            if (skinnedMeshRenderer.sharedMesh == null && customSkinMap == null && actor.sharedBlueprint != null)
                skinnedMeshRenderer.sharedMesh = ((ObiSoftbodySurfaceBlueprint)actor.sharedBlueprint).inputMesh;

            // if there's a sharedMesh, store it.
            if (skinnedMeshRenderer.sharedMesh != null)
                sourceMesh = skinnedMeshRenderer.sharedMesh;

            // at runtime, set the sharedMesh to null since we will be doing our own rendering.
            if (Application.isPlaying)
                skinnedMeshRenderer.sharedMesh = null;

            var skm = skinMap;

            if (softbody == null || softbody.softbodyBlueprint == null || skm == null)
                return false;

            // make sure checksums match, the amount of particles and the amount of bind poses in the skinmap match,
            // and the amount of influence counts and vertices also match.
            return skm.checksum == softbody.softbodyBlueprint.checksum &&
                   skm.bindPoses.count == actor.particleCount + sourceMesh.bindposes.Length &&
                   skm.particlesOnVertices.influenceOffsets.count == vertexCount + 1;
        }

        private void Softbody_OnBlueprintLoaded(ObiActor a, ObiActorBlueprint blueprint)
        {
            BindBoneParticles();
        }

        private void BindBoneParticles()
        {
            boneTransforms = null;
            var bprint = (ObiSoftbodySurfaceBlueprint)softbody.softbodyBlueprint;

            // the blueprint contains no root bone, no work to do:
            if (bprint.rootBone == null)
                return;

            // we'll be traversing the transform hierarchy breadth-first,
            // to make sure bone order matches the one in the blueprint.
            Queue<Transform> queue = new Queue<Transform>();

            // start at the root bone in the skeleton, make sure its name
            // matches the one used in the blueprint:
            string rootName = bprint.rootBone.name;

            if (skinnedMeshRenderer.rootBone != null && skinnedMeshRenderer.rootBone.name == rootName)
                queue.Enqueue(skinnedMeshRenderer.rootBone);
            else
            {
                // root bone names do not match, try to find a bone with the same name in the hierarchy.
                var bones = skinnedMeshRenderer.bones;
                foreach (var bone in bones)
                    if (bone.name == rootName)
                    {
                        queue.Enqueue(bone);
                        break;
                    }
            }

            // didn't find the root bone, so warn the user and just ignore bones.
            if (queue.Count != 1)
            {
                Debug.LogWarning("Did not find any bone matching the root bone defined in the softbody blueprint.");
                return;
            }

            // traverse the hierarchy and store the bones in a list:
            boneTransforms = new List<Transform>();
            while (queue.Count > 0)
            {
                var bone = queue.Dequeue();

                if (bone != null)
                {
                    boneTransforms.Add(bone);
                    foreach (Transform child in bone)
                        queue.Enqueue(child);
                }
            }

            // Deactivate bone particle dynamics by setting their inverse mass to zero.
            for (int i = 0; i < bprint.bonePairs.Count; ++i)
            {
                int solverIndex = softbody.solverIndices[bprint.bonePairs[i].x];
                softbody.solver.invMasses[solverIndex] = 0;
                softbody.solver.invRotationalMasses[solverIndex] = 0;
            }

            // No need to UpdateParticleProperties: at this point, shape matching constraints haven't been added yet,
            // their rest shape will be calculated the next PushConstraints() after loading the actor's blueprint.
        }

        private void Softbody_OnSimulate(ObiActor act, float simulatedTime, float substepTime)
        {
            if (boneTransforms == null || boneTransforms.Count == 0)
                return;

            var bprint = (ObiSoftbodySurfaceBlueprint)softbody.softbodyBlueprint;

            for (int i = 0; i < bprint.bonePairs.Count; ++i)
            {
                int solverIndex = softbody.solverIndices[bprint.bonePairs[i].x];
                var bone = boneTransforms[bprint.bonePairs[i].y];

                Matrix4x4 deformMatrix = softbody.solver.transform.worldToLocalMatrix * bone.transform.localToWorldMatrix * bprint.boneBindPoses[bprint.bonePairs[i].y];

                softbody.solver.startPositions[solverIndex] =
                softbody.solver.endPositions[solverIndex] =
                softbody.solver.positions[solverIndex] = deformMatrix.MultiplyPoint3x4(softbody.solver.restPositions[solverIndex]);

                softbody.solver.startOrientations[solverIndex] =
                softbody.solver.endOrientations[solverIndex] =
                softbody.solver.orientations[solverIndex] = deformMatrix.rotation * softbody.solver.restOrientations[solverIndex];
            }
        }
            if (skinMap != null && softbody != null && softbody.softbodyBlueprint != null)
            {
                skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();

                var blueprintTransform = transform.gameObject == softbody.gameObject ? Matrix4x4.TRS(Vector3.zero, softbody.softbodyBlueprint.rotation, softbody.softbodyBlueprint.scale) : Matrix4x4.identity;

                skinMap.MapParticlesToVertices(skinnedMeshRenderer.sharedMesh, softbody, transform.localToWorldMatrix * blueprintTransform, softbody.transform.worldToLocalMatrix, radius, falloff, maxInfluences, true, softbodyInfluence, m_softbodyInfluences);
            }





#if (OBI_BURST && OBI_MATHEMATICS && OBI_COLLECTIONS)
#endif
                default: