using System;
using System.IO;

class Program
{
    static void Main()
    {
        // مسیر دسکتاپ کاربر
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        // نام فایل متنی
        string fileName = "تاریخ_فعلی.txt";

        // مسیر کامل فایل
        string filePath = Path.Combine(desktopPath, fileName);

        // تاریخ فعلی
        string currentDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // نوشتن تاریخ در فایل
        File.WriteAllText(filePath, currentDate);

        Console.WriteLine("فایل با موفقیت ایجاد شد: " + filePath);
    }
}
