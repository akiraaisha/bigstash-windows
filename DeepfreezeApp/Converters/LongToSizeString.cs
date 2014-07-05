using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeepfreezeApp
{
    public static class LongToSizeString
    {
        private static double ConvertToKB(double size)
        {
            return size / 1024;
        }
        private static double ConvertToMB(double size)
        {
            return ConvertToKB(size) / 1024;
        }
        private static double ConvertToGB(double size)
        {
            return ConvertToMB(size) / 1024;
        }

        public static string ConvertToString(double size)
        {
            double sizeInKB = ConvertToKB(size);
            double sizeInMB = ConvertToMB(size);
            double sizeInGB = ConvertToGB(size);

            StringBuilder sizeString = new StringBuilder();

            if (sizeInMB < 1)
                sizeString.Append(Math.Round(sizeInKB, 2).ToString() + " KB");
            else if (sizeInGB < 1)
                sizeString.Append(Math.Round(sizeInMB, 2).ToString() + " MB");
            else
                sizeString.Append(Math.Round(sizeInGB, 2).ToString() + " GB");

            return sizeString.ToString();
        }
    }
}
