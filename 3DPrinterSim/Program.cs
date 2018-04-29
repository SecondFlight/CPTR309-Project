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
            //Console.Clear();
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
            bool oldIsLaserOn = false;

            int instructionCount = 0;

            foreach (var line in instructions.AllLines())
            {
                if (line.parameters != null)
                {
                    //Console.WriteLine(line.code);
                    if (line.code == 1)
                    {
                        bool containsX = false;
                        bool containsY = false;
                        bool containsZ = false;
                        bool containsE = false;
                        foreach (var parameter in line.parameters)
                        {
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
                        }

                        // Command number: 0x00
                        // Format: X (4 bytes), Y (4 bytes)
                        if (containsX && containsY)
                        {
                            instructionCount++;
                            //continue;
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

                            // Send data
                            if (!HostToFirmware(bytesToSend, simCtl))   // NOTE: the first byte here is 8!!!!!!!!!!!!!=============
                            {
                                //Console.Write("retry");
                                Console.WriteLine("Print failed - command failed to send.");
                                return;
                            }
                        }

                        // Command number: 0x01
                        // Format: Z (4 bytes)
                        else if (containsZ)
                        {
                            instructionCount++;
                            //continue;
                            var bytesToSend = new byte[8];
                            bytesToSend[0] = 0x01; // Command number
                            bytesToSend[1] = 0x04; // Data length   // changed from 04
                            bytesToSend[2] = 0x00; // Blank (for checksum)
                            bytesToSend[3] = 0x00; // Blank (for checksum)

                            // Convert z position to a byte array
                            var zBytes = BitConverter.GetBytes((float)currentZ);

                            // Insert z position
                            for (int i = 0; i < zBytes.Length; i++)
                            {
                                bytesToSend[i + 4] = zBytes[i];
                            }

                            // Send data
                            if (!HostToFirmware(bytesToSend, simCtl))
                            {
                                Console.WriteLine("Print failed - command failed to send.");
                                return;
                            }
                        }
                        else if (containsX || containsY)
                        {
                            throw new InvalidDataException("Invalid GCode command - command contains one X/Y command without the other.");
                        }

                        // Separately, if it contains an E
                        // Command number: 0x02
                        // Format: isLaserOn (1 byte)
                        if (containsE && (isLaserOn != oldIsLaserOn))
                        {
                            oldIsLaserOn = isLaserOn;
                            instructionCount++;
                            //continue;
                            var bytesToSend = new byte[8];
                            bytesToSend[0] = 0x02; // Command number
                            bytesToSend[1] = 0x01; // Data length
                            bytesToSend[2] = 0x00; // Blank (for checksum)
                            bytesToSend[3] = 0x00; // Blank (for checksum)

                            // Convert z position to a byte array
                            var isLaserOnBytes = BitConverter.GetBytes(isLaserOn);

                            // Insert isLaserOn
                            for (int i = 0; i < isLaserOnBytes.Length; i++)
                            {
                                bytesToSend[i + 4] = isLaserOnBytes[i];
                            }

                            // Send data
                            if (!HostToFirmware(bytesToSend, simCtl))
                            {
                                Console.WriteLine("Print failed - command failed to send.");
                                return;
                            }
                        }
                    }
                }
            }

            Console.WriteLine("Host: There were " + instructionCount.ToString() + " instructions sent by the host.");

            swTimer.Stop();
            long elapsedMS = swTimer.ElapsedMilliseconds;

            Console.WriteLine("Total Print Time: {0}", elapsedMS / 1000.0);
            Console.WriteLine("Press any key to continue");
            Console.ReadKey();
        }

        static byte[] Checksum(byte[] packet)
        {
            byte[] return_packet = packet.ToArray();
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

        /*
         * HostToFirmware
         * 
         * Returns: true on success, false on failure
         * Arguments:
         *   packet: Packet to be sent
         *   simCtl: PrinterControl variable that gets passed around to everything
         *   (optional) maxRetries: Maximum number of retries before faliure is returned
         *   (internal) currentRetry: Used internally to track the current retry count
         */

            // MAKE SURE TO CHANGE MAXRETI
        static bool HostToFirmware(byte[] packet, PrinterControl simCtl, int maxRetries = 10, int currentRetry = 0) // MAKE COPIES OF ALL THE THINGS
        {
            if (currentRetry >= maxRetries)
            {
                return false;
            }

            //Console.Write("+++++++++++++++NEXT COMMAND++++++++++++++++ \n");
            //        Host-to-Firmware Communication Procedure
            const int header_size = 4;    // 4 is header size
            int response_size = 1;  // So it reads one byte at a time
            int ACK_NAK_size = 1;   // size of both ACK and NAK bytes
            byte null_byte = 0x30;  // Null byte
            const int max_size = 20;
            byte[] ACK = { 0xA5 };  // ACK byte 
            byte[] NAK = { 0xFF };  // ACK byte
            string success = "SUCCESS";

            //      Send 4-byte header consisting of command byte, length, and 16-bit checksum
            byte[] checksummed_packet = Checksum(packet);
            byte[] header = checksummed_packet.Skip(0).Take(header_size).ToArray();   // array substring from Skip and Take, 0 to 4
            var header_copy = header.ToArray();   // making a copy for header to go in so it doesn't change it
            printByteArray(header_copy, "Host sending header");
            int header_bytes_sent = simCtl.WriteSerialToFirmware(header_copy, header_size);

            //      Read header bytes back from firmware to verify correct receipt of command header
            byte[] possible_header = new byte[header_size];
            // int header_bytes_recieved = simCtl.ReadSerialFromFirmware(possible_header, header_size);
            
            while (simCtl.ReadSerialFromFirmware(possible_header, header_size) < header_size)    // function inside returns header_bytes_recieved
            {
                ;  // wait for four bytes to be recieved // 4 bytes
            }
            int test = 0;
            printByteArray(possible_header, "Host received header response");

            //      If header is correct
            if (header.SequenceEqual(possible_header))  // header == possible_header
            {
                //      Send ACK(0xA5) to firmware
                byte[] ACK_to_send = ACK.ToArray();
                printByteArray(ACK_to_send, "Host sending ack");
                int ACK_send = simCtl.WriteSerialToFirmware(ACK_to_send, ACK_NAK_size);    // 1 is the size of the ACK and NAK bytes

                //      Send rest of packet not including the 4-byte header
                byte[] rest_bytes_send = checksummed_packet.Skip(header_size).Take(checksummed_packet.Length - header_size).ToArray();  // array substring
                printByteArray(rest_bytes_send, "Host sending remaining bytes");
                int rest_bytes_sent = simCtl.WriteSerialToFirmware(rest_bytes_send, packet.Length - header_size);   // change last argument to parameter data length in the 4th byte

                //      Wait for first byte of response to be received
                byte[] response_byte = new byte[response_size];
                byte[] response_bytes = new byte[max_size];
                int num_received = 0;
                int i = 0;
                //int response_bytes_recieved = simCtl.ReadSerialFromFirmware(response_bytes, response_size);
                //      Continue reading rest of response until null byte (0) is received
                while (true)    // might get hung up here
                {
                    num_received = simCtl.ReadSerialFromFirmware(response_byte, 1);
                    if(num_received == 1)
                    {
                        i++;    // ++'s the number of bytes received
                        response_bytes[i] = response_byte[response_byte.Length - 1];    // fills the byte[] with the bytes starting at 1 for some reason
                        num_received = 0;   // resets num_received
                    }

                    if (response_bytes[i] == null_byte) // should I use .SequenceEqual()?
                    {
                        break;  // exit the wait loop
                    }
                    else if (i >= max_size)
                    {
                        Console.Write("Broke when trying to read response \n");
                        return false;
                    }

                }
                var new_response = response_bytes.Skip(1).Take(i - 1).ToArray();    // i - 1 to take off the null and skip 1 to get rid of the 0 in first
                string response_string = System.Text.Encoding.ASCII.GetString(new_response);    // converts from byte[] to string
                printByteArray(response_bytes, "Host received response string " + response_string);



                //      Verify that response string equals “SUCCESS” or “VERSION n.n” (If not, re-send entire command)
                if (response_string == success)    //  || response_bytes == "VERSION n.n"
                {
                    return true;
                }
                else
                {
                    Console.Write("retry NO SUCCESS  \n");
                    //      retry command
                    return HostToFirmware(packet, simCtl, maxRetries, currentRetry + 1);  // this retries the command and returns the result of that command
                }
            }
            //      else if header is not received correctly
            else
            {
                //      Send NAK(0xFF)
                byte[] NAK_to_send = NAK.ToArray();
                printByteArray(NAK_to_send, "Host sending nak :(");
                int NAK_send = simCtl.WriteSerialToFirmware(NAK_to_send, ACK_NAK_size);
                Console.Write("retry NAK \n");
                //      Retry command
                return HostToFirmware(packet, simCtl, maxRetries, currentRetry + 1);  // this retries the command and returns the result of that command
            }
        }

        static void printByteArray(byte[] bytesToPrint, string message)
        {
            return;
            Console.Write(message);
            Console.Write(" [");
            bool firstIter = true;
            foreach (var item in bytesToPrint)
            {
                if (firstIter)
                    firstIter = false;
                else
                    Console.Write(", ");
                Console.Write(item.ToString());
            }
            Console.WriteLine("] \n");
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
                //Console.Clear();
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