using UnityEngine;

namespace Obi
{
    public interface ObiRenderer<T> where T : ObiRenderer<T>
    {
        protected RenderSystem<T> CreateRenderSystem(ObiSolver solver);

        public bool ValidateRenderer() { return true; }
        public void CleanupRenderer() { }

        protected bool UnregisterRenderer(ObiSolver solver)
        {
            CleanupRenderer();

            // try to get a render system from the solver:
            var system = solver.GetRenderSystem<T>();

            // if there's an existing render system for this kind of renderer,
            // unregister from it.
            if (system != null && system.RemoveRenderer((T)this))
            {
                // if the render system is empty, destroy it:
                if (system.isEmpty)
                {
                    solver.UnregisterRenderSystem(system);
                    system.Dispose();
                }

                solver.dirtyRendering |= (int)system.typeEnum;
                return true;
            }

            return false;
        }

        protected bool RegisterRenderer(ObiSolver solver)
        {
            if (ValidateRenderer())
            {
                // try to get a render system from the solver:
                var system = solver.GetRenderSystem<T>();

                // if no appropiate system has been created yet, create it:
                if (system == null)
                {
                    system = CreateRenderSystem(solver) as RenderSystem<T>;
                    solver.RegisterRenderSystem(system);
                }

                // register in the renderer:
                if (system != null)
                {
                    system.AddRenderer((T)this);
                    solver.dirtyRendering |= (int)system.typeEnum;
                    return true;
                }
            }

            return false;
        }
    }
}
