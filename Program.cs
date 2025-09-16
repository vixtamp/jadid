using System;
using System.IO;

namespace RloFileRenamer
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                // دریافت مسیر فایل
                Console.WriteLine("Please enter the full path of the file :");
                string filePath = Console.ReadLine();

                // اعتبارسنجی مسیر فایل
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    Console.WriteLine("Error: Invalid or non-existent file path.");
                    return;
                }

                // دریافت پسوند جعلی
                Console.WriteLine("Please enter the fake extension (e.g., jpg, pdf, txt):");
                string fakeExtension = Console.ReadLine()?.Trim();

                if (string.IsNullOrWhiteSpace(fakeExtension))
                {
                    Console.WriteLine("Error: Fake extension cannot be empty.");
                    return;
                }

                // کاراکتر RLO (U+202E)
                char rloChar = '\u202E';

                // استخراج نام فایل و پسوند اصلی
                string directory = Path.GetDirectoryName(filePath);
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
                string originalExtension = Path.GetExtension(filePath);

                // ساخت نام جدید با تکنیک RLO
                string newFileName = $"{fileNameWithoutExtension}{rloChar}{fakeExtension}{originalExtension}";
                string newFilePath = Path.Combine(directory, newFileName);

                // تغییر نام فایل
                File.Move(filePath, newFilePath);

                Console.WriteLine($"Success! File renamed to: {newFileName}");
                Console.WriteLine("In File Explorer, it will appear as: " +
                    $"{fileNameWithoutExtension}{originalExtension}.{fakeExtension}");
                Console.WriteLine($"New file path: {newFilePath}");
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Error: Access denied. Run the program as Administrator or check file permissions.");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error: An I/O error occurred: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: An unexpected error occurred: {ex.Message}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}