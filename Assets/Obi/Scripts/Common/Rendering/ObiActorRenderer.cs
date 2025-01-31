
namespace Obi
{
    public interface IActorRenderer
    {
        public ObiActor actor
        {
            get;
        }
    }

    public interface ObiActorRenderer<T> : IActorRenderer, ObiRenderer<T> where T : ObiActorRenderer<T>
    {
        public void EnableRenderer()
        {
            actor.OnBlueprintLoaded += ObiActorRenderer_OnBlueprintLoaded;
            actor.OnBlueprintUnloaded += ObiActorRenderer_OnBlueprintUnloaded;

            if (actor.isLoaded)
                RegisterRenderer(actor.solver);
        }

        public void DisableRenderer()
        {
            if (actor.isLoaded)
                UnregisterRenderer(actor.solver);

            actor.OnBlueprintLoaded -= ObiActorRenderer_OnBlueprintLoaded;
            actor.OnBlueprintUnloaded -= ObiActorRenderer_OnBlueprintUnloaded;
        }

        public void SetRendererDirty(Oni.RenderingSystemType type)
        {
            if (actor != null)
                actor.SetRenderingDirty(type);
        }

        private void ObiActorRenderer_OnBlueprintLoaded(ObiActor act, ObiActorBlueprint blueprint)
        {
            RegisterRenderer(act.solver);
        }

        protected void ObiActorRenderer_OnBlueprintUnloaded(ObiActor act, ObiActorBlueprint blueprint)
        {
            UnregisterRenderer(act.solver);
        }
    }
}
