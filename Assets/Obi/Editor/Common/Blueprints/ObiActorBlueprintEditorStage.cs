using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Obi
{
    [Serializable]
    class ObiActorBlueprintEditorStage : PreviewSceneStage
    {
        ObiActorBlueprintEditor m_BlueprintEditor;

        string m_AssetPath;
        public override string assetPath { get { return m_AssetPath; } }

        internal static ObiActorBlueprintEditorStage CreateStage(string assetPath, ObiActorBlueprintEditor avatarEditor)
        {
            ObiActorBlueprintEditorStage stage = CreateInstance<ObiActorBlueprintEditorStage>();
            stage.Init(assetPath, avatarEditor);
            return stage;
        }

        private void Init(string modelAssetPath, ObiActorBlueprintEditor avatarEditor)
        {
            m_AssetPath = modelAssetPath;
            m_BlueprintEditor = avatarEditor;
        }

        protected override bool OnOpenStage()
        {
            base.OnOpenStage();

            if (!File.Exists(assetPath))
            {
                Debug.LogError("ActivateStage called on BlueprintStage with an invalid path: Blueprint file not found " + assetPath);
                return false;
            }

            return true;
        }

        protected override void OnCloseStage()
        {
            m_BlueprintEditor.CleanupEditor();

            base.OnCloseStage();
        }

        protected override void OnFirstTimeOpenStageInSceneView(SceneView sceneView)
        {
            // Frame in scene view
            sceneView.Frame(m_BlueprintEditor.blueprint.bounds);

            // Setup Scene view state
            sceneView.sceneViewState.showFlares = false;
            sceneView.sceneViewState.alwaysRefresh = false;
            sceneView.sceneViewState.showFog = false;
            sceneView.sceneViewState.showSkybox = false;
            sceneView.sceneViewState.showImageEffects = false;
            sceneView.sceneViewState.showParticleSystems = false;
            sceneView.sceneLighting = true;
        }

        protected override GUIContent CreateHeaderContent()
        {
            return new GUIContent(
                "Blueprint Editor",
                Resources.Load<Texture2D>("Icons/ObiActorBlueprint Icon"));
        }
    }
}