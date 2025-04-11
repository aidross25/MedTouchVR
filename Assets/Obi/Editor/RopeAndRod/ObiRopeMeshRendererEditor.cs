using UnityEditor;
using UnityEngine;

namespace Obi{
	
	[CustomEditor(typeof(ObiRopeMeshRenderer)), CanEditMultipleObjects] 
	public class  ObiRopeMeshRendererEditor : Editor
	{
		
		ObiRopeMeshRenderer renderer;

        [MenuItem("CONTEXT/ObiRopeMeshRenderer/Bake mesh")]
        static void Bake(MenuCommand command)
        {
            ObiRopeMeshRenderer renderer = (ObiRopeMeshRenderer)command.context;

            if (renderer.actor.isLoaded)
            {
                var system = renderer.actor.solver.GetRenderSystem<ObiRopeMeshRenderer>() as ObiMeshRopeRenderSystem;

                if (system != null)
                {
                    var mesh = new Mesh();
                    system.BakeMesh(renderer, ref mesh, true);
                    ObiEditorUtils.SaveMesh(mesh, "Save rope mesh", "rope mesh");
                    GameObject.DestroyImmediate(mesh);
                }
            }
        }

        public void OnEnable(){
			renderer = (ObiRopeMeshRenderer)target;
		}

        public override void OnInspectorGUI() {
			
			serializedObject.UpdateIfRequiredOrScript();

            Editor.DrawPropertiesExcluding(serializedObject,"m_Script");
			
			// Apply changes to the serializedProperty
			if (GUI.changed){
				
				serializedObject.ApplyModifiedProperties();
				
			}
			
		}
		
	}
}

