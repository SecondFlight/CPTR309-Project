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
using gs;
using System.IO;

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
            StreamReader file = new System.IO.StreamReader(path);

            Stopwatch swTimer = new Stopwatch();
            swTimer.Start();

            // Parse the GCode file
            var parser = new GenericGCodeParser();
            var instructions = parser.Parse(file);

            /*
             * Commands to note:
             * G28: Home all axes
             * G28 X0: Home x axis
             * 
             * 
             */

            double currentX = 0;
            double currentY = 0;
            double currentZ = 0;
            double currentSize = 0;

            foreach (var line in instructions.AllLines())
            {
                if (line.parameters != null)
                {
                    //Console.WriteLine(line.code);
                    if (line.code == 1)
                    {
                        foreach (var parameter in line.parameters)
                        {
                            if (parameter.identifier.ToUpper() == "X")
                            {
                                currentX = parameter.doubleValue;
                            }

                            if (parameter.identifier.ToUpper() == "Y")
                            {
                                currentY = parameter.doubleValue;
                            }

                            if (parameter.identifier.ToUpper() == "Z")
                            {
                                currentZ = parameter.doubleValue;
                            }

                            if (parameter.identifier.ToUpper() == "E")
                            {
                                currentSize = parameter.doubleValue;
                            }

                            // TODO: Send printer command (replace below with function call)
                            Console.WriteLine("X: " + currentX.ToString() + ", Y: " + currentY.ToString() + ", Z: " + currentZ.ToString() + ", size: " + currentSize.ToString());
                        }
                    }
                    /*
                    foreach (var parameter in line.parameters)
                    {
                        if (parameter.identifier.ToUpper() == "E")
                        {
                            
                        }
                    }*/
        }
    }

            swTimer.Stop();
            long elapsedMS = swTimer.ElapsedMilliseconds;

            Console.WriteLine("Total Print Time: {0}", elapsedMS / 1000.0);
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
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