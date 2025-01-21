using Unity.Mathematics;

namespace TiltBrushToolkit
{

    // utils for working with Unity.Mathematics
    public class MathematicsUtils
    {

        public static bool AreFloatsEqual(float a, float b, float epsilon = 0.00001f)
        {
            return math.abs(a - b) < epsilon;
        }

    }
}