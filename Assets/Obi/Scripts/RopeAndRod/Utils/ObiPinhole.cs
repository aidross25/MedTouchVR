using System;using UnityEngine;namespace Obi{    [AddComponentMenu("Physics/Obi/Obi Pinhole", 820)]    [RequireComponent(typeof(ObiRopeBase))]    [ExecuteInEditMode]    public class ObiPinhole : MonoBehaviour    {        [SerializeField] [HideInInspector] private ObiRopeBase m_Rope;        [SerializeField] [HideInInspector] private Transform m_Target;        [Range(0, 1)]        [SerializeField] [HideInInspector] private float m_Position = 0;

        [SerializeField] [HideInInspector] private bool m_LimitRange = false;        [MinMax(0, 1)]        [SerializeField] [HideInInspector] private Vector2 m_Range = new Vector2(0, 1);        [Range(0, 1)]        [SerializeField] [HideInInspector] private float m_Friction = 0;        [SerializeField] [HideInInspector] private float m_MotorSpeed = 0;        [SerializeField] [HideInInspector] private float m_MotorForce = 0;
        [SerializeField] [HideInInspector] private float m_Compliance = 0;        [SerializeField] [HideInInspector] private bool m_ClampAtEnds = true;

        [SerializeField] [HideInInspector] private ObiPinholeConstraintsBatch.PinholeEdge currentEdge;
        [SerializeField] [HideInInspector] public ObiPinholeConstraintsBatch.PinholeEdge firstEdge;
        [SerializeField] [HideInInspector] public ObiPinholeConstraintsBatch.PinholeEdge lastEdge;

        // private variables are serialized during script reloading, to keep their value. Must mark them explicitly as non-serialized.
        [NonSerialized] private ObiPinholeConstraintsBatch pinBatch;        [NonSerialized] private ObiColliderBase attachedCollider;        [NonSerialized] private int attachedColliderHandleIndex;

        [NonSerialized] private Vector3 m_PositionOffset;
        [NonSerialized] private bool m_ParametersDirty = true;
        [NonSerialized] private bool m_PositionDirty = false;
        [NonSerialized] private bool m_RangeDirty = false;

        /// <summary>          /// The rope this attachment is added to.        /// </summary>         public ObiActor rope        {            get { return m_Rope; }        }

        public float edgeCoordinate
        {
            get { return currentEdge.coordinate; }
        }

        public int edgeIndex
        {
            get { return currentEdge.edgeIndex; }
        }

        /// <summary>          /// The target transform that the pinhole should be attached to.        /// </summary>         public Transform target        {            get { return m_Target; }            set            {                if (value != m_Target)                {                    m_Target = value;                    Bind();                }            }        }

        /// <summary>          /// Normalized coordinate of the point along the rope where the pinhole is positioned.        /// </summary> 
        public float position
        {
            get { return m_Position; }
            set
            {
                if (!Mathf.Approximately(value, m_Position))                {                    m_Position = value;                    CalculateMu();                }
            }
        }

        public bool limitRange
        {
            get { return m_LimitRange; }
            set
            {                if (m_LimitRange != value)
                {
                    m_LimitRange = value;
                    CalculateRange();
                }            }
        }

        /// <summary>          /// Normalized coordinate of the point along the rope where the pinhole is positioned.        /// </summary> 
        public Vector2 range
        {
            get { return m_Range; }
            set
            {                m_Range = value;                CalculateRange();            }
        }

        /// <summary>          /// Whether this pinhole is currently bound or not.        /// </summary>         public bool isBound        {            get { return m_Target != null && currentEdge.edgeIndex >= 0; }        }

        /// <summary>          /// Constraint compliance.        /// </summary>        /// High compliance values will increase the pinhole's elasticity.        public float compliance        {            get { return m_Compliance; }            set            {                if (!Mathf.Approximately(value, m_Compliance))                {                    m_Compliance = value;                    m_ParametersDirty = true;                }            }        }

        public float friction        {            get { return m_Friction; }            set            {                if (!Mathf.Approximately(value, m_Friction))                {                    m_Friction = value;                    m_ParametersDirty = true;
                }            }        }

        public float motorSpeed        {            get { return m_MotorSpeed; }            set            {                if (!Mathf.Approximately(value, m_MotorSpeed))                {                    m_MotorSpeed = value;                    m_ParametersDirty = true;                }            }        }

        public float motorForce        {            get { return m_MotorForce; }            set            {                if (!Mathf.Approximately(value, m_MotorForce))                {                    m_MotorForce = value;                    m_ParametersDirty = true;                }            }        }

        public bool clampAtEnds        {            get { return m_ClampAtEnds; }            set            {                if (m_ClampAtEnds != value)                {                    m_ClampAtEnds = value;                    m_ParametersDirty = true;                }            }        }

        /// <summary>          /// Force threshold above which the pinhole should break.        /// </summary>        [Delayed] public float breakThreshold = float.PositiveInfinity;        public float relativeVelocity { get; private set; }        private void OnEnable()        {            m_Rope = GetComponent<ObiRopeBase>();            m_Rope.OnBlueprintLoaded += Actor_OnBlueprintLoaded;            m_Rope.OnSimulationStart += Actor_OnSimulationStart;            m_Rope.OnRequestReadback += Actor_OnRequestReadback;            if (m_Rope.solver != null)                Actor_OnBlueprintLoaded(m_Rope, m_Rope.sourceBlueprint);            EnablePinhole();        }

        private void OnDisable()        {            DisablePinhole();            m_Rope.OnBlueprintLoaded -= Actor_OnBlueprintLoaded;            m_Rope.OnSimulationStart -= Actor_OnSimulationStart;            m_Rope.OnRequestReadback -= Actor_OnRequestReadback;        }        private void OnValidate()        {            m_Rope = GetComponent<ObiRopeBase>();
            m_ParametersDirty = true;            m_PositionDirty = true;            m_RangeDirty = true;        }        private void Actor_OnBlueprintLoaded(ObiActor act, ObiActorBlueprint blueprint)        {            Bind();        }        private void Actor_OnSimulationStart(ObiActor act, float stepTime, float substepTime)        {
            // Attachments must be updated at the start of the step, before performing any simulation.
            UpdatePinhole();

            // if there's any broken constraint, flag pinhole constraints as dirty for remerging at the start of the next step.
            BreakPinhole(substepTime);        }

        private void Actor_OnRequestReadback(ObiActor actor)
        {
            if (enabled && m_Rope.isLoaded && isBound)            {
                var solver = m_Rope.solver;

                var actorConstraints = m_Rope.GetConstraintsByType(Oni.ConstraintType.Pinhole) as ObiConstraints<ObiPinholeConstraintsBatch>;
                var solverConstraints = solver.GetConstraintsByType(Oni.ConstraintType.Pinhole) as ObiConstraints<ObiPinholeConstraintsBatch>;

                if (actorConstraints != null && pinBatch != null && actorConstraints.batchCount <= solverConstraints.batchCount)
                {
                    int pinBatchIndex = actorConstraints.batches.IndexOf(pinBatch);
                    if (pinBatchIndex >= 0 && pinBatchIndex < rope.solverBatchOffsets[(int)Oni.ConstraintType.Pinhole].Count)
                    {
                        var solverBatch = solverConstraints.batches[pinBatchIndex];

                        solverBatch.particleIndices.Readback();
                        solverBatch.edgeMus.Readback();
                        solverBatch.relativeVelocities.Readback();
                    }
                }
            }
        }

        private void ClampMuToRange()
        {
            if (m_LimitRange)
            {
                float maxCoord = lastEdge.GetRopeCoordinate(m_Rope);
                float minCoord = firstEdge.GetRopeCoordinate(m_Rope);

                if (m_Position > maxCoord)
                {
                    m_Position = maxCoord;
                    currentEdge.edgeIndex = m_Rope.GetEdgeAt(m_Position, out currentEdge.coordinate);
                    m_PositionDirty = true;
                }
                else if (m_Position < minCoord)
                {
                    m_Position = minCoord;
                    currentEdge.edgeIndex = m_Rope.GetEdgeAt(m_Position, out currentEdge.coordinate);
                    m_PositionDirty = true;
                }
            }
        }

        public void CalculateMu()
        {
            currentEdge.edgeIndex = m_Rope.GetEdgeAt(m_Position, out currentEdge.coordinate);
            ClampMuToRange();

            m_PositionDirty = true;
        }        public void CalculateRange()
        {
            if (m_LimitRange)
            {
                firstEdge.edgeIndex = m_Rope.GetEdgeAt(m_Range.x, out firstEdge.coordinate);
                lastEdge.edgeIndex = m_Rope.GetEdgeAt(m_Range.y, out lastEdge.coordinate);
            }
            else
            {
                firstEdge.edgeIndex = m_Rope.GetEdgeAt(0, out firstEdge.coordinate);
                lastEdge.edgeIndex = m_Rope.GetEdgeAt(1, out lastEdge.coordinate);
                firstEdge.coordinate = -float.MaxValue;
                lastEdge.coordinate = float.MaxValue;
            }

            ClampMuToRange();

            m_RangeDirty = true;
        }        public void Bind()        {
            // Disable pinhole.
            DisablePinhole();            if (m_Target != null && m_Rope.isLoaded)            {                Matrix4x4 bindMatrix = m_Target.worldToLocalMatrix * m_Rope.solver.transform.localToWorldMatrix;

                var ropeBlueprint = m_Rope.sharedBlueprint as ObiRopeBlueprintBase;
                if (ropeBlueprint != null && ropeBlueprint.deformableEdges != null)
                {
                    currentEdge.edgeIndex = m_Rope.GetEdgeAt(m_Position, out currentEdge.coordinate);

                    if (currentEdge.edgeIndex >= 0)
                    {
                        CalculateRange();
                        m_RangeDirty = false;
                        m_PositionDirty = false;

                        int p1 = ropeBlueprint.deformableEdges[currentEdge.edgeIndex * 2];
                        int p2 = ropeBlueprint.deformableEdges[currentEdge.edgeIndex * 2+1];
                        Vector4 pos = Vector4.Lerp(m_Rope.solver.positions[m_Rope.solverIndices[p1]], m_Rope.solver.positions[m_Rope.solverIndices[p2]], currentEdge.coordinate);
                        m_PositionOffset = bindMatrix.MultiplyPoint3x4(pos);
                    }                }            }            else            {                currentEdge.edgeIndex = -1;            }            // Re-enable pinhole.            EnablePinhole();        }        private void EnablePinhole()        {            if (enabled && m_Rope.isLoaded && isBound)            {                var pins = m_Rope.GetConstraintsByType(Oni.ConstraintType.Pinhole) as ObiPinholeConstraintsData;
                attachedCollider = m_Target.GetComponent<ObiColliderBase>();

                if (pins != null && attachedCollider != null && pinBatch == null)
                {
                    // create a new data batch with all our pin constraints:
                    pinBatch = new ObiPinholeConstraintsBatch(pins);
                    pinBatch.AddConstraint(currentEdge,
                                           firstEdge,
                                           lastEdge,
                                           m_Rope,
                                           attachedCollider,
                                           m_PositionOffset,
                                           m_Compliance,
                                           m_Friction,
                                           m_MotorSpeed,
                                           m_MotorForce,
                                           m_ClampAtEnds);

                    pinBatch.activeConstraintCount++;

                    // add the batch to the actor:
                    pins.AddBatch(pinBatch);

                    // store the attached collider's handle:
                    attachedColliderHandleIndex = -1;
                    if (attachedCollider.Handle != null)
                        attachedColliderHandleIndex = attachedCollider.Handle.index;

                    m_Rope.SetConstraintsDirty(Oni.ConstraintType.Pinhole);
                }            }        }        private void DisablePinhole()        {            if (isBound)            {                if (pinBatch != null)
                {
                    var pins = m_Rope.GetConstraintsByType(Oni.ConstraintType.Pinhole) as ObiConstraints<ObiPinholeConstraintsBatch>;
                    if (pins != null)
                    {
                        pins.RemoveBatch(pinBatch);
                        if (rope.isLoaded)
                            m_Rope.SetConstraintsDirty(Oni.ConstraintType.Pinhole);
                    }

                    attachedCollider = null;
                    pinBatch = null;
                    attachedColliderHandleIndex = -1;
                }            }        }        private void UpdatePinhole()        {            if (enabled && m_Rope.isLoaded && isBound)            {
                UpdateEdgeCoordinate();
                UpdateParameters();

                // in case the handle has been updated/invalidated (for instance, when disabling the target) rebuild constraints:
                if (attachedCollider != null &&
                    attachedCollider.Handle != null &&
                    attachedCollider.Handle.index != attachedColliderHandleIndex)
                {
                    attachedColliderHandleIndex = attachedCollider.Handle.index;
                    m_Rope.SetConstraintsDirty(Oni.ConstraintType.Pinhole);
                }            }            else if (!isBound && attachedColliderHandleIndex >= 0)
            {
                attachedColliderHandleIndex = -1;
                m_Rope.SetConstraintsDirty(Oni.ConstraintType.Pinhole);
            }        }

        private void UpdateParameters()
        {
            if (enabled && m_Rope.isLoaded && isBound && m_ParametersDirty)            {
                var solver = m_Rope.solver;

                var actorConstraints = m_Rope.GetConstraintsByType(Oni.ConstraintType.Pinhole) as ObiConstraints<ObiPinholeConstraintsBatch>;
                var solverConstraints = solver.GetConstraintsByType(Oni.ConstraintType.Pinhole) as ObiConstraints<ObiPinholeConstraintsBatch>;

                if (actorConstraints != null && pinBatch != null && actorConstraints.batchCount <= solverConstraints.batchCount)
                {
                    int pinBatchIndex = actorConstraints.batches.IndexOf(pinBatch);
                    if (pinBatchIndex >= 0 && pinBatchIndex < rope.solverBatchOffsets[(int)Oni.ConstraintType.Pinhole].Count)
                    {
                        int offset = rope.solverBatchOffsets[(int)Oni.ConstraintType.Pinhole][pinBatchIndex];
                        var solverBatch = solverConstraints.batches[pinBatchIndex];

                        for (int i = 0; i < pinBatch.activeConstraintCount; i++)
                        {
                            solverBatch.parameters[(offset + i) * 5] = m_Compliance;
                            solverBatch.parameters[(offset + i) * 5+1] = m_Friction;
                            solverBatch.parameters[(offset + i) * 5+2] = m_MotorSpeed;
                            solverBatch.parameters[(offset + i) * 5+3] = m_MotorForce;
                            solverBatch.parameters[(offset + i) * 5+4] = m_ClampAtEnds ? 1 : 0;
                        }
                        solverBatch.parameters.Upload();

                        m_ParametersDirty = false;
                    }
                }
            }
        }        private void UpdateEdgeCoordinate()
        {
            if (enabled && m_Rope.isLoaded && isBound)            {
                var solver = m_Rope.solver;

                var actorConstraints = m_Rope.GetConstraintsByType(Oni.ConstraintType.Pinhole) as ObiConstraints<ObiPinholeConstraintsBatch>;
                var solverConstraints = solver.GetConstraintsByType(Oni.ConstraintType.Pinhole) as ObiConstraints<ObiPinholeConstraintsBatch>;

                if (actorConstraints != null && pinBatch != null && actorConstraints.batchCount <= solverConstraints.batchCount)
                {
                    int pinBatchIndex = actorConstraints.batches.IndexOf(pinBatch);
                    if (pinBatchIndex >= 0 && pinBatchIndex < rope.solverBatchOffsets[(int)Oni.ConstraintType.Pinhole].Count)
                    {
                        int offset = rope.solverBatchOffsets[(int)Oni.ConstraintType.Pinhole][pinBatchIndex];
                        var solverBatch = solverConstraints.batches[pinBatchIndex];

                        solverBatch.particleIndices.WaitForReadback();
                        solverBatch.edgeMus.WaitForReadback();
                        solverBatch.relativeVelocities.WaitForReadback();

                        if (m_RangeDirty)
                        {
                            // update edge index and coordinate, then upload them.
                            for (int i = 0; i < pinBatch.activeConstraintCount; i++)
                            {
                                solverBatch.edgeRanges[(offset + i) * 2] = m_Rope.deformableEdgesOffset + firstEdge.edgeIndex;
                                solverBatch.edgeRanges[(offset + i) * 2 + 1] = m_Rope.deformableEdgesOffset + lastEdge.edgeIndex;
                                solverBatch.edgeRangeMus[(offset + i) * 2] = firstEdge.coordinate;
                                solverBatch.edgeRangeMus[(offset + i) * 2 + 1] = lastEdge.coordinate;
                            }
                            solverBatch.edgeRanges.Upload();
                            solverBatch.edgeRangeMus.Upload();

                            m_RangeDirty = false;
                        }

                        if (m_PositionDirty)
                        {
                            // update edge index and coordinate, then upload them.
                            for (int i = 0; i < pinBatch.activeConstraintCount; i++)
                            {
                                solverBatch.particleIndices[offset + i] = m_Rope.deformableEdgesOffset + currentEdge.edgeIndex;
                                solverBatch.edgeMus[offset + i] = currentEdge.coordinate;
                            }
                            solverBatch.particleIndices.Upload();
                            solverBatch.edgeMus.Upload();

                            m_PositionDirty = false;
                        }
                        else
                        {
                            // read edge index and coordinate:
                            for (int i = 0; i < pinBatch.activeConstraintCount; i++)
                            {
                                currentEdge.coordinate = solverBatch.edgeMus[offset + i];
                                currentEdge.edgeIndex = solverBatch.particleIndices[offset + i] - m_Rope.deformableEdgesOffset;
                                m_Position = currentEdge.GetRopeCoordinate(m_Rope);
                            }
                        }

                        for (int i = 0; i < pinBatch.activeConstraintCount; i++)
                        {
                            relativeVelocity = solverBatch.relativeVelocities[offset + i];
                        }
                    }
                }
            }
        }        private void BreakPinhole(float substepTime)        {            if (enabled && m_Rope.isLoaded && isBound)            {                var solver = m_Rope.solver;                var actorConstraints = m_Rope.GetConstraintsByType(Oni.ConstraintType.Pinhole) as ObiConstraints<ObiPinholeConstraintsBatch>;                var solverConstraints = solver.GetConstraintsByType(Oni.ConstraintType.Pinhole) as ObiConstraints<ObiPinholeConstraintsBatch>;                bool dirty = false;                if (actorConstraints != null && pinBatch != null && actorConstraints.batchCount <= solverConstraints.batchCount)                {                    int pinBatchIndex = actorConstraints.batches.IndexOf(pinBatch);                    if (pinBatchIndex >= 0 && pinBatchIndex < rope.solverBatchOffsets[(int)Oni.ConstraintType.Pinhole].Count)                    {                        int offset = rope.solverBatchOffsets[(int)Oni.ConstraintType.Pinhole][pinBatchIndex];                        var solverBatch = solverConstraints.batches[pinBatchIndex];                        float sqrTime = substepTime * substepTime;                        for (int i = 0; i < pinBatch.activeConstraintCount; i++)                        {
                            // In case the handle has been created/destroyed.
                            if (pinBatch.pinBodies[i] != attachedCollider.Handle)                            {                                pinBatch.pinBodies[i] = attachedCollider.Handle;                                dirty = true;                            }

                            // in case the constraint has been broken:
                            if (-solverBatch.lambdas[offset + i] / sqrTime > breakThreshold)                            {                                pinBatch.DeactivateConstraint(i);                                dirty = true;                            }                        }                    }                }

                // constraints are recreated at the start of a step.
                if (dirty)                    m_Rope.SetConstraintsDirty(Oni.ConstraintType.Pinhole);            }        }    }}