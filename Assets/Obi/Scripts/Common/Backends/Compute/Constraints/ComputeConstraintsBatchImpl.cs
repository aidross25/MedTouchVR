using UnityEngine;

namespace Obi
{
    public abstract class ComputeConstraintsBatchImpl : IConstraintsBatchImpl
    {
        protected IComputeConstraintsImpl m_Constraints;
        protected Oni.ConstraintType m_ConstraintType;

        protected bool m_Enabled = true;
        protected int m_ConstraintCount = 0;

        public Oni.ConstraintType constraintType
        {
            get { return m_ConstraintType; }
        }

        public bool enabled
        {
            set
            {
                if (m_Enabled != value)
                    m_Enabled = value;
            }
            get { return m_Enabled; }
        }

        public IConstraints constraints
        {
            get { return m_Constraints; }
        }

        public ObiSolver solverAbstraction
        {
            get { return ((ComputeSolverImpl)m_Constraints.solver).abstraction; }
        }

        public ComputeSolverImpl solverImplementation
        {
            get { return (ComputeSolverImpl)m_Constraints.solver; }
        }

        protected GraphicsBuffer particleIndices;
        protected GraphicsBuffer lambdas;

        protected ObiNativeFloatList lambdasList;

        public virtual void Initialize(float stepTime, float substepTime, int steps, float timeLeft)
        {
            if (lambdasList != null)
            {
                lambdasList.WipeToZero();
                lambdasList.Upload();
            }
        }

        // implemented by concrete constraint subclasses.
        public abstract void Evaluate(float stepTime, float substepTime, int steps, float timeLeft);
        public abstract void Apply(float substepTime);

        public ComputeConstraintsBatchImpl() { }

        public virtual void Destroy()
        {
            // clean resources allocated by the batch, no need for a default implementation.
        }

        public void SetDependency(IConstraintsBatchImpl batch)
        {
            // no need to implement.
        }

        public void SetConstraintCount(int constraintCount)
        {
            m_ConstraintCount = constraintCount;
        }
        public int GetConstraintCount()
        {
            return m_ConstraintCount;
        }
    }
}