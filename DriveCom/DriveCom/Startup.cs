using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DriveCom
{
    class Startup
    {
        private static SMIDevice _device = null;

        public enum ExitCode
        {
            Success = 0,
            Failure = 1
        }

        static void Main(string[] args)
        {
            try
            {
                Environment.ExitCode = (int)ExitCode.Success;

                string drive = string.Empty;

                foreach (var arg in args)
                {
                    var parts = arg.TrimStart(new char[] { '/' }).Split(new char[] { '=' },
                        StringSplitOptions.RemoveEmptyEntries);
                    switch (parts[0].ToLower())
                    {
                        case "drive":
                            {
                                drive = parts[1];
                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
                }

                if (!string.IsNullOrEmpty(drive))
                {
                    _OpenDrive(drive);
                }

                Console.WriteLine("Valid Commands:\nopen [drive]\nclose\nreadcid [magic byte] (Usually 2 or 4)\nwritecid [cid.bin]");

                bool exiting = false;
                while (!exiting)
                {
                    Console.Write(">");
                    var line = Console.ReadLine();
                    var @params = line.Split(new char[] { ' ' });

                    try
                    {
                        switch (@params[0].ToLower())
                        {
                            case "open":
                                {
                                    _OpenDrive(@params[1]);
                                    break;
                                }
                            case "close":
                                {
                                    _CloseDrive();
                                    break;
                                }
                            case "readcid":
                                {
                                    byte magic = 0x02;
                                    if (@params.Length == 2)
                                    {
                                        magic = Convert.ToByte(@params[1]);
                                    }
                                    _device.getDeviceCID(magic);
                                    _device.info.printInfo();
                                    break;
                                }
                            case "writecid":
                                {
                                    _device.setDeviceCID(@params[1]);
                                    break;
                                }
                            case "quit":
                            case "exit":
                                {
                                    exiting = true;
                                    break;
                                }
                            default:
                                Console.WriteLine("Invalid command: " + @params[0]);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("ERROR: " + ex.ToString());
                    }
                }

                Console.WriteLine("Done.");
            }

            catch (Exception ex)
            {
                Environment.ExitCode = (int)ExitCode.Failure;

                Console.WriteLine("FATAL: " + ex.ToString());
            }
            finally
            {
                if (_device != null)
                {
                    _device.Close();
                }
            }
        }

        private static void _OpenDrive(string drive)
        {
            _CloseDrive();

            _device = new SMIDevice(drive[0]);
            _device.Open();
        }

        private static void _CloseDrive()
        {
            if (_device != null)
            {
                _device.Close();
                _device = null;
            }
        }
    }
}
