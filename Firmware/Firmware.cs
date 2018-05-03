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

        float currentLocation;
        const int top = 100; // mm
        const int bottom = 0; //mm
        const int Acceleration = 4; // mm/s^2
        const int maxVelocity = 40; // mm/s
        bool firstCommand;
     


        public FirmwareController(PrinterControl printer)
        {
            this.printer = printer;
            currentLocation = -1; //setting it to an unknown state
            //moveTop(printer);
            firstCommand = true;
           
        }

        // Handle incoming commands from the serial link
        void Process()
        {
            // Todo - receive incoming commands from the serial link and act on those commands by calling the low-level hardwarwe APIs, etc.
            //while (!fDone)
            //{
            //}
            //int test = 0;
            int num_commands = 0;
            while (true)
            {
                
                string result = FirmwareToHost(printer);
                if (fDone)
                    break;
                num_commands++;
                //Console.Write(result + " " + num_commands + "\n");
            }
            //test++;
            //FirmwareToHost(printer);
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
        static void ClearBuffer(PrinterControl printer)
        {
            int response_size = 1;
            byte[] read_byte = new byte[response_size];
            int num_received = 0;
            while (true)    // might get hung up here
            {
                num_received = printer.ReadSerialFromHost(read_byte, 1);
                if (num_received == 1)
                {
                    num_received = 0;   // resets num_received
                }
                else
                {
                    return;
                }
            }
        }
        string FirmwareToHost(PrinterControl printer) // took out byte[] packet, // need to change the return type to the same as the host side
        {
            //Firmware-to-Host Communication Procedure
            int header_size = 4;    // 4 is header size
            int response_size = 1;  // UNSURE OF SIZE OF RESPONSE
            int ACK_NAK_size = 1;   // size of both ACK and NAK bytes
            byte[] ACK = { 0xA5 };  // ACK byte 
            byte[] NAK = { 0xFF };  // ACK byte
            string timeout = "TIMEOUT";
            string success = "SUCCESS";
            string checksum = "CHECKSUM";
            string nak = "NAK";

            //      Read 4-byte header from host
            byte[] header_recieved = new byte[header_size];

            // wait for bytes to be recieved
            while(printer.ReadSerialFromHost(header_recieved, header_size) < header_size)      // the function returns header_bytes_recieved
            {
                ; // wait
            }
            ClearBuffer(printer);  // This should clear the buffer  
            printByteArray(header_recieved, "Firmware received header");

            //      Write 4-byte header back to host
            byte[] header_sent = header_recieved.ToArray();
            printByteArray(header_sent, "Firmware sent header");
            int header_bytes_sent = printer.WriteSerialToHost(header_sent, header_size);

            //      Read ACK/NAK byte
            byte[] ACK_NAK = { 0x00 };  // change this to a one element array with nothing in it
            while(printer.ReadSerialFromHost(ACK_NAK, ACK_NAK_size) < 1)    // function inside returns ACK_NAK_recieved (int)
            {
                ; // wait
            }
            //ClearBuffer(printer);  // This should clear the buffer  
            printByteArray(ACK_NAK, "Firmware received ACK or NAK");
            //      If ACK received
            if (ACK_NAK.SequenceEqual(ACK))
            {
                //      Attempt to read number of parameter bytes indicated in command header
                int num_bytes_rest = (int)(header_recieved[1]); // header_recieved[1] is the number of bytes in the rest of the packet
                byte[] rest_bytes = new byte[num_bytes_rest];
                int rest_bytes_recieved = 0;
                // header_recieved[1] should be rest of packet size
                int test_count = 0;
                bool timeout_test = false;
                while ((rest_bytes_recieved = printer.ReadSerialFromHost(rest_bytes, num_bytes_rest)) < num_bytes_rest)    // function inside returns rest_bytes_recieved
                {
                    // wait
                    if (rest_bytes_recieved < num_bytes_rest && test_count >= 100)
                    {
                        timeout_test = true;
                        break;
                    }    
                }
                ClearBuffer(printer);  // This should clear the buffer  
                printByteArray(rest_bytes, "Firmware received data");
                //      If insufficient bytes are received
                if (timeout_test)   // if number of bytes recieved is less than number of bytes sent
                {
                    //      return “TIMEOUT”
                    byte[] response_bytes = ResponseMaker(timeout);
                    printByteArray(response_bytes, "Firmware sent response");
                    int response_bytes_sent = printer.WriteSerialToHost(response_bytes, response_size);
                    return timeout;
                }
                else
                {
                    //      Validate checksum(Be sure NOT to include checksum values themselves)
                    byte[] combined = new byte[header_recieved.Length + rest_bytes.Length]; // change header_revcieved.,length to header_size
                    System.Buffer.BlockCopy(header_recieved, 0, combined, 0, header_recieved.Length);
                    System.Buffer.BlockCopy(rest_bytes, 0, combined, 4, rest_bytes.Length);

                    byte[] combined_checksum = Checksum(combined);   // compare bytes 2 and 3 from both the header and what is recieved by adding up both parts: header and rest
                    var combined_checksum_bytes = combined_checksum.Skip(2).Take(2).ToArray();
                    var header_checksum_bytes = header_recieved.Skip(2).Take(2).ToArray();

                    //      If checksum correct
                    if (combined_checksum_bytes.SequenceEqual(header_checksum_bytes))    // .SequenceEqual checks the actual value
                    {
                        // Combined should be the packet without the checksum. make sure
                        //      Process      // TYLER's Section
                        ProcessCommand(combined, printer);     // change from packet to actual packet that goes into the function // how can this work without returning????

                        //      Return “SUCCESS” or “VERSION n.n”
                        byte[] response_bytes = ResponseMaker(success);
                        printByteArray(response_bytes, "Firmware sent Success or Version response");
                        int response_bytes_sent = printer.WriteSerialToHost(response_bytes, response_bytes.Length);
                        //ClearBuffer(printer);  // This should clear the buffer  
                        return success; // SUCCESS
                    }
                    //      Else
                    else
                    {
                        //      Return “CHECKSUM”
                        byte[] response_bytes = ResponseMaker(checksum);
                        printByteArray(response_bytes, "Firmware sent Checksum response");
                        int response_bytes_sent = printer.WriteSerialToHost(response_bytes, response_bytes.Length);
                        return checksum;    // CHECKSUM
                    }
                }
            }
            //      Else if NAK received
            else if (ACK_NAK.SequenceEqual(NAK))
            {
                //ClearBuffer(printer);
                //      Ignore command – it will be resent
                return nak;
            }
            else
            {   // BANDAID
                int max_size = 20;
                byte[] nak_byte = new byte[response_size];
                byte[] nak_bytes = new byte[max_size];
                int num_received = 0;
                int i = 0;
                while (true)    // might get hung up here
                {
                    num_received = printer.ReadSerialFromHost(nak_byte, 1);
                    if (num_received == 1)
                    {
                        i++;    // ++'s the number of bytes received
                        nak_bytes[i] = nak_byte[nak_byte.Length - 1];    // fills the byte[] with the bytes starting at 1 for some reason
                        num_received = 0;   // resets num_received
                    }
                    // NAK[0] because one byte long
                    if (nak_bytes[i] == NAK[0]) // should I use .SequenceEqual()?
                    {
                        break;  // exit the wait loop
                    }

                }
                ClearBuffer(printer);  // This should clear the buffer  
                return "Found the bloody NAK";  // should never get to this but it does
            }
        }

        static byte[] ResponseMaker(string response_string)
        {
            byte[] return_response = Encoding.ASCII.GetBytes(response_string);  // Make sure this encoding works
            // to add the null_byte to the end
            byte null_byte = 0x30;
            byte[] new_response = new byte[return_response.Length + 1];
            return_response.CopyTo(new_response, 0);
            new_response[new_response.Length - 1] = null_byte;
            return new_response;
        }

        void ProcessCommand(byte[] packet, PrinterControl printer)
        {
            //      Process      // TYLER's Section, Recieves byte array . 
            /*  Byte 0:	  Command byte
	            Byte 1:   Length of parameter data (# of bytes)
	            Byte 2:	  Low-byte of 16-bit checksum
	            Byte 3:   High-byte of 16-bit checksum
	            Byte 4-n: Parameter data wait ms, stepper, move galvos, remove model, set laser
            
             Note: what we need to know is which bytes corespond to controling which of the below commands.
             */
            
            if (firstCommand)
            {
                printer.ResetStepper();
                moveTop(printer);
                firstCommand = false;
                
            }

            printByteArray(packet, "Firmware received successfully. Now in process command.");
            byte command = packet[0];

            byte MoveGalvos_command = 0x00;
            byte MoveZ_command = 0x01;
            byte SetLaser_command = 0x02;
            byte PrintDone_command = 0x03;
            //byte WaitMicroseconds_command = 0x00;
            //byte ResetStepper_command = 0x00;
            //byte RemoveModelFromPrinter_command = 0x00;

            //if (command == WaitMicroseconds_command) //WaitMicroseconds
            //{
            //    // convert from byte to long
            //    long microsec = 0;
            //    printer.WaitMicroseconds(microsec);
            //}

            //if (command == ResetStepper_command)    //ResetStepper
            //{
            //    // void function
            //    printer.ResetStepper();
            //}

            if (command == MoveGalvos_command)      //MoveGalvos
            {
                // convert from byte to float x and float y 
                byte[] x_substring = new byte[4];   // should I make this 4?
                Array.Copy(packet, 4, x_substring, 0, 4);
                byte[] y_substring = new byte[4];   // should I make this 4?
                Array.Copy(packet, 8, y_substring, 0, 4);

                float x = BitConverter.ToSingle(x_substring, 0);
                float y = BitConverter.ToSingle(y_substring, 0);
                float x_voltage = (float)(x * (2.5 / 100));  // find a better way to do these magic numbers
                float y_voltage = (float)(y * (2.5 / 100));
                printer.MoveGalvos(x_voltage, y_voltage);   // sends voltages to MoveGalvos();
            }

            //else if (command == RemoveModelFromPrinter_command) //RemoveModelFromPrinter
            //{
            //    // void function
            //    printer.RemoveModelFromPrinter();
            //}

            else if (command == SetLaser_command) //SetLaser
            {
                // convert from byte to bool
                bool set = BitConverter.ToBoolean(packet, 4);
                printer.SetLaser(set);
            }
            else if (command == MoveZ_command)
            {
                // convert from byte to float
                float z_frombottom = BitConverter.ToSingle(packet, 4);  // converting from byte[] starting at 4 to float
                //printer.ResetStepper();
                movementWithSpeed(printer, calculateDirection(printer, z_frombottom), CalculateDistance(printer, z_frombottom));
                // zrailcontroller
            }
            else if (command == PrintDone_command)
            {
                fDone = true;
                moveTop(printer);
            }
        } 
        static void printByteArray (byte[] bytesToPrint, string message)
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

        public void moveTop(PrinterControl printer)
        {
            movementWithSpeed(printer, PrinterControl.StepperDir.STEP_UP, top);
            currentLocation = 100;
        }

        public void moveBottom(PrinterControl printer)
        {
            movementWithSpeed(printer, PrinterControl.StepperDir.STEP_DOWN, bottom);
            currentLocation = 0;
        }

        public float CalculateDistance(PrinterControl printer, float Position)
        {
            var distance = Math.Abs(currentLocation - Position);
            return distance;
        }

        public PrinterControl.StepperDir calculateDirection(PrinterControl printer, float Position)
        {
            var distance = currentLocation - Position;
            if (distance > 0)
            {
                return PrinterControl.StepperDir.STEP_DOWN;
            }
            else
            {
                return PrinterControl.StepperDir.STEP_UP;
            }
        }

        public void movementWithSpeed(PrinterControl printer, PrinterControl.StepperDir dir, float distance)
        {
            float velocity = 0;
            float distanceTraveled = 0;
            while (currentLocation == -1 && !(printer.LimitSwitchPressed()))
            {
                
                velocity += Acceleration;
                //Thread.Sleep(1000);
                for (var i = 0; i < velocity; i++)
                {
                    for (var j = 0; j < 400; j++)
                    {
                        if (printer.LimitSwitchPressed())
                            break;
                        printer.StepStepper(dir);
                        distanceTraveled += (float)(1.0 / 400);
                        printer.WaitMicroseconds(625);


                    }
                    if (printer.LimitSwitchPressed())
                        break;
                }
                
                //printer.StepStepper(dir);
            }
                    if (((dir == PrinterControl.StepperDir.STEP_DOWN) && (currentLocation - distance > 0) && (!printer.LimitSwitchPressed()|| (currentLocation != -1))))
            {
                while (distanceTraveled < distance)
                {
                    
                    if (velocity < maxVelocity && (velocity + Acceleration < maxVelocity))
                        velocity += Acceleration;
                    else if (velocity < maxVelocity && (maxVelocity - velocity < Acceleration)) //allows us to reach max velocity without overshooting
                        velocity = maxVelocity;
                    //Thread.Sleep(1000); 
                    for (var i = 0; i < velocity; i++)
                    {
                        for (var j = 0; j < 400; j++)
                        {
                            if (distanceTraveled >= distance || (printer.LimitSwitchPressed() && (currentLocation == -1)))
                                break;
                            printer.StepStepper(dir);
                            //printer.WaitMicroseconds(5);
                            distanceTraveled += (float)(1.0 / 400);
                            //printer.ResetStepper();
                            printer.WaitMicroseconds(625);


                        }
                        if (distanceTraveled >= distance || printer.LimitSwitchPressed())
                            break;
                    }/*
                    if (distanceTraveled + velocity < distance) //if we wont over shoot
                    {
                        distanceTraveled += velocity;
                    }
                    else //this happens if we're close but the velocity will over shoot it should move the rest of the way without overshooting
                    {
                        //distanceTraveled += (velocity - distance);
                        velocity = 0;
                    }
                    */
                }
                currentLocation -= distanceTraveled;
            }
            if ((dir == PrinterControl.StepperDir.STEP_UP) && currentLocation + distance < 100 && !printer.LimitSwitchPressed())
            {
                while (distanceTraveled < distance)
                {
                    if (velocity < maxVelocity && (velocity + Acceleration < maxVelocity))
                        velocity += Acceleration;
                    else if (velocity < maxVelocity && (maxVelocity - velocity < Acceleration))
                        velocity = maxVelocity;
                   // Thread.Sleep(1000);
                    for (var i = 0; i < velocity; i++)
                    {
                        for (var j = 0; j < 400; j++)
                        {
                            if (distanceTraveled >= distance || printer.LimitSwitchPressed())
                                break;
                            printer.StepStepper(dir);
                            
                            distanceTraveled += (float)(1.0 / 400);
                            //printer.ResetStepper();
                            printer.WaitMicroseconds(625);

                        }
                        if (distanceTraveled >= distance || printer.LimitSwitchPressed())
                            break;
                    }
                    
                }
                currentLocation += distanceTraveled;
            }
        }

    /*public class zRailController
    {
        PrinterControl printer;
        float currentLocation;
        const int top = 100; // mm
        const int bottom = 0; //mm
        const int Acceleration = 4; // mm/s^2
        const int maxVelocity = 40; // mm/s

        public zRailController(ref PrinterControl printer)
        {
            this.printer = printer;
            currentLocation = -1; //setting it to an unknown state
            moveTop();
        }

        public void moveTop()
        {
            movementWithSpeed(PrinterControl.StepperDir.STEP_UP, top);
            currentLocation = 100;
        }

        public void moveBottom()
        {
            movementWithSpeed(PrinterControl.StepperDir.STEP_DOWN, bottom);
            currentLocation = 0;
        }

        public float CalculateDistance(float Position)
        {
            var distance = Math.Abs(currentLocation - Position);
            return distance;
        }

        public PrinterControl.StepperDir calculateDirection(float Position)
        {
            var distance = currentLocation - Position;
            if (distance > 0)
            {
                return PrinterControl.StepperDir.STEP_UP;
            }
            else
            {
                return PrinterControl.StepperDir.STEP_DOWN;
            }
        }

        public void movementWithSpeed(PrinterControl.StepperDir dir, float distance)
        {
            float velocity = 0;
            float distanceTraveled = 0;
            if(printer == null)
            {
                Console.WriteLine("I hate everythihng");

            }
            while (currentLocation == -1 && !(printer.LimitSwitchPressed()) )
            {
                velocity += Acceleration;
                Thread.Sleep(1000);
                for (var i = 0; i < velocity; i++)
                {
                    for (var j = 0; j < 400; j++)
                    {
                        printer.StepStepper(dir);
                        currentLocation += (float)0.0025;
                    }
                }
            }
            if ((dir == PrinterControl.StepperDir.STEP_DOWN) && (currentLocation - distance > 0))
            //TODO: if the distance is too much it will calculate the rest of the way to go to the bottom and just do that
            {
                while (distanceTraveled < distance)
                {
                    if (velocity < maxVelocity && (velocity + Acceleration < maxVelocity))
                        velocity += Acceleration;
                    else if (velocity < maxVelocity && (maxVelocity - velocity < Acceleration)) //allows us to reach max velocity without overshooting
                        velocity = maxVelocity;
                    Thread.Sleep(1000); //in a perfect world I'd time the next few loops and accamodate but i also dont feel like it
                    for (var i = 0; i < velocity; i++)
                    {
                        for (var j = 0; j < 400; j++)
                        {
                            printer.StepStepper(dir);
                        }
                    }
                    if (distanceTraveled + velocity < distance) //if we wont over shoot
                    {
                        distanceTraveled += velocity;
                    }
                    else //this happens if we're close but the velocity will over shoot it should move the rest of the way without overshooting
                    {
                        distanceTraveled += (distance - velocity);
                    }

                }
                currentLocation -= distance;
            }
            if ((dir == PrinterControl.StepperDir.STEP_UP) && currentLocation + distance < 100)
            {
                while (distanceTraveled < distance)
                {
                    if (velocity < maxVelocity && (velocity + Acceleration < maxVelocity))
                        velocity += Acceleration;
                    else if (velocity < maxVelocity && (maxVelocity - velocity < Acceleration))
                        velocity = maxVelocity;
                    Thread.Sleep(1000);
                    for (var i = 0; i < velocity; i++)
                    {
                        for (var j = 0; j < 400; j++)
                        {
                            printer.StepStepper(dir);
                        }
                    }
                    if (distanceTraveled + velocity < distance) //if we wont over shoot
                    {
                        distanceTraveled += velocity;
                    }
                    else //this happens if we're close but the velocity will over shoot it should move the rest of the way without overshooting
                    {
                        distanceTraveled += (distance - velocity);
                    }
                    currentLocation += distance;
                }
            }
        }

    }*/
}
}
