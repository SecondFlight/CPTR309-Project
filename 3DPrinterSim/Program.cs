using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Threading;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Input;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Hardware;
using Firmware;
using System.Windows.Forms;

namespace PrinterSimulator
{
    class PrintSim
    {
        static void PrintFile(PrinterControl simCtl)
        {
            Console.Clear();
            Console.WriteLine("\nDefault file will be used unless alternate file is given");
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
            OpenFileDialog filePath = new OpenFileDialog();
            string path = "";
            if (filePath.ShowDialog() == DialogResult.OK)
            {
                path = filePath.FileName;
            }
            if (path == "")
            {
                path = "..\\..\\..\\SampleSTLs\\F-35_Corrected.gcode";
            }
            System.IO.StreamReader file = new System.IO.StreamReader(path);

            Stopwatch swTimer = new Stopwatch();
            swTimer.Start();

            // Todo - Read GCODE file and send data to firmware for printing

            swTimer.Stop();
            long elapsedMS = swTimer.ElapsedMilliseconds;

            Console.WriteLine("Total Print Time: {0}", elapsedMS / 1000.0);
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }

        private static void TesterMenu()
        {
            Console.WriteLine("3D Printer Simulation - Tester Menu\n");
            Console.WriteLine("L - Test laser");
            Console.WriteLine("G - Test galvanometer");
            Console.WriteLine("Z - Test Z-Rail");
            Console.WriteLine("S - Test limit switch");
            Console.WriteLine("P - Test Printer");
            Console.WriteLine("Q - Go back to Main Menu");

            char ch = Char.ToUpper(Console.ReadKey().KeyChar);
            switch (ch)
            {
                case 'L':
                    break;
                case 'G':
                    break;
                case 'Z':
                    break;
                case 'S':
                    break;
                case 'P':
                    break;
                case 'Q':
                    break;
            }

        }



        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [STAThread]

        static void Main()
        {

            IntPtr ptr = GetConsoleWindow();
            MoveWindow(ptr, 0, 0, 1000, 400, true);

            // Start the printer - DO NOT CHANGE THESE LINES
            PrinterThread printer = new PrinterThread();
            Thread oThread = new Thread(new ThreadStart(printer.Run));
            oThread.Start();
            printer.WaitForInit();

            // Start the firmware thread - DO NOT CHANGE THESE LINES
            FirmwareController firmware = new FirmwareController(printer.GetPrinterSim());
            oThread = new Thread(new ThreadStart(firmware.Start));
            oThread.Start();
            firmware.WaitForInit();

            SetForegroundWindow(ptr);

            bool fDone = false;
            while (!fDone)
            {
                Console.Clear();
                Console.WriteLine("3D Printer Simulation - Control Menu\n");
                Console.WriteLine("P - Print");
                Console.WriteLine("T - Test");
                Console.WriteLine("Q - Quit");

                char ch = Char.ToUpper(Console.ReadKey().KeyChar);
                switch (ch)
                {
                    case 'P': // Print
                        PrintFile(printer.GetPrinterSim());
                        break;

                    case 'T': // Test menu
                        TesterMenu();
                        break;

                    case 'Q' :  // Quite
                        printer.Stop();
                        firmware.Stop();
                        fDone = true;
                        break;
                }

            }

        }
    }
}