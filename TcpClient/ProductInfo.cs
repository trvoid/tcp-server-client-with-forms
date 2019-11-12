using System.Text;

namespace TcpClient
{
    class ProductInfo
    {
        public static string PRODUCT_NAME = "TcpClient";

        public static int VERSION_MAJOR = 0;
        public static int VERSION_MINOR = 2;
        public static int VERSION_FIX = 1;
        public static string VERSION_PHASE = "";
        public static string VERSION_BUILD = "";

        public static string MANUFACTURER_NAME = "";

        public static string GetVersionString()
        {
            StringBuilder builder = new StringBuilder();

            builder.Append((string.Format("{0}.{1}.{2}", VERSION_MAJOR, VERSION_MINOR, VERSION_FIX)));

            if (!string.IsNullOrEmpty(VERSION_PHASE))
            {
                builder.Append(".").Append(VERSION_PHASE);
            }

            if (!string.IsNullOrEmpty(VERSION_BUILD))
            {
                builder.Append(".").Append(VERSION_BUILD);
            }

            return builder.ToString();
        }

        public static string GetBannerString()
        {
            return $"{PRODUCT_NAME} {GetVersionString()} by {MANUFACTURER_NAME}";
        }
    }
}
