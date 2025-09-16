using System;
using System.Drawing;
using System.Windows.Forms;

class Program
{
    static void Main()
    {
        try
        {
            // ابعاد صفحه نمایش
            int screenWidth = Screen.PrimaryScreen.Bounds.Width;
            int screenHeight = Screen.PrimaryScreen.Bounds.Height;

            // ایجاد یک تصویر Bitmap با ابعاد صفحه
            using (Bitmap bitmap = new Bitmap(screenWidth, screenHeight))
            {
                // گرفتن گرافیک از تصویر
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    // گرفتن اسکرین شات
                    g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
                }

                // مسیر دسکتاپ
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string fileName = "Screenshot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
                string filePath = System.IO.Path.Combine(desktopPath, fileName);

                // ذخیره تصویر به صورت PNG
                bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);

                Console.WriteLine("اسکرین شات با موفقیت ذخیره شد: " + filePath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("خطا در گرفتن اسکرین شات: " + ex.Message);
        }
    }
}
