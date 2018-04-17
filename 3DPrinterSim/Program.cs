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
using gs;   // what is this for?
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
            bool isLaserOn = false;

            foreach (var line in instructions.AllLines())
            {
                if (line.parameters != null)
                {
                    //Console.WriteLine(line.code);
                    if (line.code == 1)
                    {
                        foreach (var parameter in line.parameters)
                        {
                            bool containsX = false;
                            bool containsY = false;
                            bool containsZ = false;
                            bool containsE = false;
                            if (parameter.identifier.ToUpper() == "X")
                            {
                                currentX = parameter.doubleValue;
                                containsX = true;
                            }

                            if (parameter.identifier.ToUpper() == "Y")
                            {
                                currentY = parameter.doubleValue;
                                containsY = true;
                            }

                            if (parameter.identifier.ToUpper() == "Z")
                            {
                                currentZ = parameter.doubleValue;
                                containsZ = true;
                            }

                            if (parameter.identifier.ToUpper() == "E")
                            {
                                isLaserOn = parameter.doubleValue > 0;
                                containsE = true;
                            }

                            // Command number: 0x00
                            // Format: X (4 bytes), Y (4 bytes)
                            if (containsX && containsY)
                            {
                                var bytesToSend = new byte[12];
                                bytesToSend[0] = 0x00; // Command number
                                bytesToSend[1] = 0x08; // Data length
                                bytesToSend[2] = 0x00; // Blank (for checksum)
                                bytesToSend[3] = 0x00; // Blank (for checksum)

                                // Convert x position and y position to a byte array
                                var xBytes = BitConverter.GetBytes((float)currentX);
                                var yBytes = BitConverter.GetBytes((float)currentY);

                                // Insert x position
                                for (int i = 0; i < xBytes.Length; i++)
                                {
                                    bytesToSend[i + 4] = xBytes[i];
                                }

                                // Insert y position
                                for (int i = 0; i < yBytes.Length; i++)
                                {
                                    bytesToSend[i + 8] = yBytes[i];
                                }

                                HostToFirmware(bytesToSend, simCtl); // Send data
                            }

                            // Command number: 0x01
                            // Format: Z (4 bytes)
                            else if (containsZ)
                            {

                            }
                            else if (containsX || containsY)
                            {
                                throw new InvalidDataException("Invalid GCode command - command contains one X/Y command without the other.");
                            }

                            // Separately, if it contains an E
                            // Command number: 0x02
                            // Format: isLaserOn (1 byte)
                            if (containsE)
                            {

                            }
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

        static byte[] Checksum(byte[] packet)
        {
            byte[] return_packet = packet;
            ushort checksum = 0;
            //      When adding bytes, the checksum fields are initialized to zero.
            return_packet[2] = 0x00;
            return_packet[3] = 0x00;

            //      Add up all the bytes in the entire command packet including command byte, length, etc. 
            for (int i = 0; i < return_packet.Length; i++)
            {
                checksum = (ushort)(return_packet[i] + checksum);
            }

            //      After the checksum for the command is calculated, the checksum bytes(2 & 3) are set with the calculated checksum.
            return_packet[2] = (byte)checksum;
            return_packet[3] = (byte)(checksum >> 8);

            return return_packet;
        }
        static string HostToFirmware(byte[] packet, PrinterControl simCtl) // MAKE COPIES OF ALL THE THINGS
        {
            //        Host-to-Firmware Communication Procedure
            int header_size = 4;    // 4 is header size
            int response_size = 0;  // UNSURE OF SIZE OF RESPONSE
            int ACK_NAK_size = 1;   // size of both ACK and NAK bytes
            byte null_byte = 0x30;
            byte[] ACK = { 0xA5 };  // ACK byte 
            byte[] NAK = { 0xFF };  // ACK byte
            string success = "SUCCESS";

            //      Send 4-byte header consisting of command byte, length, and 16-bit checksum
            byte[] checksummed_packet = Checksum(packet);
            byte[] header = checksummed_packet.Skip(0).Take(header_size).ToArray();   // array substring from Skip and Take, 0 to 4
            var header_copy = header;   // making a copy for header to go in
            int header_bytes_sent = simCtl.WriteSerialToFirmware(header_copy, header_size);

            //      Read header bytes back from firmware to verify correct receipt of command header
            byte[] possible_header = { 0x00 };
            int header_bytes_recieved = simCtl.ReadSerialFromFirmware(possible_header, header_size); 
            
            //      If header is correct
            if (header.SequenceEqual(possible_header))  // header == possible_header
            {
                //      Send ACK(0xA5) to firmware
                int ACK_send = simCtl.WriteSerialToFirmware(ACK, ACK_NAK_size);    // 1 is the size of the ACK and NAK bytes

                //      Send rest of packet not including the 4-byte header
                byte[] rest_bytes_send = checksummed_packet.Skip(header_size).Take(checksummed_packet.Length - header_size).ToArray();  // array substring
                int rest_bytes_sent = simCtl.WriteSerialToFirmware(rest_bytes_send, packet.Length - header_size);

                //      Wait for first byte of response to be received
                byte[] response_bytes = { 0x00 };
                int response_bytes_recieved = simCtl.ReadSerialFromFirmware(response_bytes, response_size);
                
                while (response_bytes_recieved < 1)
                {
                    ;   // wait
                }

                //      Continue reading rest of response until null byte (0) is received
                while (true)
                {
                    if (response_bytes[response_bytes.Length - 1] == null_byte) // should I use .SequenceEqual()?
                    {
                        break;  // exit the wait loop
                    }
                }

                string response_string = System.Text.Encoding.ASCII.GetString(response_bytes);

                //      Verify that response string equals “SUCCESS” or “VERSION n.n” (If not, re-send entire command)
                if (response_string == success)    //  || response_bytes == "VERSION n.n"
                {
                    return success;
                }
                else
                {
                    //      retry command
                    return HostToFirmware(packet, simCtl);  // this retries the command and returns the result of that command
                }
            }
            //      else if header is not received correctly
            else
            {
                //      Send NAK(0xFF)
                int NAK_send = simCtl.WriteSerialToFirmware(NAK, ACK_NAK_size);

                //      Retry command
                return HostToFirmware(packet, simCtl);  // this retries the command and returns the result of that command
            }
        }

        //  This function will be later moved to Firmware.cs as it is Firmware to Host side of things
        

        //static string ToString(byte[] bytes)
        //{
        //    string response = string.Empty;

        //    foreach (byte b in bytes)
        //        response += (Char)b;

        //    return response;
        //}

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