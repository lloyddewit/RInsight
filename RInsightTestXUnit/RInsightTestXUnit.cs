using RInsight;

namespace RInsightTestXUnit
{
    public class RInsightTestXUnit
    {
        [Fact]
        public void Test1()
        {
            RInsight.Class1 c1 = new RInsight.Class1();
            Assert.True(RInsight.Class1.ReturnTrue());
        }
    }
}