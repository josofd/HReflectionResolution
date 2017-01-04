using System;
using System.Text;

namespace HReflectionResolution
{
    internal class Extensions
    {
        /// <summary>
        /// Generates a random sequence of numbers and upper letters with the specified size.
        /// </summary>
        /// <param name="size">The size.</param>
        /// <returns></returns>
        public static string RandomNumberString(int size)
        {
            char[] charList = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

            StringBuilder builder = new StringBuilder();
            Random random = new Random();
            char ch;

            for (int i = 0; i < size; i++)
            {
                ch = charList[Convert.ToInt32(Math.Floor(36 * random.NextDouble()))];
                builder.Append(ch);
            }

            return builder.ToString().ToUpper();
        }
    }
}
