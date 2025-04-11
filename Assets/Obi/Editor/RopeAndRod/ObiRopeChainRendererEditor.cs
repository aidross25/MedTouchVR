using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi{
	
	[CustomEditor(typeof(ObiRopeChainRenderer)), CanEditMultipleObjects] 
	public class  ObiRopeChainRendererEditor : Editor
	{
		
		ObiRopeChainRenderer renderer;
		
		public void OnEnable(){
			renderer = (ObiRopeChainRenderer)target;
		}

        [MenuItem("CONTEXT/ObiRopeChainRenderer/Bake mesh")]
        static void Bake(MenuCommand command)
        {
            ObiRopeChainRenderer renderer = (ObiRopeChainRenderer)command.context;

            if (renderer.actor.isLoaded)
            {
                var system = renderer.actor.solver.GetRenderSystem<ObiRopeChainRenderer>() as ObiChainRopeRenderSystem;

                if (system != null)
                {
                    var mesh = new Mesh();
                    system.BakeMesh(renderer, ref mesh, true);
                    ObiEditorUtils.SaveMesh(mesh, "Save chain mesh", "chain mesh");
                    GameObject.DestroyImmediate(mesh);
                }
            }
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

