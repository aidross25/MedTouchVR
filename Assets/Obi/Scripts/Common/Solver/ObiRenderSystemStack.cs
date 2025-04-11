using System;
using System.Collections.Generic;

namespace Obi
{
    /**
     * Render Systems are organized into a fixed number of tiers. Tiers are update sequentially from lower to highest, so systems on
     * a higher tier may depend on the output of lower tier systems.
     * All systems in the same tier might be updated in parallel.
     */
    public class ObiRenderSystemStack
    {
        private List<IRenderSystem>[] stack;

        public ObiRenderSystemStack(int tiers)
        {
            stack = new List<IRenderSystem>[tiers];
            for (int i = 0; i < tiers; ++i)
                stack[i] = new List<IRenderSystem>();
        }

        public void Setup(int dirtyFlags)
        {
            foreach (var tier in stack)
                foreach (var system in tier)
                    if ((dirtyFlags & (int)system.typeEnum) != 0)
                        system.Setup();
        }

        public void Step()
        {
            foreach (var tier in stack)
                foreach (var system in tier)
                    system.Step();
        }

        public void Render()
        {
            foreach (var tier in stack)
                foreach (var system in tier)
                    system.Render();
        }

        public bool RegisterRenderSystem(IRenderSystem renderSystem)
        {
            if (renderSystem != null)
            {
                // Here we don't check whether the render system already exists:
                // We assume this is done before calling AddRenderSystem
                // (by calling GetRenderSystem first).
                // Otherwise, no guarantees a render system cannot be registered twice.
                if (renderSystem.tier >= 0 && renderSystem.tier < stack.Length)
                {
                    stack[renderSystem.tier].Add(renderSystem);
                    return true;
                }
            }
            return false;
        }

        public bool UnregisterRenderSystem(IRenderSystem renderSystem)
        {
            if (renderSystem != null)
            {
                if (renderSystem.tier >= 0 && renderSystem.tier < stack.Length)
                {
                    stack[renderSystem.tier].Remove(renderSystem);
                    return true;
                }
            }
            return false;
        }

        public RenderSystem<T> GetRenderSystem<T>() where T : ObiRenderer<T>
        {
            foreach (var tier in stack)
                foreach (var system in tier)
                    if (system.GetRendererType() == typeof(T))
                        return system as RenderSystem<T>;
            return null;
        }

        public IRenderSystem GetRenderSystem(Oni.RenderingSystemType systemType)
        {
            foreach (var tier in stack)
                foreach (var system in tier)
                    if (system.typeEnum == systemType)
                        return system as IRenderSystem;
            return null;
        }
    }
}
