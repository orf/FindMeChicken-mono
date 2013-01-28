
namespace FindMeChicken_ASP.Lib
{
    public interface ILocationProvider
    {
        string GetPostcodeFromLatLong(float lat, float lon);
    }
}
