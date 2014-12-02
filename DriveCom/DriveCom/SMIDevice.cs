using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DriveCom
{
    class SMIDevice : PhisonDevice
    {
        public DeviceInfo info;

        public SMIDevice(char driveLetter)
            : base(driveLetter)
        {

        }

        public class DeviceInfo
        {
            public UInt16 VID;
            public UInt16 PID;
            public String VendorStr;
            public String ProductStr;
            public String Serial;
            public String VendorINQ;
            public String ProductINQ;
            public String bcdDevice;

            public String PartNumber;
            public uint FlashID;

            public void printInfo()
            {
                Console.WriteLine("VID: 0x{0:X}", VID);
                Console.WriteLine("PID: 0x{0:X}", PID);
                Console.WriteLine("Vendor String: {0}", VendorStr);
                Console.WriteLine("Product String: {0}", ProductStr);
                Console.WriteLine("Serial Number: {0}", Serial);
                Console.WriteLine("Vendor INQUIRY String: {0}", VendorINQ);
                Console.WriteLine("Product INQUIRY String: {0}", ProductINQ);
                Console.WriteLine("Version INQUIRY: {0}", bcdDevice);
                Console.WriteLine("Part Number: {0}", PartNumber);
                Console.WriteLine("Flash ID: 0x{0:X}", FlashID);
            }
        }

        public void getDeviceCID(byte param)
        {
            DeviceInfo ret = new DeviceInfo();

            //Custom SMI SCSI commands for handshaking and reading the CID from the controller
            //SCSI Opcode 0xf0 indicates a read type operation

            //The first two operations (0x04 and 0x2a) are required reads for any SMI operation return the status and basic info required by SMI's tools
            //Operations 2-5 return  binary information
            //Operations 6-7 return the CID information that we can write/parse.
            byte[][] infoSequence = new byte[8][];
            infoSequence[0] = new byte[] {0xf0, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00};
            infoSequence[1] = new byte[] {0xf0, 0x2a, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00};
            infoSequence[2] = new byte[] {0xf0, 0x06, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00};
            infoSequence[3] = new byte[] {0xf0, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00};
            infoSequence[4] = new byte[] {0xf0, 0x04, 0xf8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00};
            infoSequence[5] = new byte[] {0xf0, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00};
            infoSequence[6] = new byte[] {0xf0, 0x01, 0x40, 0x02, 0x00, 0x00, 0x00, 0x30, 0x00, 0x3e, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00};
            infoSequence[7] = new byte[] {0xf0, 0x01, 0x40, 0x02, 0x00, 0x01, 0x00, 0x30, 0x00, 0x32, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00};
            int[] sizeSequence = new int[] {
                512, 512, 512, 512, 1536, 1024, 2048, 2048
            };

            //"Magic parameter" that makes the CID read work.
            //The controller has some state variable that causes this value to change from time to time and is exchanged during the handshake phase
            //From my experience it's either 2 or 4 and can be guessed trivially
            infoSequence[6][3] = param;
            infoSequence[7][3] = param;

            byte[] data = new byte[]{};
            for(int i = 0; i < infoSequence.GetLength(0); i++)
            {
                data = SendCommand(infoSequence[i], sizeSequence[i]);

                //Need to send SCSI opcode 0x00 in between transfers
                if (!sendTestUnitReady())
                {
                    return;
                }


                //C# forbids many of the useful memory operations available in C so parsing the received binary data here is a little obfuscated.
                switch (i)
                {
                    case 1:
                        var controller = data.Skip(430).Take(508 - 430).ToArray();
                        var tmp = System.Text.UTF8Encoding.ASCII.GetString(controller);
                        var isp = data.Skip(400).Take(430 - 400).ToArray();
                        tmp = tmp.TrimEnd('\0') + '-' + System.Text.UTF8Encoding.ASCII.GetString(isp);
                        ret.PartNumber = tmp;
                        break;
                    case 7:
                        uint b0 = data[8];
                        uint b1 = data[9];
                        uint vid = b0 | (b1 << 8);
                        ret.VID = (UInt16)vid;

                        b0 = data[10];
                        b1 = data[11];
                        uint pid = b0 | (b1 << 8);
                        ret.PID = (UInt16)pid;

                        var vendStr = data.Skip(56).Take(116 - 56).ToArray();
                        ret.VendorStr = System.Text.UnicodeEncoding.Unicode.GetString(vendStr);

                        var prodStr = data.Skip(118).Take(178 - 118).ToArray();
                        ret.ProductStr = System.Text.UnicodeEncoding.Unicode.GetString(prodStr);

                        var vendor = data.Skip(306).Take(8).ToArray();
                        ret.VendorINQ = System.Text.UTF8Encoding.ASCII.GetString(vendor);

                        var product = data.Skip(314).Take(16).ToArray();
                        ret.ProductINQ = System.Text.UTF8Encoding.ASCII.GetString(product);

                        var serial = data.Skip(180).Take(32).ToArray();
                        ret.Serial = System.Text.UnicodeEncoding.Unicode.GetString(serial);

                        var rev = data.Skip(330).Take(4).ToArray();
                        ret.bcdDevice = System.Text.UTF8Encoding.ASCII.GetString(rev);
                        
                        b0 = data[0x408];
                        b1 = data[0x409];
                        uint flash = b1 | (b0 << 8);
                        flash <<= 16;
                        b0 = data[0x40A];
                        b1 = data[0x40B];
                        flash |= (b0 << 8);
                        flash |= b1;
                        ret.FlashID = flash;

                        break;
                    default:
                        break;
                }
            }

            info = ret;
        }

        private bool sendTestUnitReady()
        {
            try
            {
                SendCommand(new byte[6] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0);
            }
            catch (InvalidOperationException e)
            {
                Console.WriteLine("SCSI \"Test Unit Ready\" operation failed.");
                return false;
            }
            return true;
        }

        public void setDeviceCID(string cidFile)
        {
            //Need to query device status (0x04 and 0x2a) before any SMI operation
            //SCSI Opcode 0xf1 is SMI's general USB controller write operation.
            byte[][] infoSequence = new byte[3][];
            infoSequence[0] = new byte[] { 0xf0, 0x04, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 };
            infoSequence[1] = new byte[] { 0xf0, 0x2a, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 };
            infoSequence[2] = new byte[] { 0xf1, 0x41, 0x40, 0x00, 0x00, 0x00, 0x00, 0x55, 0x20, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x00 };
            int[] sizeSequence = new int[] {
                512, 512
            };

            //Use a hex editor to edit the desired strings in the CID.
            var cid = new FileStream(cidFile, FileMode.Open);
            var data = new byte[cid.Length];
            cid.Read(data, 0, data.Length);
            cid.Close();

            for(int i = 0; i < infoSequence.GetLength(0); i++)
            {
                switch (i)
                {
                    case 0:
                    case 1:
                        {
                            SendCommand(infoSequence[i], sizeSequence[i]);
                            break;
                        }
                    case 2:
                        {
                            SendCommand(infoSequence[i], data);
                            break;
                        }
                }
            }

            if (!sendTestUnitReady())
            {
                return;
            }
        }

    }
}
