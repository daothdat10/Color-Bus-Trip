using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KJam
{
    public class DomainExpansion : MonoBehaviour
    {
#if UNITY_EDITOR
        public KeyCode keyCode;
        void Update()
        {
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(keyCode))
            {
                string name = DateTime.Now.ToString();
                name = name.Replace("/", "_");
                name = name.Replace(":", "_");
                name = name.Replace(" ", "_");
                ScreenCapture.CaptureScreenshot(name + ".png");
                UnityEditor.AssetDatabase.Refresh();
                Debug.Log($"Capture Screen");
            }
        }
#endif
    }
}
