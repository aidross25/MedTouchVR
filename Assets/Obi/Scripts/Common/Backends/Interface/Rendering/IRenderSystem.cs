using System;
using System.Collections.Generic;

namespace Obi
{
    public interface IRenderSystem
    {
        void Setup(); // build meshes, prepare render state, etc.
        void Step(); // update constraints (currently only used by skinned cloth)
        void Render(); // do the actual rendering.

        void Dispose();

        uint tier { get { return 1; } }
        Oni.RenderingSystemType typeEnum { get; }

        bool isEmpty { get; }
        Type GetRendererType();
    }

    public class RendererSet<T> where T : ObiRenderer<T>
    {
        private List<T> list = new List<T>();

        public T this[int i]
        {
            get { return list[i]; }
            set { list[i] = value; }
        }

        public int Count
        {
            get { return list.Count; }
        }

        public bool AddRenderer(T renderer)
        {
            // Even though using a HashSet would keep us from checking for
            // duplicates in O(n), the only way to iterate trough a set
            // causes GC. Since we iterate trough renderers every frame,
            // but we only add new renderers once in a blue moon,
            // it's preferable to use a list instead.

            if (!list.Contains(renderer))
            {
                list.Add(renderer);
                return true;
            }
            return false;
        }

        public int IndexOf(T renderer)
        {
            return list.IndexOf(renderer);
        }

        public IReadOnlyList<T> AsReadOnly()
        {
            return list.AsReadOnly();
        }

        public bool RemoveRenderer(T renderer)
        {
            return list.Remove(renderer);
        }

        public void Clear()
        {
            list.Clear();
        }
    }

    public interface RenderSystem<T> : IRenderSystem where T : ObiRenderer<T>
    {
        RendererSet<T> renderers { get; }

        Type IRenderSystem.GetRendererType() { return typeof(T); }

        bool IRenderSystem.isEmpty
        {
            get { return renderers.Count == 0; }
        }

        public virtual bool AddRenderer(T renderer)
        {
            return renderers.AddRenderer(renderer);
        }
        public virtual bool RemoveRenderer(T renderer)
        {
            return renderers.RemoveRenderer(renderer);
        }
    }
}
