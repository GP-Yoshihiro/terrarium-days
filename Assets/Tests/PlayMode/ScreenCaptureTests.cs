using System.Collections;
using System.IO;
using NUnit.Framework;
using TerrariumDays.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace TerrariumDays.Tests
{
    /// <summary>
    /// Renders the real Terrarium scene at iPhone 15 Pro resolution into Logs/Screens/*.png
    /// so layout and animation can be checked without a device. Explicit: it only runs when
    /// selected, e.g. scripts/run-unity-tests.sh with
    /// -testFilter TerrariumDays.Tests.ScreenCaptureTests (see scripts/capture-screens.sh).
    /// Safe-area insets are not simulated (the editor reports a full-screen safe area).
    /// </summary>
    [Explicit("Writes screenshots; run on demand via scripts/capture-screens.sh")]
    public sealed class ScreenCaptureTests
    {
        private const int Width = 1179;
        private const int Height = 2556;

        private RenderTexture target;
        private PanelSettings panelSettings;

        [UnityTest]
        public IEnumerator CaptureTerrariumScreens()
        {
            SceneManager.LoadScene("Terrarium");
            yield return null;

            var document = Object.FindFirstObjectByType<UIDocument>();
            Assert.That(document, Is.Not.Null);
            var view = document.GetComponent<TerrariumView>();

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            panelSettings = document.panelSettings;
            panelSettings.targetTexture = target;

            var outputDir = Path.Combine(Application.dataPath, "..", "Logs", "Screens");
            Directory.CreateDirectory(outputDir);

            yield return new WaitForSeconds(1.5f);
            yield return Capture(outputDir, "01-idle");

            view.OnCleanClicked();
            yield return new WaitForSeconds(0.45f);
            yield return Capture(outputDir, "02-happy-hearts");

            yield return new WaitForSeconds(2f);
            view.OnFeedClicked();
            yield return new WaitForSeconds(0.3f);
            yield return Capture(outputDir, "03-eat");

            yield return new WaitForSeconds(2.2f);
            for (var i = 0; i < 4; i++)
            {
                view.OnPetTapped();
                yield return new WaitForSeconds(0.1f);
            }

            yield return new WaitForSeconds(0.2f);
            yield return Capture(outputDir, "04-threat");
        }

        [TearDown]
        public void TearDown()
        {
            if (panelSettings != null)
            {
                panelSettings.targetTexture = null;
            }

            if (target != null)
            {
                target.Release();
                Object.Destroy(target);
            }
        }

        private IEnumerator Capture(string outputDir, string name)
        {
            yield return null;
            yield return null;

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            File.WriteAllBytes(Path.Combine(outputDir, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
