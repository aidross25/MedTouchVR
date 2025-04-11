namespace Obi
{
    public class ComputeBackend : IObiBackend
    {
        #region Solver
        public ISolverImpl CreateSolver(ObiSolver solver, int capacity)
        {
            return new ComputeSolverImpl(solver);
        }
        public void DestroySolver(ISolverImpl  solver)
        {
            if (solver != null)
                solver.Destroy();
        }
        #endregion
    }
}
