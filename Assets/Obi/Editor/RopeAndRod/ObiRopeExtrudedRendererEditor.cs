using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi{
	
	[CustomEditor(typeof(ObiRopeExtrudedRenderer)), CanEditMultipleObjects] 
	public class  ObiRopeExtrudedRendererEditor : Editor
	{
		
		ObiRopeExtrudedRenderer renderer;
		
		public void OnEnable(){
			renderer = (ObiRopeExtrudedRenderer)target;
		}

        [MenuItem("CONTEXT/ObiRopeExtrudedRenderer/Bake mesh")]
        static void Bake(MenuCommand command)
        {
            ObiRopeExtrudedRenderer renderer = (ObiRopeExtrudedRenderer)command.context;

            if (renderer.actor.isLoaded)
            {
                var system = renderer.actor.solver.GetRenderSystem<ObiRopeExtrudedRenderer>() as ObiExtrudedRopeRenderSystem;

                if (system != null)
                {
                    var mesh = new Mesh();
                    system.BakeMesh(renderer, ref mesh, true);
                    ObiEditorUtils.SaveMesh(mesh, "Save rope mesh", "rope mesh");
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

