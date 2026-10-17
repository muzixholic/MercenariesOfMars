using NUnit.Framework;
namespace MercenariesOfMars.Tests
{
    public sealed class WeaponSelectionLayoutTests
    {
        [TestCase(640,480)] [TestCase(1280,720)] [TestCase(1920,1080)] [TestCase(1280,1024)] [TestCase(2560,1080)] [TestCase(3840,2160)]
        public void 무장선택창은_화면비와_해상도가_달라도_중앙과_화면경계를_지킨다(int width,int height)
        {
            var r=WeaponSelectionView.CalculateRect(width,height);
            Assert.That(r.center.x,Is.EqualTo(width/2f).Within(.1f)); Assert.That(r.center.y,Is.EqualTo(height/2f).Within(.1f));
            Assert.That(r.xMin,Is.GreaterThanOrEqualTo(0)); Assert.That(r.yMin,Is.GreaterThanOrEqualTo(0));
            Assert.That(r.xMax,Is.LessThanOrEqualTo(width)); Assert.That(r.yMax,Is.LessThanOrEqualTo(height));
        }
    }
}
