using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;

using System.IO;
using System.IO.Ports;
using System.Xml.Serialization;
using System.Threading;
using System.Reflection;
using System.Data.SqlClient;

using Modbus.Data;
using Modbus.Device;
using Modbus.Utility;


namespace ModbusRegService
{

    public enum RegType { Coil, Input, HoldingReg, InputReg }; 
                        // HoldingReg == 4x  InputReg == 3x

    public struct SRegister
    {
        public byte DeviceAddress;
        public RegType RegisterType;
        public UInt16 RegisterAddress;
        public string DBRegisterAddress;
        public int ReadInterval;        
        public int ReadTimeout;
        public double LinearA;
        public double LinearB;
    }

    public struct SOptions
    {
        public string SerialPortName;
        public int SerialPortBaudRate;
        public int SerialPortDataBits;
        public Parity SerialPortParity;
        public StopBits SerialPortStopBits;

        //public int ModbusTimeout;

        public bool DebugEnable;

        public string DBConnectionString;
        public string DBTableName;
        public string DBValueFieldName;
        public string DBAddressFieldName;
        
        public SRegister[] Registers;
    }


    public partial class ModbusRegService : ServiceBase
    {
        private struct SValue
        {
            public bool Ready;
            public UInt16 Value;
        }

        private volatile SValue[] Values;
        public static string AppStartupPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\";
        public SOptions o = new SOptions();
        private SerialPort sp = new SerialPort();
        private static volatile bool CanRead = true;
        private static volatile bool DoWorkFlag = true;
        private IModbusSerialMaster MBRtu;
        private Random r = new Random();

        

        private readonly Thread workerThread;


        public ModbusRegService()
        {            
            InitializeComponent();
            workerThread = new Thread(DoWork);
        }

        protected override void OnStart(string[] args)
        {
            LoadSettings();
            WriteLog(GetVersion());

            sp.PortName = o.SerialPortName;
            sp.BaudRate = o.SerialPortBaudRate;
            sp.DataBits = o.SerialPortDataBits;
            sp.Parity = o.SerialPortParity;
            sp.StopBits = o.SerialPortStopBits;
            try
            {
                sp.Open();
            }
            catch
            {
                WriteLog("Unable to open serial port " + sp.PortName);
                WriteLog("Service not started.");
                WriteLog("Exitting...");
                return;
            }

            Values = new SValue[o.Registers.Length];
            for (int i = 0; i < Values.Length; ++i)
            {
                Values[i].Ready = false;
                Values[i].Value = 0;
            }

            MBRtu = ModbusSerialMaster.CreateRtu(sp);

            workerThread.Start();
            WriteLog("Service started.");
        }

        protected override void OnStop()
        {
            if (workerThread.IsAlive)
            {
                WriteLog("Aborting thread...");
                workerThread.Abort();
            }

            MBRtu.Dispose();

            WriteLog("Service stopped.");
        }

        private void DoWork()
        {            
            double v=0.0;
            MyTimer[] tm = new MyTimer[o.Registers.Length];
            for (int i = 0; i < tm.Length; ++i)
            {
                tm[i] = new MyTimer(i, o.Registers[i].ReadInterval * 1000);                
                tm[i].Elapsed += new System.Timers.ElapsedEventHandler(TimersTick);
                tm[i].Enabled = true;
            }

            while (DoWorkFlag)
            {
                for (int i = 0; i < Values.Length; ++i)
                {
                    if(Values[i].Ready)
                    {
                        v = Convert.ToDouble(Values[i].Value) * o.Registers[i].LinearA + o.Registers[i].LinearB;
                        WriteLog("Addr: "+o.Registers[i].RegisterAddress+ " = " + v.ToString("F2"));
                        Values[i].Ready = false;
                        SqlInsert(o.Registers[i].DBRegisterAddress, v, o.DBConnectionString, o.DBTableName, o.DBValueFieldName, o.DBAddressFieldName);
                    }
                }

                System.Threading.Thread.Sleep(3000);
            }

        }

        private void TimersTick(object source, System.Timers.ElapsedEventArgs e)
        {
            MyTimer t = source as MyTimer;
            t.Enabled = false;            
            while (!CanRead) System.Threading.Thread.Sleep(r.Next(10,100)); //w8ing 4 other instances ends...            
            try
            {
                Values[t.Index].Value = GetModbusData(o.Registers[t.Index]);
                Values[t.Index].Ready = true;
            }
            catch(Exception ex)
            {
                //WriteLog("Error! " + ex.Message);
            }

            t.Enabled = true;
        }

        private UInt16 GetModbusData(SRegister reg)
        {
            UInt16 val = 0;
            if (sp != null && (!sp.IsOpen))
            {                
                try
                {
                    sp.Open();
                }
                catch
                {
                }
                if (sp.IsOpen) WriteLog("!Warning: Port " + sp.PortName + " has been reopened.");
            }

            if (sp != null && sp.IsOpen)
            {
                try
                {
                    CanRead = false;
                    MBRtu.Transport.ReadTimeout = reg.ReadTimeout;
                    UInt16[] buf = new UInt16[1];
                    string s = "";

                    switch (reg.RegisterType)
                    {
                        case RegType.Coil:
                            {
                                WriteLog("Sorry. Not implemented yet");
                                break;
                            }

                        case RegType.Input:
                            {
                                WriteLog("Sorry. Not implemented yet");
                                break;
                            }

                        case RegType.HoldingReg:
                            {
                                buf = MBRtu.ReadHoldingRegisters(reg.DeviceAddress, reg.RegisterAddress, 1);

                                break;
                            }

                        case RegType.InputReg:
                            {
                                buf = MBRtu.ReadInputRegisters(reg.DeviceAddress, reg.RegisterAddress, 1);

                                break;
                            }
                    }
                    val = buf[0];
                    //s += reg.RegisterType.ToString() + " " + reg.RegisterAddress.ToString() + " " + buf[0].ToString() + "\t";
                    //WriteLog(s);

                }
                catch (Exception ex)
                {
                    WriteLog("!Error modbus: " + ex.Message);
                }
            }
            else
            {
                CanRead = true;
                throw new Exception("Serial port " + sp.PortName + " is closed.");
            }

            CanRead = true;
            return val;
        }


        private void LoadSettings()
        {
            try
            {
                FEServerConfig.LoadSettings(ref o);
            }
            catch
            {
                //Default settings here

                o.SerialPortName = "COM1";
                o.SerialPortBaudRate = 9600;
                o.SerialPortDataBits = 8;
                o.SerialPortParity = Parity.None;
                o.SerialPortStopBits = StopBits.One;        
                o.DebugEnable = true;
                o.DBConnectionString="";
                o.DBTableName="";
                o.DBValueFieldName="";
                o.DBAddressFieldName="";
                o.Registers = new SRegister[1];
                    o.Registers[0].DBRegisterAddress = "2900000000000000";
                    o.Registers[0].DeviceAddress = 0;
                    o.Registers[0].ReadInterval = 60;
                    o.Registers[0].ReadTimeout = 200;
                    o.Registers[0].RegisterAddress = 0;
                    o.Registers[0].RegisterType = RegType.InputReg;
                    o.Registers[0].LinearA = 1.0;
                    o.Registers[0].LinearB = 0.0;

                FEServerConfig.CreateSettings(o);
                WriteLog("Default Settings Created");
            }
        }


        private bool SqlInsert(string addr, double v, string DBConnctStr, string DBTableName, string DBValueField, string DBAddrField)
        {
            SqlCommand cmd;
            SqlConnection conn = new SqlConnection(DBConnctStr);
            string str = "Insert into " + DBTableName + " (" + DBAddrField + "," + DBValueField + ") Values('" + addr + "', " + v.ToString().Replace(',', '.') + ")";
            cmd = new SqlCommand(str, conn);
            try
            {
                conn.Open();
                cmd.ExecuteNonQuery();
                conn.Close();
                conn.Dispose();
            }
            catch (Exception ex)
            {
                WriteLog("Failure: " + ex.Message);
                return false;
            }

            return true;
        }

        private void WriteLog(string s)
        {
            if (o.DebugEnable)
            {
                try
                {
                    StreamWriter wr = new StreamWriter(AppStartupPath + "debug.log", true);
                    wr.WriteLine(DateTime.Now.ToString() + "\t" + s);
                    wr.Close();
                    wr.Dispose();
                }
                catch
                {
                }
            }
        }

        public string GetVersion()
        {
            Version version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            DateTime buildDateTime = new DateTime(2000, 1, 1).Add(new TimeSpan(
            TimeSpan.TicksPerDay * version.Build + // days since 1 January 2000
            TimeSpan.TicksPerSecond * 2 * version.Revision));

            string s = "\n\nСервис сбора данных Modbus. \nВерсия: " + version.ToString() + "\nБилд: " + version.Build.ToString() + "\nРевизия: " + version.Revision.ToString() + "\nДата: " + buildDateTime.ToString();

            return s;
        }

    }

    public class MyTimer : System.Timers.Timer
    {
        private int ind;
        public MyTimer(int index, int interval)
        {
            this.ind = index;
            this.Interval = interval;
        }
        public int Index { get { return ind; } }
    }

    public class FEServerConfig
    {
        //Лишаем возможности создавать объекты этого класса
        private FEServerConfig() { }
        public static void CreateSettings(object o)
        {
            XmlSerializer myXmlSer = new XmlSerializer(o.GetType());
            StreamWriter myWriter = new StreamWriter(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\settings.conf");
            myXmlSer.Serialize(myWriter, o);
            myWriter.Close();
        }

        public static void LoadSettings(ref SOptions o)
        {
            XmlSerializer myXmlSer = new XmlSerializer(typeof(SOptions));
            FileStream mySet = new FileStream(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\settings.conf", FileMode.Open);
            o = (SOptions)myXmlSer.Deserialize(mySet);
            mySet.Close();
        }


    }

}
