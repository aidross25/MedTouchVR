
namespace Obi
{
    public static class ComputeMath
    {
        public static int ThreadGroupCount(int elements, int numThreads)
        {
            return elements / numThreads + 1;
        }

        public static int NextMultiple(int baseNumber, int number)
        {
            return ((baseNumber / number) + 1) * number;
        }
    }
}
