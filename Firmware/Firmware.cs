using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Hardware;

namespace Firmware
{

    public class FirmwareController
    {
        PrinterControl printer;
        bool fDone = false;
        bool fInitialized = false;

        public FirmwareController(PrinterControl printer)
        {
            this.printer = printer;
        }

        // Handle incoming commands from the serial link
        void Process()
        {
            // Todo - receive incoming commands from the serial link and act on those commands by calling the low-level hardwarwe APIs, etc.
            while (!fDone)
            {
            }
        }

        public void Start()
        {
            fInitialized = true;

            Process(); // this is a blocking call
        }

        public void Stop()
        {
            fDone = true;
        }

        public void WaitForInit()
        {
            while (!fInitialized)
                Thread.Sleep(100);
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

        static string FirmwareToHost(byte[] packet, PrinterControl printer)
        {
            //Firmware-to-Host Communication Procedure
            int header_size = 4;    // 4 is header size
            int response_size = 0;  // UNSURE OF SIZE OF RESPONSE
            int ACK_NAK_size = 1;   // size of both ACK and NAK bytes
            byte[] ACK = { 0xA5 };  // ACK byte 
            byte[] NAK = { 0xFF };  // ACK byte
            string timeout = "TIMEOUT";
            string success = "SUCCESS";
            string checksum = "CHECKSUM";
            string nak = "NAK";

            //      Read 4-byte header from host
            byte[] header_recieved = { 0x00 };
            int header_bytes_recieved = printer.ReadSerialFromHost(header_recieved, header_size);

            //      Write 4-byte header back to host
            byte[] header_sent = header_recieved;
            int header_bytes_sent = printer.WriteSerialToHost(header_sent, header_size);

            //      Read ACK/NAK byte
            byte[] ACK_NAK = { 0x00 };
            int ACK_NAK_recived = printer.ReadSerialFromHost(ACK_NAK, ACK_NAK_size);

            //      If ACK received
            if (ACK_NAK == ACK)
            {
                //      Attempt to read number of parameter bytes indicated in command header
                byte[] rest_bytes = { 0x00 };
                int num_bytes_rest = (int)(header_recieved[1]); // header_recieved[1] is the number of bytes in the rest of the packet
                int rest_bytes_recieved = printer.ReadSerialFromHost(rest_bytes, num_bytes_rest);

                //      If insufficient bytes are received
                if (rest_bytes_recieved < num_bytes_rest)   // if number of bytes recieved is less than number of bytes sent
                {
                    //      return “TIMEOUT”
                    byte[] response_bytes = ResponseMaker(timeout);
                    int response_bytes_sent = printer.WriteSerialToHost(response_bytes, response_size);
                    return timeout;
                }
                else
                {
                    //      Validate checksum(Be sure NOT to include checksum values themselves)
                    byte[] combined = new byte[header_recieved.Length + rest_bytes.Length];
                    System.Buffer.BlockCopy(header_recieved, 0, combined, 0, header_recieved.Length);
                    System.Buffer.BlockCopy(rest_bytes, 0, combined, 0, rest_bytes.Length);

                    byte[] combined_checksum = Checksum(combined);   // compare bytes 2 and 3 from both the header and what is recieved by adding up both parts: header and rest
                    var combined_checksum_bytes = combined_checksum.Skip(2).Take(2).ToArray();
                    var header_checksum_bytes = header_recieved.Skip(2).Take(2).ToArray();

                    //      If checksum correct
                    if (combined_checksum_bytes.SequenceEqual(header_checksum_bytes))    // .SequenceEqual checks the actual value
                    {

                        //      Process      // TYLER's Section
                        ProcessCommand(packet);     // change from packet to actual packet that goes into the function

                        //      Return “SUCCESS” or “VERSION n.n”
                        byte[] response_bytes = ResponseMaker(success);
                        int response_bytes_sent = printer.WriteSerialToHost(response_bytes, response_size);
                        return success;
                    }
                    //      Else
                    else
                    {
                        //      Return “CHECKSUM”
                        byte[] response_bytes = ResponseMaker(checksum);
                        int response_bytes_sent = printer.WriteSerialToHost(response_bytes, response_size);
                        return checksum;
                    }
                }
            }
            //      Else if NAK received
            else if (ACK_NAK == NAK)
            {
                //      Ignore command – it will be resent
                return nak;
            }
            else
            {
                return "Unknown byte";  // should never get to this 
            }
        }

        static byte[] ResponseMaker(string response_string)
        {
            byte[] return_response = Encoding.ASCII.GetBytes(response_string);  // Make sure this encoding 
            return return_response;
        }

        static void ProcessCommand(byte[] packet)
        {
            //      Process      // TYLER's Section
            if (true)   // command = set laser
            {
                PrinterControl.SetLaser(true);
            }
            else if (command == movegalvos)
            {

            }
        }
    }
}
