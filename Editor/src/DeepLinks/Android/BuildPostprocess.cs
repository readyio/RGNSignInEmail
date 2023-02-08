using System.IO;
using RGN.Modules.SignIn;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RGN.MyEditor
{
    public class BuildPostprocess : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            var platform = report.summary.platform;
            Debug.Log("[EmailSignIn]: On postprocess build begin, platform: " + platform);
            if (platform == BuildTarget.Android)
            {
                HandleAndroid();
            }
        }

        private void HandleAndroid()
        {
            string packageNameScheme = RGNHttpUtility.GetSanitizedApplicationIdentifier();
            string path = RGNHttpUtility.EMAIL_SIGN_IN_PATH;

            string manifestPath = Application.dataPath + "/Plugins/Android/AndroidManifest.xml";
            if (File.Exists(manifestPath))
            {
                Debug.Log("[EmailSignIn]: Found AndroidManifest at " + manifestPath);
                string manifestContent = File.ReadAllText(manifestPath);
                string NEW_INTENT = "<intent-filter>\n" +
                    "<action android:name=\"android.intent.action.VIEW\"/>\n" +
                    "<category android:name=\"android.intent.category.DEFAULT\"/>\n" +
                    "<category android:name=\"android.intent.category.BROWSABLE\"/>\n" +
                    "<data android:scheme=\"" + packageNameScheme + "\" android:host=\"localhost\" android:path=\"" + path + "\"/>\n" +
                    "</intent-filter>\n";

                if (!manifestContent.Contains(NEW_INTENT))
                {
                    // Add the new intent
                    int insertIndex = manifestContent.IndexOf("</activity>");
                    manifestContent = manifestContent.Insert(insertIndex, NEW_INTENT);
                }
                File.WriteAllText(manifestPath, manifestContent);
                Debug.Log("[EmailSignIn]: Android Manifest updated: " + manifestContent);
                return;
            }
            Debug.LogError("[EmailSignIn]: Can not find Android Manifest at: " + manifestPath);
        }
    }
}
