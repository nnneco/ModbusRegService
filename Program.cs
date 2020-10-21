using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Reflection;
using System.Configuration.Install;
using System.IO;
using System.Messaging;

namespace ModbusRegService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {            
            if (args != null && args.Length == 1 && args[0].Length > 1 
                && (args[0][0] == '-' || args[0][0] == '/'))
            {
                switch (args[0].Substring(1).ToLower())
                {
                    case "install":
                    case "i":
                        if (!ServiceInstallerUtility.Install())
                            Console.WriteLine("Failed to install service");
                        break;
                    case "uninstall":
                    case "u":
                        if (!ServiceInstallerUtility.Uninstall())
                            Console.WriteLine("Failed to uninstall service");
                        break;
                    default:
                        Console.WriteLine("Unrecognized parameters.");
                        Console.WriteLine("to install service type /install");
                        Console.WriteLine("to remove service type /uninstall");
                        break;
                }
            }
            else
            {
                try
                {
                    var service = new ModbusRegService();
                    var servicesToRun = new ServiceBase[] { service };

                    if (Environment.UserInteractive)
                    {
                        //Console.OutputEncoding = Encoding.GetEncoding(1251);

                        Console.CancelKeyPress += (x, y) => service.Stop();
                        //service.DoStart();
                        Console.WriteLine(service.GetVersion());
                        Console.WriteLine("Running service, press a key to stop");
                        Console.ReadKey();
                        //service.DoStop();
                        Console.WriteLine("Service stopped. Good-bye.");
                    }
                    else
                    {
                        ServiceBase.Run(servicesToRun);
                    }
                }
                catch (IOException ex)
                {
                    Console.WriteLine(ex.Message);
                }
                catch (MessageQueueException ex)
                {
                    Console.WriteLine("Queue operation failed, service cannot continue " + ex.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Could not start polling service due to unknown error" + ex.Message);
                }



                //try
                //{
                //    ServiceBase[] ServicesToRun;
                //    ServicesToRun = new ServiceBase[] { new ModbusRegService() };
                //    ServiceBase.Run(ServicesToRun);
                //}
                //catch (Exception ex)
                //{
                //    Console.WriteLine("Could not start polling service due to unknown error" + ex.Message);
                //}

            }
            Console.ReadLine();
        }
    }

    class ServiceInstallerUtility   
    {
        private static readonly string exePath = Assembly.GetExecutingAssembly().Location;

        public static bool Install()
        {
            try { ManagedInstallerClass.InstallHelper(new[] { exePath }); }
            catch { return false; }
            return true;
        }

        public static bool Uninstall()
        {
            try { ManagedInstallerClass.InstallHelper(new[] { "/u", exePath }); }
            catch { return false; }
            return true;
        }
    }

}
