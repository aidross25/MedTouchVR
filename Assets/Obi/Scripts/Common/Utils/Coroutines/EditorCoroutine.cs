using UnityEngine;
#if (UNITY_EDITOR)
	using UnityEditor;
#endif
using System.Collections;

namespace Obi
{
    public class EditorCoroutine
    {

	    public static bool ShowCoroutineProgressBar(string title, IEnumerator coroutine)
        {

            bool cancelled = false;

        #if (UNITY_EDITOR)
            if (coroutine != null){

                while (coroutine.MoveNext() && !cancelled)
                {
                    var progressInfo = coroutine.Current as CoroutineJob.ProgressInfo;
                    cancelled |= EditorUtility.DisplayCancelableProgressBar(title, progressInfo.userReadableInfo, progressInfo.progress);
                }

			    // once finished, set coroutine to null and clear progress bar.
			    coroutine = null;
			    EditorUtility.ClearProgressBar();

            }
        #endif
            return cancelled;
        }
		
    }
}