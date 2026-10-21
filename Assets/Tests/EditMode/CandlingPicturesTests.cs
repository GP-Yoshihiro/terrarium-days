using System;
using NUnit.Framework;
using TerrariumDays.Core;
using TerrariumDays.UI;

namespace TerrariumDays.Tests
{
    public class CandlingPicturesTests
    {
        [Test]
        public void EveryStagePictureLoadsThroughTheViewPath()
        {
            var textures = IncubatorView.LoadCandlingTextures();

            foreach (CandlingStage stage in Enum.GetValues(typeof(CandlingStage)))
            {
                var name = "candling_" + IncubatorView.FileStage(stage);
                Assert.IsTrue(textures.TryGetValue(name, out var texture), name + " missing");
                Assert.IsNotNull(texture, name);
                Assert.Greater(texture.width, 0, name);
            }
        }
    }
}
