using System;
using System.Windows.Forms;

namespace TcpServer
{
    static class Program
    {
        private static readonly Log logger = LogManager.GetLogger("Program");

        /// <summary>
        /// 해당 애플리케이션의 주 진입점입니다.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                logger.LogInfo($"=== Begin {ProductInfo.PRODUCT_NAME} {ProductInfo.GetVersionString()} ===");

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                logger.LogError(ex.ToString());
                MessageBox.Show(ex.Message, ProductInfo.PRODUCT_NAME, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                logger.LogInfo($"=== End {ProductInfo.PRODUCT_NAME} {ProductInfo.GetVersionString()} ===");
            }
        }
    }
}
