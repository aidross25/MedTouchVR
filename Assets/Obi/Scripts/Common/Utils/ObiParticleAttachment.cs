using System;

        // private variables are serialized during script reloading, to keep their value. Must mark them explicitly as non-serialized.
        [NonSerialized] private ObiPinConstraintsBatch pinBatch;


        /// <summary>  

        /// <summary>  

        /// <summary>  

        /// <summary>  

        /// <summary>  

        /// <summary>  

        /// <summary>  

        /// <summary>  

            // do not re-bind: simply disable and re-enable the attachment.
            DisableAttachment(AttachmentType.Static);
            // Attachments must be updated at the start of the step, before performing any simulation.
            UpdateAttachment();

            // if there's any broken constraint, flag pin constraints as dirty for remerging at the start of the next step.
            BreakDynamicAttachment(substepTime);
            // Disable attachment.
            DisableAttachment(m_AttachmentType);
                            // create a new data batch with all our pin constraints:
                            pinBatch = new ObiPinConstraintsBatch(pins);

                            // add the batch to the actor:
                            pins.AddBatch(pinBatch);

                            // store the attached collider's handle:
                            attachedColliderHandleIndex = -1;

                        // in case the handle has been updated/invalidated (for instance, when disabling the target) rebuild constraints:
                        if (attachedCollider != null &&
                            attachedCollider.Handle != null &&
                            attachedCollider.Handle.index != attachedColliderHandleIndex)

                        // Build the attachment matrix:
                        Matrix4x4 attachmentMatrix = solver.transform.worldToLocalMatrix * m_Target.localToWorldMatrix;

                        // Fix all particles in the group and update their position 
                        // Note: skip assignment to startPositions if you want attached particles to be interpolated too.
                        for (int i = 0; i < m_SolverIndices.Length; ++i)
                                else
            {
                attachedColliderHandleIndex = -1;
                m_Actor.SetConstraintsDirty(Oni.ConstraintType.Pin);
            }
                            // In case the handle has been created/destroyed.
                            if (pinBatch.pinBodies[i] != attachedCollider.Handle)

                            // in case the constraint has been broken:
                            if (-solverBatch.lambdas[(offset + i) * 4 + 3] / sqrTime > pinBatch.breakThresholds[i])

                // constraints are recreated at the start of a step.
                if (dirty)