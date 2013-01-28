
namespace FindMeChicken_ASP.Lib
{
    public class NumberScale
    {
        public static int ScaleNumber(int OldValue, int OldMin, int OldMax, int NewMax, int NewMin)
        {
            return (((OldValue - OldMin) * (NewMax - NewMin)) / (OldMax - OldMin)) + NewMin;
        }
    }
}