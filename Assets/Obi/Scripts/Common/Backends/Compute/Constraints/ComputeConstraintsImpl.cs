using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
    public interface IComputeConstraintsImpl : IConstraints
    {
        void Initialize(float stepTime, float substepTime, int steps, float timeLeft);
        void Project(float stepTime, float substepTime, int substeps, float timeLeft);
        void Dispose();

        IConstraintsBatchImpl CreateConstraintsBatch();
        void RemoveBatch(IConstraintsBatchImpl batch);
    }

    public abstract class ComputeConstraintsImpl<T> : IComputeConstraintsImpl where T : ComputeConstraintsBatchImpl
    {
        protected ComputeSolverImpl m_Solver;
        public List<T> batches = new List<T>();

        protected Oni.ConstraintType m_ConstraintType;

        public Oni.ConstraintType constraintType
        {
            get { return m_ConstraintType; }
        }

        public ISolverImpl solver
        {
            get { return m_Solver; }
        }

        public ComputeConstraintsImpl(ComputeSolverImpl solver, Oni.ConstraintType constraintType)
        {
            this.m_ConstraintType = constraintType;
            this.m_Solver = solver;
        }

        public virtual void Dispose()
        {

        }

        public abstract IConstraintsBatchImpl CreateConstraintsBatch();

        public abstract void RemoveBatch(IConstraintsBatchImpl batch);

        public virtual int GetConstraintCount()
        {
            int count = 0;
            if (batches == null) return count;

            foreach (T batch in batches)
                if (batch != null)
                    count += batch.GetConstraintCount();

            return count;
        }

        public void Initialize(float stepTime, float substepTime, int steps, float timeLeft)
        {
            // initialize all batches in parallel:
            if (batches.Count > 0)
            {
                for (int i = 0; i < batches.Count; ++i)
                    if (batches[i].enabled) batches[i].Initialize(stepTime, substepTime, steps, timeLeft);
            }

        }

        public void Project(float stepTime, float substepTime, int substeps, float timeLeft)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Project");

            var parameters = m_Solver.abstraction.GetConstraintParameters(m_ConstraintType);

            switch(parameters.evaluationOrder)
            {
                case Oni.ConstraintParameters.EvaluationOrder.Sequential:
                    EvaluateSequential(stepTime, substepTime, substeps, timeLeft);
                break;

                case Oni.ConstraintParameters.EvaluationOrder.Parallel:
                    EvaluateParallel(stepTime, substepTime, substeps, timeLeft);
                break;
            }

            UnityEngine.Profiling.Profiler.EndSample();
            
        }

        protected virtual void EvaluateSequential(float stepTime, float substepTime, int substeps, float timeLeft)
        {
            // evaluate and apply all batches:
            for (int i = 0; i < batches.Count; ++i)
            {
                if (batches[i].enabled)
                {
                    batches[i].Evaluate(stepTime, substepTime, substeps, timeLeft);
                    batches[i].Apply(substepTime);
                }
            }
        }

        protected virtual void EvaluateParallel(float stepTime, float substepTime, int substeps, float timeLeft)
        {
            // evaluate all batches:
            for (int i = 0; i < batches.Count; ++i)
                if (batches[i].enabled)
                {
                    batches[i].Evaluate(stepTime, substepTime, substeps, timeLeft);
                }

            // then apply them:
            for (int i = 0; i < batches.Count; ++i)
                if (batches[i].enabled)
                {
                    batches[i].Apply(substepTime);
                }
        }

    }
}
