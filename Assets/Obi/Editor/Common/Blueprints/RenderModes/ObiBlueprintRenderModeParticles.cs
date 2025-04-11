using UnityEngine;
using System.Collections;

namespace Obi
{
    public class ObiBlueprintRenderModeParticles : ObiBlueprintRenderMode
    {
        public override string name
        {
            get { return "Particles"; }
        }

        private Shader shader;
        private Material material;
        private ParticleImpostorRendering impostorDrawer;
        private MaterialPropertyBlock mpb;

        public ObiBlueprintRenderModeParticles(ObiActorBlueprintEditor editor) :base(editor)
        {
            impostorDrawer = new ParticleImpostorRendering();
            impostorDrawer.UpdateMeshes(editor.blueprint);
            mpb = new MaterialPropertyBlock();
        }

        void CreateMaterialIfNeeded()
        {
            if (shader == null)
            {
                shader = Shader.Find("Obi/EditorParticles");
                if (shader != null)
                {
                    if (!shader.isSupported)
                        Debug.LogWarning("Particle rendering shader not suported.");

                    if (material == null || material.shader != shader)
                    {
                        GameObject.DestroyImmediate(material);
                        material = new Material(shader);
                        material.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
            }
        }

        public override void DrawWithCamera(Camera camera) 
        {
            CreateMaterialIfNeeded();
            mpb.SetFloat("_RadiusScale", 1);
            mpb.SetColor("_ParticleColor", Color.white);
            foreach (Mesh mesh in impostorDrawer.Meshes)
                Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, camera, 0, mpb);
        }

        public override void Refresh()
        {
            impostorDrawer.UpdateMeshes(editor.blueprint, editor.visible, editor.tint);
        }

        public override void OnDestroy()
        {
            GameObject.DestroyImmediate(material);
            impostorDrawer.ClearMeshes();
        }
    }
}