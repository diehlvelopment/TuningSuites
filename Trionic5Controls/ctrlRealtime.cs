/*
 * This control should be fed with information from the main ECUConnection control in the application
 * Changes and user interaction should be done through delegates to prevent illegal inter-thread calls
 * */

// if we are auto tuning (_autoTuning = true) we have to wait for certain criteria before we can step in.
// criteria are: stable in cell for x ms

// digitalDisplayControl59 -> Ignition trim
// digitalDisplayControl60 -> Ignition counter
// digitalDisplayControl62 -> Ignition adapt
// digitalDisplayControl63 -> Ignition knock offset
// Option to clear knock count map and knock_count_cylX

//TODO: ctrlRealtime implement the free logging options properly
// - remember previously selected symbols after shutdown (save as xml?)
// - update these remembered symbols with the correct sram values?
// - Let the user decide which symbol have a gauge attached and remember which gauge is what (including decimals etc)



using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using System.IO;
using Trionic5Tools;


namespace Trionic5Controls
{
    public enum RealtimeMonitoringType : int
    {
        Fuel,
        Ignition,
        Boost,
        Knock,
        Dashboard,
        Settings,
        Userdefined,
        GraphDashboard,
        OnlineGraph,
        EngineStatus,
        UserMaps,
        AutotuneIgnition,
        AutotuneFuel
    }

    public partial class ctrlRealtime : DevExpress.XtraEditors.XtraUserControl
    {

        public delegate void LoggingStarted(object sender, LoggingEventArgs e);
        public event ctrlRealtime.LoggingStarted onLoggingStarted;

        public delegate void ProgramModeChange(object sender, ProgramModeEventArgs e);
        public event ctrlRealtime.ProgramModeChange onProgramModeChange;


        public delegate void MonitorTypeChanged(object sender, RealtimeMonitoringEventArgs e);
        public event ctrlRealtime.MonitorTypeChanged onMonitorTypeChanged;

        public delegate void MapDisplayRequested(object sender, MapDisplayRequestEventArgs e);
        public event ctrlRealtime.MapDisplayRequested onMapDisplayRequested;

        public delegate void AddSymbolToMonitorList(object sender, MapDisplayRequestEventArgs e);
        public event ctrlRealtime.AddSymbolToMonitorList onAddSymbolToMonitorList;
        public event ctrlRealtime.AddSymbolToMonitorList onRemoveSymbolFromMonitorList;

        public delegate void OpenLogFileRequest(object sender, OpenLogFileRequestEventArgs e);
        public event ctrlRealtime.OpenLogFileRequest onOpenLogFileRequest;

        public delegate void AutoTuneStateChanged(object sender, AutoTuneEventArgs e);
        public event ctrlRealtime.AutoTuneStateChanged onAutoTuneStateChanged;


        public delegate void SwitchClosedLoopOnOff(object sender, ClosedLoopOnOffEventArgs e);
        public event ctrlRealtime.SwitchClosedLoopOnOff onSwitchClosedLoopOnOff;

        public delegate void SwitchIgnitionTuningOnOff(object sender, ClosedLoopOnOffEventArgs e);
        public event ctrlRealtime.SwitchIgnitionTuningOnOff onSwitchIgnitionTuningOnOff;

        private bool _initiallyShowIgnitionMap = true;
        private bool _initiallyShowFuelMap = true;
        private bool _initiallyShowAFRMap = true;

        private System.Media.SoundPlayer sndplayer;

        private RealtimeMonitoringType type = RealtimeMonitoringType.Fuel;
        private MapSensorType m_MapSensor = MapSensorType.MapSensor25;
        private double multiply = 1;
        public MapSensorType MapSensor
        {
            get { return m_MapSensor; }
            set
            {
                m_MapSensor = value;

                switch (m_MapSensor)
                {
                    case MapSensorType.MapSensor30:
                        multiply = 1.2;
                        break;
                    case MapSensorType.MapSensor35:
                        multiply = 1.4;
                        break;
                    case MapSensorType.MapSensor40:
                        multiply = 1.6;
                        break;
                    case MapSensorType.MapSensor50:
                        multiply = 2.0;
                        break;
                }
            }
        }

        private int[] m_ignitionxaxis;

        public int[] Ignitionxaxis
        {
            get { return m_ignitionxaxis; }
            set { m_ignitionxaxis = value; }
        }
        private int[] m_ignitionyaxis;

        public int[] Ignitionyaxis
        {
            get { return m_ignitionyaxis; }
            set { m_ignitionyaxis = value; }
        }

        private int[] m_fuelxaxis;

        public int[] Fuelxaxis
        {
            get { return m_fuelxaxis; }
            set { m_fuelxaxis = value; }
        }

        private int[] m_fuelyaxis;

        public int[] Fuelyaxis
        {
            get { return m_fuelyaxis; }
            set { m_fuelyaxis = value; }
        }

        private string m_autoLogTriggerStartSymbol = string.Empty;

        public string AutoLogTriggerStartSymbol
        {
            get { return m_autoLogTriggerStartSymbol; }
            set { m_autoLogTriggerStartSymbol = value; }
        }
        private string m_autoLogTriggerStopSymbol = string.Empty;

        public string AutoLogTriggerStopSymbol
        {
            get { return m_autoLogTriggerStopSymbol; }
            set { m_autoLogTriggerStopSymbol = value; }
        }

        private int m_autoLogStartSign = 0;

        public int AutoLogStartSign
        {
            get { return m_autoLogStartSign; }
            set { m_autoLogStartSign = value; }
        }
        private int m_autoLogStopSign = 0;

        public int AutoLogStopSign
        {
            get { return m_autoLogStopSign; }
            set { m_autoLogStopSign = value; }
        }

        private double m_autoLogStartValue = 0;

        public double AutoLogStartValue
        {
            get { return m_autoLogStartValue; }
            set { m_autoLogStartValue = value; }
        }
        private double m_autoLogStopValue = 0;

        public double AutoLogStopValue
        {
            get { return m_autoLogStopValue; }
            set { m_autoLogStopValue = value; }
        }

        private bool m_autoLoggingEnabled = false;

        public bool AutoLoggingEnabled
        {
            get { return m_autoLoggingEnabled; }
            set { m_autoLoggingEnabled = value; }
        }


        private SymbolCollection m_RealtimeSymbolCollection;

        public SymbolCollection RealtimeSymbolCollection
        {
            get { return m_RealtimeSymbolCollection; }
            set { m_RealtimeSymbolCollection = value; }
        }

        private string _wideBandAFRSymbol = string.Empty;

        private int _AcceptableTargetErrorPercentage;

        public int AcceptableTargetErrorPercentage
        {
            get { return _AcceptableTargetErrorPercentage; }
            set { _AcceptableTargetErrorPercentage = value; }
        }
        private int _AreaCorrectionPercentage;

        public int AreaCorrectionPercentage
        {
            get { return _AreaCorrectionPercentage; }
            set { _AreaCorrectionPercentage = value; }
        }
        private bool _AutoUpdateFuelMap;

        public bool AutoUpdateFuelMap
        {
            get { return _AutoUpdateFuelMap; }
            set { _AutoUpdateFuelMap = value; }
        }
        private int _CellStableTime_ms;

        public int CellStableTime_ms
        {
            get { return _CellStableTime_ms; }
            set { _CellStableTime_ms = value; }
        }
        private int _CorrectionPercentage;

        public int CorrectionPercentage
        {
            get { return _CorrectionPercentage; }
            set { _CorrectionPercentage = value; }
        }
        private bool _DiscardClosedThrottleMeasurements;

        public bool DiscardClosedThrottleMeasurements
        {
            get { return _DiscardClosedThrottleMeasurements; }
            set { _DiscardClosedThrottleMeasurements = value; }
        }
        private bool _DiscardFuelcutMeasurements;

        public bool DiscardFuelcutMeasurements
        {
            get { return _DiscardFuelcutMeasurements; }
            set { _DiscardFuelcutMeasurements = value; }
        }
        private int _EnrichmentFilter;

        public int EnrichmentFilter
        {
            get { return _EnrichmentFilter; }
            set { _EnrichmentFilter = value; }
        }
        private int _FuelCutDecayTime_ms;

        public int FuelCutDecayTime_ms
        {
            get { return _FuelCutDecayTime_ms; }
            set { _FuelCutDecayTime_ms = value; }
        }
        private int _MaximumAdjustmentPerCyclePercentage;

        public int MaximumAdjustmentPerCyclePercentage
        {
            get { return _MaximumAdjustmentPerCyclePercentage; }
            set { _MaximumAdjustmentPerCyclePercentage = value; }
        }
        private int _MaximumAFRDeviance;

        public int MaximumAFRDeviance
        {
            get { return _MaximumAFRDeviance; }
            set { _MaximumAFRDeviance = value; }
        }
        private int _MinimumAFRMeasurements;

        public int MinimumAFRMeasurements
        {
            get { return _MinimumAFRMeasurements; }
            set { _MinimumAFRMeasurements = value; }
        }

        public string WideBandAFRSymbol
        {
            get { return _wideBandAFRSymbol; }
            set { _wideBandAFRSymbol = value; }
        }

        private bool m_loggingActive = false;
        private string m_logfileName = string.Empty;

        private string m_currentfile = string.Empty;

        private float m_injectorCC = 875F;

        public float InjectorCC
        {
            get { return m_injectorCC; }
            set { m_injectorCC = value; }
        }

        public string Currentfile
        {
            get { return m_currentfile; }
            set { m_currentfile = value; }
        }

        private int[] m_ignitionmapMutations;

        private int[] m_fuelmapMutations;
        private int[] m_afrmapCounter;
        private float[] m_afrtargetmap;

        private float[] m_AFRMapInMemory;

        private bool m_WriteLogMarker = false;



        public ctrlRealtime()
        {
            InitializeComponent();
            LoadExtraMeasurements();
            SymbolCollection sc = new SymbolCollection();
            SymbolHelper sh_fuel = new SymbolHelper();
            sh_fuel.Varname = "Insp_mat!";
            sh_fuel.Helptext = "Main fuel map";
            sc.Add(sh_fuel);
            SymbolHelper sh_korr = new SymbolHelper();
            //<GS-29112010> different for T5.2


            sh_korr.Varname = "Adapt_korr!";
            sh_korr.Helptext = "Fuel adaption";
            sc.Add(sh_korr);

            SymbolHelper sh_fuel_mut = new SymbolHelper();
            sh_fuel_mut.Varname = "FuelAdjustmentMap";
            sh_fuel_mut.Helptext = "Autotune fuel";
            sc.Add(sh_fuel_mut);
            SymbolHelper sh_knock = new SymbolHelper();
            sh_knock.Varname = "Fuel_knock_mat!";
            sh_knock.Helptext = "Knock fuel map";
            sc.Add(sh_knock);
            SymbolHelper sh_injconst = new SymbolHelper();
            sh_injconst.Varname = "Inj_konst!";
            sh_injconst.Helptext = "Injector constant";
            sc.Add(sh_injconst);
            btnMapEdit.Relevantsymbols = sc;
            try
            {
                sndplayer = new System.Media.SoundPlayer();
            }
            catch (Exception E)
            {
                Console.WriteLine(E.Message);
            }
        }

        public void SwitchOffAutoTune()
        {
            if (_autoTuning)
            {
                //btnAutoTune.Text = "Autotune";
                StopAutoTune();
            }
        }


        public void SwitchOffAutoTuneIgnition()
        {
            if (_autoTuningIgnition)
            {
                StopAutoTuneIgnition();
            }
        }

        private void xtraTabPage3_Paint(object sender, PaintEventArgs e)
        {

        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            if (!m_loggingActive)
            {
                m_loggingActive = true;
                m_logfileName = DetermineNewLogFileName();
                simpleButton1.Text = "Stop session";
            }
            else
            {
                m_loggingActive = false;
                simpleButton1.Text = "Start session";
                //Optioneel de laatste log starten
                CastOpenLogFileEvent(m_logfileName);
            }
        }


        private string DetermineNewLogFileName()
        {
            // <GS-27072010> depends on settings!
            // Option 1: One big log or one per tabpage
            // Option 2: One log per day or one per started session
            string retval = string.Empty;
            string additionalInfo = type.ToString() + "-";
            if (m_appSettings.OneLogForAllTypes) additionalInfo = "";
            string dt_format = "yyyyMMddHHmmss";
            if (m_appSettings.OneLogPerTypePerDay) dt_format = "yyyyMMdd";
            if (m_currentfile != string.Empty)
            {
                if (!Directory.Exists(Path.GetDirectoryName(m_currentfile) + "\\Logs")) Directory.CreateDirectory(Path.GetDirectoryName(m_currentfile) + "\\Logs");

                if (File.Exists(m_currentfile))
                {
                    retval = Path.GetDirectoryName(m_currentfile) + "\\Logs\\" + Path.GetFileNameWithoutExtension(m_currentfile) + "-" + additionalInfo + DateTime.Now.ToString(dt_format) + ".t5l";
                }
            }
            else
            {
                retval = Application.StartupPath + "\\Realtimelog-" + additionalInfo + DateTime.Now.ToString(dt_format) + ".t5l";
            }
            if (retval != string.Empty)
            {
                // notify main application <GS-18032010>
                if (onLoggingStarted != null)
                {
                    onLoggingStarted(this, new LoggingEventArgs(retval));
                }

            }
            return retval;

        }
        private RealtimeValue _lastthermovalue = new RealtimeValue();
        private RealtimeValue _lastInjectorDC = new RealtimeValue();
        private RealtimeValue _lastRPM = new RealtimeValue();
        private RealtimeValue _lastCoolantTemperature = new RealtimeValue();
        private RealtimeValue _lastAirTemperature = new RealtimeValue();
        private RealtimeValue _lastAFR = new RealtimeValue();
        private RealtimeValue _lastInletPressure = new RealtimeValue();
        private RealtimeValue _lastInjectorDuration = new RealtimeValue();
        private RealtimeValue _lastIgnitionAngle = new RealtimeValue();
        private RealtimeValue _lastMaxTryck = new RealtimeValue();
        private RealtimeValue _lastReglTryck = new RealtimeValue();
        private RealtimeValue _lastTPS = new RealtimeValue();
        private RealtimeValue _lastBoostError = new RealtimeValue();
        private RealtimeValue _lastAPCDecrease = new RealtimeValue();
        private RealtimeValue _lastPFactor = new RealtimeValue();
        private RealtimeValue _lastIFactor = new RealtimeValue();
        private RealtimeValue _lastDFactor = new RealtimeValue();
        private RealtimeValue _lastPWM = new RealtimeValue();
        private RealtimeValue _lastKnockCountCylinder1 = new RealtimeValue();
        private RealtimeValue _lastKnockCountCylinder2 = new RealtimeValue();
        private RealtimeValue _lastKnockCountCylinder3 = new RealtimeValue();
        private RealtimeValue _lastKnockCountCylinder4 = new RealtimeValue();
        private RealtimeValue _lastKnockAverage = new RealtimeValue();
        private RealtimeValue _lastVehicleSpeed = new RealtimeValue();
        private RealtimeValue _lastTQ = new RealtimeValue();
        private RealtimeValue _lastPower = new RealtimeValue();
        private RealtimeValue _maxTQ = new RealtimeValue();
        private RealtimeValue _maxTQRPM = new RealtimeValue();
        private RealtimeValue _maxPower = new RealtimeValue();
        private RealtimeValue _maxPowerRPM = new RealtimeValue();
        float _last_Lacc_mangd = -1;
        float _last_Acc_mangd = -1;
        float _last_Lret_mangd = -1;
        float _last_Ret_mangd = -1;

        int _last_Lacc_mangd_cyl1 = 0;
        int _last_Lacc_mangd_cyl2 = 0;
        int _last_Lacc_mangd_cyl3 = 0;
        int _last_Lacc_mangd_cyl4 = 0;
        int _last_Acc_mangd_cyl1 = 0;
        int _last_Acc_mangd_cyl2 = 0;
        int _last_Acc_mangd_cyl3 = 0;
        int _last_Acc_mangd_cyl4 = 0;

        int _last_Lret_mangd_cyl1 = 0;
        int _last_Lret_mangd_cyl2 = 0;
        int _last_Lret_mangd_cyl3 = 0;
        int _last_Lret_mangd_cyl4 = 0;
        int _last_Ret_mangd_cyl1 = 0;
        int _last_Ret_mangd_cyl2 = 0;
        int _last_Ret_mangd_cyl3 = 0;
        int _last_Ret_mangd_cyl4 = 0;

        int prev_knock_cyl1 = -1;
        int prev_knock_cyl2 = -1;
        int prev_knock_cyl3 = -1;
        int prev_knock_cyl4 = -1;

        float max_knock_offset1 = -1;
        float max_knock_offset2 = -1;
        float max_knock_offset3 = -1;
        float max_knock_offset4 = -1;


     //  private float _lastThermoValue = 0;
        private float _lastADC1Value = 0;
        private float _lastADC2Value = 0;
        private float _lastADC3Value = 0;
        private float _lastADC4Value = 0;
        private float _lastADC5Value = 0;


        private float CalculateInjectorDC(float rpm, float injectorduration)
        {
            return (float)((rpm * injectorduration) / 1200);
        }

        private Color _previousPanelColor = Color.FromArgb(232, 232, 232);

        /// <summary>
        /// Sets a value measured by the canbus interface
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="value"></param>
        public void SetValue(string symbol, double dvalue)
        {
            // for test only
            /*Int32 sec = Convert.ToByte(DateTime.Now.Second);
            Int32 ivalue = sec << 8;
            ivalue += sec;
            ivalue = ivalue << 8;
            ivalue += sec;
            ivalue = ivalue << 8;
            ivalue += sec;*/

            // for test only

            float value = (float)dvalue;
            switch (symbol)
            {
                case "Insptid_ms10":
                    // injector DC bijwerken
                    _lastInjectorDuration.UpdateValue(DateTime.Now, value);
                    _lastInjectorDC.UpdateValue(DateTime.Now, CalculateInjectorDC(_lastRPM.Value, _lastInjectorDuration.Value));
                    linearGauge1.Value = _lastInjectorDC.Value;
                    if (type == RealtimeMonitoringType.OnlineGraph)
                    {
                        onlineGraphControl1.AddMeasurement("Inj.dur", symbol, DateTime.Now, value, 0, 30, Color.AliceBlue);
                    }
                    break;
                case "Knock_offset1234":
                    if (type == RealtimeMonitoringType.AutotuneIgnition)
                    {
                        // better support for knock indicator
                        UInt64 klval = Convert.ToUInt64(dvalue);
                        if (klval > 0)
                        {
                            if (!ledToggler3.Checked)
                            {
                                ledToggler3.Checked = true;
                                // set panel background to indicate knock!
                                if (panel1.BackColor != null) _previousPanelColor = panel1.BackColor;
                                panel1.BackColor = Color.OrangeRed;
                                // play knock sound
                                PlayKnockSound();
                                Application.DoEvents();
                            }
                        }
                        else
                        {
                            if (ledToggler3.Checked)
                            {
                                ledToggler3.Checked = false;

                                panel1.BackColor = _previousPanelColor;//Color.FromArgb(232, 232, 232);
                                Application.DoEvents();
                            }
                        }
                    }
                    break;
                /*case "Knock_status":
                    // better support for knock indicator
                    UInt64 klval = Convert.ToUInt64(dvalue);
                    if ((klval & 0x0000000000000001) > 0)
                    {
                        if (!ledToggler3.Checked)
                        {
                            ledToggler3.Checked = true;
                            // set panel background to indicate knock!
                            if(panel1.BackColor != null) _previousPanelColor = panel1.BackColor;
                            panel1.BackColor = Color.OrangeRed;
                            // play knock sound
                            PlayKnockSound();
                            Application.DoEvents();
                        }
                    }
                    else
                    {
                        if (ledToggler3.Checked)
                        {
                            ledToggler3.Checked = false;

                            panel1.BackColor = _previousPanelColor;//Color.FromArgb(232, 232, 232);
                            Application.DoEvents();
                        }
                    }
                    break;*/
                /* case "Idle_status":
                     // better support for idle status indicator
                     UInt64 ilval = Convert.ToUInt64(dvalue);
                     if ((ilval & 0x0000000000000020) > 0)
                     {
                         if (!ledToggler1.Checked) ledToggler1.Checked = true;
                     }
                     else
                     {
                         if (ledToggler1.Checked) ledToggler1.Checked = false;
                     }
                     break;*/
                case "Pgm_status":
                    // tja... ledjes aan/uit
                    // idle = LedToggler1 if ((buffer[3] & 0x40) > 0)
                    // closed loop = LedToggler2 (byte 3 & 0x02) = lambda control active
                    // Knock = LedToggler3 (byte 1 & 0x02) = knock fuel map
                    // Warmup = LedToggler4 (byte 0 & 0x10) = engine is warm
                    UInt64 lval = Convert.ToUInt64(dvalue);
                    if (type == RealtimeMonitoringType.EngineStatus)
                    {
                        UpdateEngineStatusLeds(lval);
                    }

                    // move to Idle_status
                    if ((lval & 0x0000000040000000) > 0)
                    {
                        if (!ledToggler1.Checked) ledToggler1.Checked = true;
                    }
                    else
                    {
                        if (ledToggler1.Checked) ledToggler1.Checked = false;
                    }

                    if ((lval & 0x0000000002000000) > 0)
                    {
                        if (!ledToggler2.Checked) ledToggler2.Checked = true;
                    }
                    else
                    {
                        if (ledToggler2.Checked) ledToggler2.Checked = false;
                    }
                    // Moved to Knock_offset1234, only T5.5
                    if (type != RealtimeMonitoringType.AutotuneIgnition)
                    {
                        if ((lval & 0x0000000000000200) > 0)
                        {
                            if (!ledToggler3.Checked)
                            {
                                ledToggler3.Checked = true;
                                if (panel1.BackColor != null) _previousPanelColor = panel1.BackColor;
                                // set panel background to indicate knock!
                                panel1.BackColor = Color.OrangeRed;
                                // play knock sound
                                PlayKnockSound();
                                Application.DoEvents();
                            }
                        }
                        else
                        {
                            if (ledToggler3.Checked)
                            {
                                ledToggler3.Checked = false;
                                panel1.BackColor = _previousPanelColor;//Color.FromArgb(232, 232, 232);
                                Application.DoEvents();
                            }
                        }
                    }
                    if ((lval & 0x0000000000000010) > 0)
                    {
                        if (ledToggler4.Checked) ledToggler4.Checked = false;
                    }
                    else
                    {
                        if (!ledToggler4.Checked) ledToggler4.Checked = true;
                    }

                    break;
                case "TARGETAFR":
                    if (!digitalDisplayControl7.Enabled) digitalDisplayControl7.Enabled = true;
                    if (_showAsLambda)
                    {
                        value /= 14.7F;
                        digitalDisplayControl7.DigitText = value.ToString("F2");
                    }
                    else
                    {
                        digitalDisplayControl7.DigitText = value.ToString("F1");
                    }
                    break;
                case "AD_sond":
                case "AD_EGR":
                    _lastAFR.UpdateValue(DateTime.Now, value);
                    if (!digitalDisplayControl4.Enabled)
                    {
                        digitalDisplayControl4.Enabled = true;
                        labelControl4.Enabled = true;
                    }
                    if (!digitalDisplayControl8.Visible)
                    {
                        digitalDisplayControl8.Visible = true;
                    }
                    if (_showAsLambda)
                    {
                        value /= 14.7F;
                        digitalDisplayControl4.DigitText = value.ToString("F2");
                        digitalDisplayControl8.DigitText = value.ToString("F2");
                    }
                    else
                    {
                        digitalDisplayControl4.DigitText = value.ToString("F1");
                        digitalDisplayControl8.DigitText = value.ToString("F1");
                    }
                    if (type == RealtimeMonitoringType.OnlineGraph)
                    {
                        onlineGraphControl1.AddMeasurement("AFR", symbol, DateTime.Now, value, 7, 24, Color.AntiqueWhite);
                    }
                    break;
                case "Lufttemp":
                    _lastAirTemperature.UpdateValue(DateTime.Now, value);
                    if (!digitalDisplayControl3.Enabled)
                    {
                        digitalDisplayControl3.Enabled = true;
                        labelControl3.Enabled = true;
                    }

                    digitalDisplayControl3.DigitText = value.ToString("F0");
                    if (type == RealtimeMonitoringType.OnlineGraph)
                    {
                        onlineGraphControl1.AddMeasurement("IAT", symbol, DateTime.Now, value, -40, 100, Color.BlueViolet);
                    }
                    break;

                case "EGT":
                    _lastthermovalue.UpdateValue(DateTime.Now, value);
                       
                    if (digitalDisplayControl9.Visible = true)
                    {
                        
           
                        digitalDisplayControl9.DigitText = value.ToString("F0");
                    }

                    if (type == RealtimeMonitoringType.OnlineGraph)
                    {
                        onlineGraphControl1.AddMeasurement("EGT", symbol, DateTime.Now, value, 0, 999, Color.Black);
                    }


                    break;

                case "Kyl_temp":
                    _lastCoolantTemperature.UpdateValue(DateTime.Now, value);
                    if (!digitalDisplayControl2.Enabled)
                    {
                        digitalDisplayControl2.Enabled = true;
                        labelControl2.Enabled = true;
                    }
                    digitalDisplayControl2.DigitText = value.ToString("F0");
                    if (type == RealtimeMonitoringType.OnlineGraph)
                    {
                        onlineGraphControl1.AddMeasurement("CT", symbol, DateTime.Now, value, -40, 120, Color.Cornsilk);
                    }


                    if (value > 70)
                    {
                        if (!btnAutoTune.Enabled)
                        {
                            if (_wideBandAFRSymbol != string.Empty)
                            {
                                if (btnAutoTune.Text != "Wait...") // don't enable because of engine temperature when we are waiting for an operation to complete
                                {
                                    btnAutoTune.Enabled = true; // engine is warm enough
                                    CastAutoTuneReady(true);
                                    PlayAutotuneSound();
                                    // <GS-07102010> play a sound when autotune is available
                                }
                            }
                        }
                    }
                    else
                    {
                        if (btnAutoTune.Enabled)
                        {
                            btnAutoTune.Enabled = false;
                            CastAutoTuneReady(false);
                        }
                    }
                    // ALS debug, altijd toestaan <GS-01042010>
                    //btnAutoTune.Enabled = true; // engine is warm enough
                    break;
                case "Rpm":
                    _lastRPM.UpdateValue(DateTime.Now, value);
                    if (!digitalDisplayControl1.Enabled)
                    {
                        digitalDisplayControl1.Enabled = true;
                        labelControl1.Enabled = true;
                    }
                    digitalDisplayControl1.DigitText = value.ToString("F0");
                    _lastInjectorDC.UpdateValue(DateTime.Now, CalculateInjectorDC(_lastRPM.Value, _lastInjectorDuration.Value));
                    linearGauge1.Value = _lastInjectorDC.Value;
                    CalculatePower();
                    CalculateAverageConsumption();
                    if (type == RealtimeMonitoringType.OnlineGraph)
                    {
                        onlineGraphControl1.AddMeasurement("Rpm", symbol, DateTime.Now, value, 0, 8000, Color.Crimson);
                    }

                    break;
                case "P_Manifold10":
                case "P_medel":
                    _lastInletPressure.UpdateValue(DateTime.Now, value);
                    if (!digitalDisplayControl5.Enabled)
                    {
                        digitalDisplayControl5.Enabled = true;
                        labelControl5.Enabled = true;
                    }

                    digitalDisplayControl5.DigitText = value.ToString("F2");
                    UpdatePeakBoost(value);
                    // calculate boost error
                    _lastBoostError.UpdateValue(DateTime.Now, _lastReglTryck.Value - _lastInletPressure.Value);
                    measurementBoostError.Value = _lastBoostError.Value;
                    linearGauge3.Value = value;
                    if (type == RealtimeMonitoringType.OnlineGraph)
                    {
                        onlineGraphControl1.AddMeasurement("Boost", symbol, DateTime.Now, value, -1, 2.5F, Color.Red);
                    }
                    break;
                case "Lacc_mangd":
                    // show be in logfiles as well
                    // update values in fuel tabpage
                    //dvalue = ivalue;
                    //value = ivalue;
                    if (dvalue != _last_Lacc_mangd)
                    {
                        _last_Lacc_mangd = (float)dvalue;
                        //Console.WriteLine("ivalue: " + ivalue.ToString("X8"));

                        //UInt32 testValue = Convert.ToUInt32(dvalue);
                        //Console.WriteLine("AllCyl - double: " + dvalue.ToString());
                        //Console.WriteLine("AllCyl: " + testValue.ToString());

                        SetValueInEnrichmentTable("Enrich load accel cyl #1", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0xFF000000) >> 3 * 8));
                        SetValueInEnrichmentTable("Enrich load accel cyl #2", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x00FF0000) >> 2 * 8));
                        SetValueInEnrichmentTable("Enrich load accel cyl #3", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x0000FF00) >> 1 * 8));
                        SetValueInEnrichmentTable("Enrich load accel cyl #4", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x000000FF)));
                        _last_Lacc_mangd_cyl1 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0xFF000000) >> 3 * 8);
                        _last_Lacc_mangd_cyl2 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x00FF0000) >> 2 * 8);
                        _last_Lacc_mangd_cyl3 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x0000FF00) >> 1 * 8);
                        _last_Lacc_mangd_cyl4 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x000000FF));
                    }
                    break;
                case "Acc_mangd":
                    // update values in fuel tabpage
                    //dvalue = ivalue;

                    if (dvalue != _last_Acc_mangd)
                    {
                        _last_Acc_mangd = (float)dvalue;
                        SetValueInEnrichmentTable("Enrich TPS accel cyl #1", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0xFF000000) >> 3 * 8));
                        SetValueInEnrichmentTable("Enrich TPS accel cyl #2", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x00FF0000) >> 2 * 8));
                        SetValueInEnrichmentTable("Enrich TPS accel cyl #3", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x0000FF00) >> 1 * 8));
                        SetValueInEnrichmentTable("Enrich TPS accel cyl #4", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x000000FF)));
                        _last_Acc_mangd_cyl1 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0xFF000000) >> 3 * 8);
                        _last_Acc_mangd_cyl2 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x00FF0000) >> 2 * 8);
                        _last_Acc_mangd_cyl3 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x0000FF00) >> 1 * 8);
                        _last_Acc_mangd_cyl4 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x000000FF));
                    }
                    break;
                case "Lret_mangd":
                    // dvalue = ivalue;

                    // update values in fuel tabpage
                    if (dvalue != _last_Lret_mangd)
                    {
                        _last_Lret_mangd = (float)dvalue;
                        SetValueInEnrichmentTable("Enlean load accel cyl #1", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0xFF000000) >> 3 * 8));
                        SetValueInEnrichmentTable("Enlean load accel cyl #2", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x00FF0000) >> 2 * 8));
                        SetValueInEnrichmentTable("Enlean load accel cyl #3", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x0000FF00) >> 1 * 8));
                        SetValueInEnrichmentTable("Enlean load accel cyl #4", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x000000FF)));
                        _last_Lret_mangd_cyl1 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0xFF000000) >> 3 * 8);
                        _last_Lret_mangd_cyl2 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x00FF0000) >> 2 * 8);
                        _last_Lret_mangd_cyl3 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x0000FF00) >> 1 * 8);
                        _last_Lret_mangd_cyl4 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x000000FF));
                    }
                    break;
                case "Ret_mangd":
                    //dvalue = ivalue;

                    // update Convert.ToInt32(value)s in fuel tabpage
                    if (dvalue != _last_Ret_mangd)
                    {
                        _last_Ret_mangd = (float)dvalue;
                        SetValueInEnrichmentTable("Enlean TPS accel cyl #1", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0xFF000000) >> 3 * 8));
                        SetValueInEnrichmentTable("Enlean TPS accel cyl #2", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x00FF0000) >> 2 * 8));
                        SetValueInEnrichmentTable("Enlean TPS accel cyl #3", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x0000FF00) >> 1 * 8));
                        SetValueInEnrichmentTable("Enlean TPS accel cyl #4", Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x000000FF)));
                        _last_Ret_mangd_cyl1 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0xFF000000) >> 3 * 8);
                        _last_Ret_mangd_cyl2 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x00FF0000) >> 2 * 8);
                        _last_Ret_mangd_cyl3 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x0000FF00) >> 1 * 8);
                        _last_Ret_mangd_cyl4 = Convert.ToInt32((Convert.ToUInt32(dvalue) & 0x000000FF));

                    }
                    break;
                case "Ign_angle":
                    _lastIgnitionAngle.UpdateValue(DateTime.Now, value);
                    //digitalDisplayControl58.DigitText = value.ToString("F1");
                    measurementIgnitionAdvance.Value = value;
                    measurementIgnitionAdvanceDashboard.Value = value;
                    linearGauge2.Value = value;
                    if (type == RealtimeMonitoringType.OnlineGraph)
                    {
                        onlineGraphControl1.AddMeasurement("Ign", symbol, DateTime.Now, value, -10, 45, Color.Pink);
                    }
                    break;
                case "Knock_offset1":
                    measurementKnockOffset1.Value = value;
                    measurementKnockOffsetCyl1.Value = value;

                    if (value > max_knock_offset1)
                    {
                        measurementKnockOffsetCyl1Max.Value = value;
                        max_knock_offset1 = value;
                    }
                    break;
                case "Knock_offset2":
                    measurementKnockOffset2.Value = value;
                    measurementKnockOffsetCyl2.Value = value;
                    if (value > max_knock_offset2)
                    {
                        measurementKnockOffsetCyl2Max.Value = value;
                        max_knock_offset2 = value;
                    }
                    break;
                case "Knock_offset3":
                    measurementKnockOffset3.Value = value;
                    measurementKnockOffsetCyl3.Value = value;
                    if (value > max_knock_offset3)
                    {
                        measurementKnockOffsetCyl3Max.Value = value;
                        max_knock_offset3 = value;
                    }

                    break;
                case "Knock_offset4":
                    measurementKnockOffset4.Value = value;
                    measurementKnockOffsetCyl4.Value = value;
                    if (value > max_knock_offset4)
                    {
                        measurementKnockOffsetCyl4Max.Value = value;
                        max_knock_offset4 = value;
                    }

                    break;
                case "Max_tryck":
                    measurementBoostRequest.Value = value;
                    _lastMaxTryck.UpdateValue(DateTime.Now, value);
                    break;
                case "Regl_tryck":
                    measurementBoostTarget.Value = value;
                    _lastReglTryck.UpdateValue(DateTime.Now, value);
                    // calculate boost error
                    _lastBoostError.UpdateValue(DateTime.Now, _lastReglTryck.Value - _lastInletPressure.Value);
                    measurementBoostError.Value = _lastBoostError.Value;
                    break;
                case "Medeltrot":
                    _lastTPS.UpdateValue(DateTime.Now, value);
                    measurementTPS.Value = value;
                    break;
                case "Apc_decrese":
                    _lastAPCDecrease.UpdateValue(DateTime.Now, value);
                    //digitalDisplayControl19.DigitText = value.ToString("F2");
                    measurementAPCDecreaseKnock.Value = value;
                    measurementBoostReduction.Value = value;
                    break;
                case "P_fak":
                    _lastPFactor.UpdateValue(DateTime.Now, value);
                    measurementPFactor.Value = value;
                    break;
                case "I_fak":
                    _lastIFactor.UpdateValue(DateTime.Now, value);
                    measurementIFactor.Value = value;
                    break;
                case "D_fak":
                    _lastDFactor.UpdateValue(DateTime.Now, value);
                    measurementDFactor.Value = value;
                    break;
                case "PWM_ut10":
                    _lastPWM.UpdateValue(DateTime.Now, value);
                    measurementPWMOutput.Value = value;
                    if (type == RealtimeMonitoringType.OnlineGraph)
                    {
                        onlineGraphControl1.AddMeasurement("PWM", symbol, DateTime.Now, value, 0, 100, Color.PowderBlue);
                    }
                    break;
                case "Knock_count_cyl1":
                    _lastKnockCountCylinder1.UpdateValue(DateTime.Now, value);
                    measurementKnockCountCyl1.Value = value;
                    if (prev_knock_cyl1 != value)
                    {
                        if (prev_knock_cyl1 != -1)
                        {
                            // dan verkleuren 
                            measurementKnockDiffCyl1.Value = (float)(Convert.ToInt32(value) - prev_knock_cyl1);
                            measurementKnockDiffCyl1.SetColor(Color.Red);
                        }
                        prev_knock_cyl1 = Convert.ToInt32(value);
                    }
                    // if changed, set values in digitalDisplayControl46
                    break;
                case "Knock_count_cyl2":
                    _lastKnockCountCylinder2.UpdateValue(DateTime.Now, value);
                    measurementKnockCountCyl2.Value = value;
                    if (prev_knock_cyl2 != value)
                    {
                        if (prev_knock_cyl2 != -1)
                        {
                            // dan verkleuren 
                            measurementKnockDiffCyl2.Value = (float)(Convert.ToInt32(value) - prev_knock_cyl2);
                            measurementKnockDiffCyl2.SetColor(Color.Red);
                        }
                        prev_knock_cyl2 = Convert.ToInt32(value);
                    }
                    break;
                case "Knock_count_cyl3":
                    _lastKnockCountCylinder3.UpdateValue(DateTime.Now, value);
                    measurementKnockCountCyl3.Value = value;
                    if (prev_knock_cyl3 != value)
                    {
                        if (prev_knock_cyl3 != -1)
                        {
                            // dan verkleuren 
                            measurementKnockDiffCyl3.Value = (float)(Convert.ToInt32(value) - prev_knock_cyl3);
                            measurementKnockDiffCyl3.SetColor(Color.Red);
                        }
                        prev_knock_cyl3 = Convert.ToInt32(value);
                    }
                    break;
                case "Knock_count_cyl4":
                    _lastKnockCountCylinder4.UpdateValue(DateTime.Now, value);
                    measurementKnockCountCyl4.Value = value;
                    if (prev_knock_cyl4 != value)
                    {
                        if (prev_knock_cyl4 != -1)
                        {
                            // dan verkleuren 
                            measurementKnockDiffCyl4.Value = (float)(Convert.ToInt32(value) - prev_knock_cyl4);
                            measurementKnockDiffCyl4.SetColor(Color.Red);
                        }
                        prev_knock_cyl4 = Convert.ToInt32(value);
                    }
                    break;
                case "Knock_average":
                    _lastKnockAverage.UpdateValue(DateTime.Now, value);
                    linearGauge4.Value = value;
                    break;
                case "Bil_hast":
                    _lastVehicleSpeed.UpdateValue(DateTime.Now, value);
                    measurementSpeed.Value = value;
                    break;
                case "TQ":
                    _lastTQ.UpdateValue(DateTime.Now, value);
                    measurementTorque.Value = value;
                    CalculatePower();
                    if (type == RealtimeMonitoringType.OnlineGraph)
                    {
                        onlineGraphControl1.AddMeasurement("TQ", symbol, DateTime.Now, value, 0, 800, Color.SlateGray);
                    }
                    // if > max
                    break;
            }

            if (symbol == m_appSettings.Thermochannelname)
            {
               // _lastThermoValue = value;
               // Console.WriteLine(_lastThermoValue.ToString());
            }
            else if (symbol == m_appSettings.Adc1channelname)
            {
                _lastADC1Value = value;
            }
            else if (symbol == m_appSettings.Adc2channelname)
            {
                _lastADC2Value = value;
            }
            else if (symbol == m_appSettings.Adc3channelname)
            {
                _lastADC3Value = value;
            }
            else if (symbol == m_appSettings.Adc4channelname)
            {
                _lastADC4Value = value;
            }
            else if (symbol == m_appSettings.Adc5channelname)
            {
                _lastADC5Value = value;
            }
            SetValueInFreeLoggingList(symbol, value);
            if (m_autoLoggingEnabled)
            {
                if (m_loggingActive)
                {
                    // check if we should stop the log
                    if (symbol == m_autoLogTriggerStopSymbol)
                    {
                        bool _stopLog = false;
                        if (m_autoLogStopSign == 0 && value == m_autoLogStopValue) _stopLog = true;
                        else if (m_autoLogStopSign == 1 && value > m_autoLogStopValue) _stopLog = true;
                        else if (m_autoLogStopSign == 2 && value < m_autoLogStopValue) _stopLog = true;
                        if (_stopLog)
                        {
                            m_loggingActive = false;
                            simpleButton1.Text = "Start session";
                            CastOpenLogFileEvent(m_logfileName);
                        }
                    }
                }
                else
                {
                    // check if we should start the log
                    if (symbol == m_autoLogTriggerStartSymbol)
                    {
                        bool _startLog = false;
                        if (m_autoLogStartSign == 0 && value == m_autoLogStartValue) _startLog = true;
                        if (m_autoLogStartSign == 1 && value > m_autoLogStartValue) _startLog = true;
                        if (m_autoLogStartSign == 2 && value < m_autoLogStartValue) _startLog = true;
                        if (_startLog)
                        {
                            m_loggingActive = true;
                            m_logfileName = DetermineNewLogFileName();
                            simpleButton1.Text = "Stop session";
                        }
                    }
                }
            }
        }

        private float _peakBoost = -1;

        private void UpdatePeakBoost(float value)
        {
            if (value > 3) return; // ignore possible false readings
            if (value < -1) return; // ignore possible false readings
            if (_peakBoost < value)
            {
                _peakBoost = value;
                if (!digitalDisplayControl6.Enabled)
                {
                    digitalDisplayControl6.Enabled = true;
                    labelControl6.Enabled = true;
                }

                digitalDisplayControl6.DigitText = value.ToString("F2");
            }

        }

        private void CastAutoTuneReady(bool ready)
        {
            if (onAutoTuneStateChanged != null)
            {
                onAutoTuneStateChanged(this, new AutoTuneEventArgs(ready));
            }

        }

        private void UpdateEngineStatusLeds(ulong lval)
        {
            if ((lval & 0x0000000000000001) > 0)
            {
                if (!ledIgnitionKey.Checked) ledIgnitionKey.Checked = true;
            }
            else
            {
                if (ledIgnitionKey.Checked) ledIgnitionKey.Checked = false;
            }
            if ((lval & 0x0000000000000002) > 0)
            {
                if (!ledAfterstart2.Checked) ledAfterstart2.Checked = true;
            }
            else
            {
                if (ledAfterstart2.Checked) ledAfterstart2.Checked = false;
            }
            if ((lval & 0x0000000000000004) > 0)
            {
                if (!ledEngineStopped.Checked) ledEngineStopped.Checked = true;
            }
            else
            {
                if (ledEngineStopped.Checked) ledEngineStopped.Checked = false;
            }
            if ((lval & 0x0000000000000008) > 0)
            {
                if (!ledEngineStarted.Checked) ledEngineStarted.Checked = true;
            }
            else
            {
                if (ledEngineStarted.Checked) ledEngineStarted.Checked = false;
            }
            if ((lval & 0x0000000000000010) > 0)
            {

                if (!ledEngineWarm.Checked) ledEngineWarm.Checked = true;
            }
            else
            {
                if (ledEngineWarm.Checked) ledEngineWarm.Checked = false;
            }
            if ((lval & 0x0000000000000020) > 0)
            {
                if (!ledFuelcut.Checked) ledFuelcut.Checked = true;
            }
            else
            {
                if (ledFuelcut.Checked) ledFuelcut.Checked = false;
            }
            if ((lval & 0x0000000000000040) > 0)
            {
                if (!ledTempCompensation.Checked) ledTempCompensation.Checked = true;
            }
            else
            {
                if (ledTempCompensation.Checked) ledTempCompensation.Checked = false;
            }
            if ((lval & 0x0000000000000080) > 0)
            {
                if (!ledRPMLimiter.Checked) ledRPMLimiter.Checked = true;
            }
            else
            {
                if (ledRPMLimiter.Checked) ledRPMLimiter.Checked = false;
            }
            if ((lval & 0x0000000000000100) > 0)
            {
                if (!ledAppSyncOk.Checked) ledAppSyncOk.Checked = true;
            }
            else
            {
                if (ledAppSyncOk.Checked) ledAppSyncOk.Checked = false;
            }
            if ((lval & 0x0000000000000200) > 0)
            {
                if (!ledFuelKnockMap.Checked) ledFuelKnockMap.Checked = true;
            }
            else
            {
                if (ledFuelKnockMap.Checked) ledFuelKnockMap.Checked = false;
            }
            if ((lval & 0x0000000000000400) > 0)
            {
                if (!ledThrottleClosed.Checked) ledThrottleClosed.Checked = true;
            }
            else
            {
                if (ledThrottleClosed.Checked) ledThrottleClosed.Checked = false;
            }
            if ((lval & 0x0000000000000800) > 0)
            {
                if (!ledRoomTempStart.Checked) ledRoomTempStart.Checked = true;
            }
            else
            {
                if (ledRoomTempStart.Checked) ledRoomTempStart.Checked = false;
            }
            if ((lval & 0x0000000000001000) > 0)
            {
                if (!ledFuelcutCyl4.Checked) ledFuelcutCyl4.Checked = true;
            }
            else
            {
                if (ledFuelcutCyl4.Checked) ledFuelcutCyl4.Checked = false;
            }
            if ((lval & 0x0000000000002000) > 0)
            {
                if (!ledFuelcutCyl3.Checked) ledFuelcutCyl3.Checked = true;
            }
            else
            {
                if (ledFuelcutCyl3.Checked) ledFuelcutCyl3.Checked = false;
            }
            if ((lval & 0x0000000000004000) > 0)
            {
                if (!ledFuelcutCyl2.Checked) ledFuelcutCyl2.Checked = true;
            }
            else
            {
                if (ledFuelcutCyl2.Checked) ledFuelcutCyl2.Checked = false;
            }
            if ((lval & 0x0000000000008000) > 0)
            {
                if (!ledFuelcutCyl1.Checked) ledFuelcutCyl1.Checked = true;
            }
            else
            {
                if (ledFuelcutCyl1.Checked) ledFuelcutCyl1.Checked = false;
            }

            if ((lval & 0x0000000000010000) > 0)
            {
                if (!ledFuelNotOff.Checked) ledFuelNotOff.Checked = true;
            }
            else
            {
                if (ledFuelNotOff.Checked) ledFuelNotOff.Checked = false;
            }
            if ((lval & 0x0000000000020000) > 0)
            {
                if (!ledDecEnleanmentComplete.Checked) ledDecEnleanmentComplete.Checked = true;
            }
            else
            {
                if (ledDecEnleanmentComplete.Checked) ledDecEnleanmentComplete.Checked = false;
            }
            if ((lval & 0x0000000000040000) > 0)
            {
                if (!ledAccelEnrichComplete.Checked) ledAccelEnrichComplete.Checked = true;
            }
            else
            {
                if (ledAccelEnrichComplete.Checked) ledAccelEnrichComplete.Checked = false;
            }
            if ((lval & 0x0000000000080000) > 0)
            {
                if (!ledDecreaseOfEnrichAllowed.Checked) ledDecreaseOfEnrichAllowed.Checked = true;
            }
            else
            {
                if (ledDecreaseOfEnrichAllowed.Checked) ledDecreaseOfEnrichAllowed.Checked = false;
            }
            if ((lval & 0x0000000000100000) > 0)
            {
                if (!ledRetardEnrichInProgress.Checked) ledRetardEnrichInProgress.Checked = true;
            }
            else
            {
                if (ledRetardEnrichInProgress.Checked) ledRetardEnrichInProgress.Checked = false;
            }
            if ((lval & 0x0000000000200000) > 0)
            {
                if (!ledAdaptionAllowed.Checked) ledAdaptionAllowed.Checked = true;
            }
            else
            {
                if (ledAdaptionAllowed.Checked) ledAdaptionAllowed.Checked = false;
            }
            if ((lval & 0x0000000000400000) > 0)
            {
                if (!ledLimpHome.Checked) ledLimpHome.Checked = true;
            }
            else
            {
                if (ledLimpHome.Checked) ledLimpHome.Checked = false;
            }
            if ((lval & 0x0000000000800000) > 0)
            {
                if (!ledAlwaysTempComp.Checked) ledAlwaysTempComp.Checked = true;
            }
            else
            {
                if (ledAlwaysTempComp.Checked) ledAlwaysTempComp.Checked = false;
            }
            if ((lval & 0x0000000001000000) > 0)
            {
                if (!ledRestart.Checked) ledRestart.Checked = true;
            }
            else
            {
                if (ledRestart.Checked) ledRestart.Checked = false;
            }
            if ((lval & 0x0000000002000000) > 0)
            {
                if (!ledActiveLambdaControl.Checked) ledActiveLambdaControl.Checked = true;
            }
            else
            {
                if (ledActiveLambdaControl.Checked) ledActiveLambdaControl.Checked = false;
            }
            if ((lval & 0x0000000004000000) > 0)
            {
                if (!ledAfterStartComplete.Checked) ledAfterStartComplete.Checked = true;
            }
            else
            {
                if (ledAfterStartComplete.Checked) ledAfterStartComplete.Checked = false;
            }
            if ((lval & 0x0000000008000000) > 0)
            {
                if (!ledInitDuringStartCompleted.Checked) ledInitDuringStartCompleted.Checked = true;
            }
            else
            {
                if (ledInitDuringStartCompleted.Checked) ledInitDuringStartCompleted.Checked = false;
            }
            if ((lval & 0x0000000010000000) > 0)
            {
                if (!ledCoolingWaterEnrichFinished.Checked) ledCoolingWaterEnrichFinished.Checked = true;
            }
            else
            {
                if (ledCoolingWaterEnrichFinished.Checked) ledCoolingWaterEnrichFinished.Checked = false;
            }
            if ((lval & 0x0000000020000000) > 0)
            {
                if (!ledPurgeActive.Checked) ledPurgeActive.Checked = true;
            }
            else
            {
                if (ledPurgeActive.Checked) ledPurgeActive.Checked = false;
            }
            if ((lval & 0x0000000040000000) > 0)
            {
                if (!ledIdleFuelMap.Checked) ledIdleFuelMap.Checked = true;
            }
            else
            {
                if (ledIdleFuelMap.Checked) ledIdleFuelMap.Checked = false;
            }
            if ((lval & 0x0000000080000000) > 0)
            {
                if (!ledIgnitionSynced.Checked) ledIgnitionSynced.Checked = true;
            }
            else
            {
                if (ledIgnitionSynced.Checked) ledIgnitionSynced.Checked = false;
            }
            if ((lval & 0x0000000100000000) > 0)
            {
                if (!ledSondHeatingSecondO2.Checked) ledSondHeatingSecondO2.Checked = true;
            }
            else
            {
                if (ledSondHeatingSecondO2.Checked) ledSondHeatingSecondO2.Checked = false;
            }
            if ((lval & 0x0000000200000000) > 0)
            {
                if (!ledSondHeatingFirstO2.Checked) ledSondHeatingFirstO2.Checked = true;
            }
            else
            {
                if (ledSondHeatingFirstO2.Checked) ledSondHeatingFirstO2.Checked = false;
            }
            if ((lval & 0x0000000400000000) > 0)
            {
                if (!ledETSError.Checked) ledETSError.Checked = true;
            }
            else
            {
                if (ledETSError.Checked) ledETSError.Checked = false;
            }
            if ((lval & 0x0000000800000000) > 0)
            {
                if (!ledIdleControlDisable.Checked) ledIdleControlDisable.Checked = true;
            }
            else
            {
                if (ledIdleControlDisable.Checked) ledIdleControlDisable.Checked = false;
            }
            if ((lval & 0x0000001000000000) > 0)
            {
                if (!ledFuelcutAllowed.Checked) ledFuelcutAllowed.Checked = true;
            }
            else
            {
                if (ledFuelcutAllowed.Checked) ledFuelcutAllowed.Checked = false;
            }
            if ((lval & 0x0000002000000000) > 0)
            {
                if (!ledEnrichmentAfterFuelcut.Checked) ledEnrichmentAfterFuelcut.Checked = true;
            }
            else
            {
                if (ledEnrichmentAfterFuelcut.Checked) ledEnrichmentAfterFuelcut.Checked = false;
            }
            if ((lval & 0x0000004000000000) > 0)
            {
                if (!ledFullLoadEnrich.Checked) ledFullLoadEnrich.Checked = true;
            }
            else
            {
                if (ledFullLoadEnrich.Checked) ledFullLoadEnrich.Checked = false;
            }
            if ((lval & 0x0000008000000000) > 0)
            {
                if (!ledFuelSynched.Checked) ledFuelSynched.Checked = true;
            }
            else
            {
                if (ledFuelSynched.Checked) ledFuelSynched.Checked = false;
            }


        }

        private AppSettings m_appSettings;

        public AppSettings AppSettings
        {
            get { return m_appSettings; }
            set { m_appSettings = value; }
        }

        private bool _soundAllowed = true;

        // check for notification sounds
        private void CheckSoundsToPlay(string symbolName, double value)
        {
            string _sound2Play = string.Empty;
            if (symbolName == m_appSettings.Notification1symbol && m_appSettings.Notification1Active)
            {
                // check bounds
                switch (m_appSettings.Notification1condition)
                {
                    case 0: // equal
                        if (value == m_appSettings.Notification1value) _sound2Play = m_appSettings.Notification1sound;
                        break;
                    case 1: // is greater than
                        if (value > m_appSettings.Notification1value) _sound2Play = m_appSettings.Notification1sound;
                        break;
                    case 2: // is smaller than
                        if (value < m_appSettings.Notification1value) _sound2Play = m_appSettings.Notification1sound;
                        break;
                }
            }
            if (symbolName == m_appSettings.Notification2symbol && m_appSettings.Notification2Active && _sound2Play == "")
            {
                // check bounds
                switch (m_appSettings.Notification2condition)
                {
                    case 0: // equal
                        if (value == m_appSettings.Notification2value) _sound2Play = m_appSettings.Notification2sound;
                        break;
                    case 1: // is greater than
                        if (value > m_appSettings.Notification2value) _sound2Play = m_appSettings.Notification2sound;
                        break;
                    case 2: // is smaller than
                        if (value < m_appSettings.Notification2value) _sound2Play = m_appSettings.Notification2sound;
                        break;
                }
            }
            if (symbolName == m_appSettings.Notification3symbol && m_appSettings.Notification3Active && _sound2Play == "")
            {
                // check bounds
                switch (m_appSettings.Notification3condition)
                {
                    case 0: // equal
                        if (value == m_appSettings.Notification3value) _sound2Play = m_appSettings.Notification3sound;
                        break;
                    case 1: // is greater than
                        if (value > m_appSettings.Notification3value) _sound2Play = m_appSettings.Notification3sound;
                        break;
                    case 2: // is smaller than
                        if (value < m_appSettings.Notification3value) _sound2Play = m_appSettings.Notification3sound;
                        break;
                }
            }
            if (_sound2Play != "")
            {
                // we need to play a sound, but not too many times in a row, we have to wait until a previous sound
                // has ended

                if (File.Exists(_sound2Play))
                {
                    try
                    {
                        if (sndplayer != null)
                        {
                            if (_soundAllowed)
                            {
                                _soundAllowed = false; // no more for 2 seconds (sndTimer)
                                sndplayer.SoundLocation = _sound2Play;
                                sndplayer.Play();
                                m_WriteLogMarker = true; // mark this in the logfile immediately
                                sndTimer.Enabled = true;
                            }
                        }
                    }
                    catch (Exception E)
                    {
                        Console.WriteLine(E.Message);
                    }
                }
            }

        }

        private double ConvertADCValue(int channel, float value)
        {
            double retval = value;
            double m_HighVoltage = 5;
            double m_LowVoltage = 0;
            double m_HighValue = 1;
            double m_LowValue = 0;
            switch (channel)
            {
                case 0:
                    m_HighVoltage = m_appSettings.Adc1highvoltage;
                    m_LowVoltage = m_appSettings.Adc1lowvoltage;
                    m_LowValue = m_appSettings.Adc1lowvalue;
                    m_HighValue = m_appSettings.Adc1highvalue;
                    break;
                case 1:
                    m_HighVoltage = m_appSettings.Adc2highvoltage;
                    m_LowVoltage = m_appSettings.Adc2lowvoltage;
                    m_LowValue = m_appSettings.Adc2lowvalue;
                    m_HighValue = m_appSettings.Adc2highvalue;
                    break;
                case 2:
                    m_HighVoltage = m_appSettings.Adc3highvoltage;
                    m_LowVoltage = m_appSettings.Adc3lowvoltage;
                    m_LowValue = m_appSettings.Adc3lowvalue;
                    m_HighValue = m_appSettings.Adc3highvalue;
                    break;
                case 3:
                    m_HighVoltage = m_appSettings.Adc4highvoltage;
                    m_LowVoltage = m_appSettings.Adc4lowvoltage;
                    m_LowValue = m_appSettings.Adc4lowvalue;
                    m_HighValue = m_appSettings.Adc4highvalue;
                    break;
                case 4:
                    m_HighVoltage = m_appSettings.Adc5highvoltage;
                    m_LowVoltage = m_appSettings.Adc5lowvoltage;
                    m_LowValue = m_appSettings.Adc5lowvalue;
                    m_HighValue = m_appSettings.Adc5highvalue;
                    break;
                default:
                    break;
            }
            // convert using the known math
            // convert to AFR value using wideband lambda sensor settings
            // ranges 0 - 255 will be default for 0-5 volt
            double voltage = ((value) / 255) * (m_HighVoltage / 1000 - m_LowVoltage / 1000);
            //Console.WriteLine("Wideband voltage: " + voltage.ToString());
            // now convert to AFR using user settings
            if (voltage < m_LowVoltage / 1000) voltage = m_LowVoltage / 1000;
            if (voltage > m_HighVoltage / 1000) voltage = m_HighVoltage / 1000;
            //Console.WriteLine("Wideband voltage (after clipping): " + voltage.ToString());
            double steepness = ((m_HighValue / 1000) - (m_LowValue / 1000)) / ((m_HighVoltage / 1000) - (m_LowVoltage / 1000));
            //Console.WriteLine("Steepness: " + steepness.ToString());
            retval = (m_LowValue / 1000) + (steepness * (voltage - (m_LowVoltage / 1000)));
            //Console.WriteLine("retval: " + retval.ToString());
            return retval;
        }

        private void PlayKnockSound()
        {
            if (m_appSettings.PlayKnockSound)
            {
                if (File.Exists(System.Windows.Forms.Application.StartupPath + "\\knock.wav"))
                {
                    try
                    {
                        if (sndplayer != null)
                        {
                            sndplayer.SoundLocation = System.Windows.Forms.Application.StartupPath + "\\knock.wav";
                            sndplayer.Play();
                        }
                    }
                    catch (Exception E)
                    {
                        Console.WriteLine(E.Message);
                    }
                }
            }
        }

        private void PlayAutotuneSound()
        {
            if (File.Exists(System.Windows.Forms.Application.StartupPath + "\\autotune.wav"))
            {
                try
                {
                    if (sndplayer != null)
                    {
                        sndplayer.SoundLocation = System.Windows.Forms.Application.StartupPath + "\\autotune.wav";
                        sndplayer.Play();
                    }
                }
                catch (Exception E)
                {
                    Console.WriteLine(E.Message);
                }
            }
        }


        private void CalculateAverageConsumption()
        {
            // calculate average consumption
            // need rpm and injector dc and speed for that
            // number of injections = rpm/2 * 4
            float numberofinjectionsperminute = (((float)_lastRPM.Value / 2) * 4);
            //413cc/minute
            float ccperinjection = (/*413F*/ /*(float)set.InjectorRateCCperMin*/ m_injectorCC / 60000) * (float)_lastInjectorDC.Value;
            float ccperminute = ccperinjection * numberofinjectionsperminute;
            float literperhour = (ccperminute * 60) / 1000;

            if (_lastVehicleSpeed.Value == 0)
            {
                measurementConsumption.Value = literperhour;
                measurementConsumption.MeasurementText = "l/h";
            }
            else
            {
                float kmperliter = _lastVehicleSpeed.Value / literperhour;
                measurementConsumption.Value = kmperliter;
                measurementConsumption.MeasurementText = "km/l";
            }

        }

        private void CalculatePower()
        {
            if (_lastTQ.Value > _maxTQ.Value)
            {
                // update new maximum values
                _maxTQ.UpdateValue(DateTime.Now, _lastTQ.Value);
                _maxTQRPM.UpdateValue(DateTime.Now, _lastRPM.Value);
                measurementPeakTorque.Value = _maxTQ.Value;
                measurementPeakTorqueRPM.Value = _maxTQRPM.Value;
            }
            _lastPower.UpdateValue(DateTime.Now, _lastTQ.Value * (_lastRPM.Value / 7121));
            measurementPower.Value = _lastPower.Value;

            if (_lastPower.Value > _maxPower.Value)
            {
                _maxPower.UpdateValue(DateTime.Now, _lastPower.Value);
                _maxPowerRPM.UpdateValue(DateTime.Now, _lastRPM.Value);
                measurementPeakPower.Value = _maxPower.Value;
                measurementPeakPowerRPM.Value = _maxPowerRPM.Value;
            }
        }

        private void SetValueInEnrichmentTable(string name, Int32 value)
        {
            if (gridControl1.DataSource != null)
            {
                System.Data.DataTable dt = (System.Data.DataTable)gridControl1.DataSource;
                if (dt != null)
                {
                    bool fnd = false;
                    foreach (DataRow dr in dt.Rows)
                    {
                        if (dr["SYMBOLNAME"].ToString() == name)
                        {
                            dr["VALUE"] = value;
                            fnd = true;
                            break;
                        }
                    }
                    if (!fnd)
                    {
                        dt.Rows.Add(name, value);
                    }
                }
            }
            else
            {
                System.Data.DataTable e_dt = new System.Data.DataTable();
                e_dt.Columns.Add("SYMBOLNAME");
                e_dt.Columns.Add("VALUE", Type.GetType("System.Int32"));
                e_dt.Rows.Add(name, value);
                gridControl1.DataSource = e_dt;
            }
        }

        private string AddToLine(string name, RealtimeValue value)
        {
            string retstring = string.Empty;
            TimeSpan _ts = new TimeSpan(DateTime.Now.Ticks - value.LastUpdate.Ticks);
            if (_ts.TotalSeconds < 3)
            {
                retstring = name + "=" + value.Value.ToString("F2") + "|";
            }
            return retstring;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            // log the latest information if logging is enabled
            string _line = string.Empty;
            if (m_loggingActive && m_logfileName != string.Empty)
            {
                // do this to the lastest determined logfilename (session based)
                // if symbols are not in the realtime list anymore, don't log them, so only symbols in the realtime list
                //25/08/2009 07:59:25.000
                _line = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
                _line += "." + DateTime.Now.Millisecond.ToString("D3") + "|";
                if (type != RealtimeMonitoringType.Userdefined) // <GS-04042011> only write this when NOT in userdefined tab
                {
                    _line += AddToLine("Rpm", _lastRPM);
                    _line += AddToLine("Speed", _lastVehicleSpeed);
                    _line += AddToLine("Coolant", _lastCoolantTemperature);
                    _line += AddToLine("IAT", _lastAirTemperature);
                    _line += AddToLine("AFR", _lastAFR);
                    _line += AddToLine("Boost", _lastInletPressure);
                    _line += AddToLine("TPS", _lastTPS);
                }
                if (type == RealtimeMonitoringType.Dashboard || type == RealtimeMonitoringType.Fuel)
                {
                    _line += AddToLine("InjectorDC", _lastInjectorDC);
                    _line += AddToLine("Injection duration", _lastInjectorDuration);
                }
                if (type == RealtimeMonitoringType.Ignition || type == RealtimeMonitoringType.Dashboard)
                {
                    _line += AddToLine("Ignition angle", _lastIgnitionAngle);
                }
                if (type == RealtimeMonitoringType.Boost)
                {
                    _line += AddToLine("Target boost", _lastReglTryck);
                    _line += AddToLine("Boost request", _lastMaxTryck);
                    _line += AddToLine("Boost error", _lastBoostError);
                    _line += AddToLine("P gain", _lastPFactor);
                    _line += AddToLine("I gain", _lastIFactor);
                    _line += AddToLine("D gain", _lastDFactor);
                    _line += AddToLine("PWM APC", _lastPWM);
                }
                if (type == RealtimeMonitoringType.Boost || type == RealtimeMonitoringType.Knock || type == RealtimeMonitoringType.Dashboard)
                {
                    _line += AddToLine("Boost reduction", _lastAPCDecrease);
                }
                if (type == RealtimeMonitoringType.Knock)
                {
                    _line += AddToLine("Knock average", _lastKnockAverage);
                    //_line += AddToLine(_lastKnockCountCylinder1);
                    //_line += AddToLine(_lastKnockCountCylinder2);
                    //_line += AddToLine(_lastKnockCountCylinder3);
                    //_line += AddToLine(_lastKnockCountCylinder4);
                }
                if (type == RealtimeMonitoringType.Dashboard)
                {
                    _line += AddToLine("Torque", _lastTQ);
                    _line += AddToLine("Power", _lastPower);
                }
                // only log these in fuel monitoring mode
                if (type == RealtimeMonitoringType.Fuel)
                {
                    _line += "TPSAccCyl1=" + _last_Acc_mangd_cyl1.ToString() + "|";
                    _line += "TPSAccCyl2=" + _last_Acc_mangd_cyl2.ToString() + "|";
                    _line += "TPSAccCyl3=" + _last_Acc_mangd_cyl3.ToString() + "|";
                    _line += "TPSAccCyl4=" + _last_Acc_mangd_cyl4.ToString() + "|";
                    _line += "LoadAccCyl1=" + _last_Lacc_mangd_cyl1.ToString() + "|";
                    _line += "LoadAccCyl2=" + _last_Lacc_mangd_cyl2.ToString() + "|";
                    _line += "LoadAccCyl3=" + _last_Lacc_mangd_cyl3.ToString() + "|";
                    _line += "LoadAccCyl4=" + _last_Lacc_mangd_cyl4.ToString() + "|";
                    _line += "TPSRetCyl1=" + _last_Ret_mangd_cyl1.ToString() + "|";
                    _line += "TPSRetCyl2=" + _last_Ret_mangd_cyl2.ToString() + "|";
                    _line += "TPSRetCyl3=" + _last_Ret_mangd_cyl3.ToString() + "|";
                    _line += "TPSRetCyl4=" + _last_Ret_mangd_cyl4.ToString() + "|";
                    _line += "LoadRetCyl1=" + _last_Lret_mangd_cyl1.ToString() + "|";
                    _line += "LoadRetCyl2=" + _last_Lret_mangd_cyl2.ToString() + "|";
                    _line += "LoadRetCyl3=" + _last_Lret_mangd_cyl3.ToString() + "|";
                    _line += "LoadRetCyl4=" + _last_Lret_mangd_cyl4.ToString() + "|";
                }
                if (type == RealtimeMonitoringType.Userdefined)
                {
                    // get all values from the gridview at this moment //<GS-29032010>
                    DataTable dt = (DataTable)gridControl4.DataSource;
                    foreach (DataRow dr in dt.Rows)
                    {
                        string symbolName = dr["SYMBOLNAME"].ToString();
                        if (symbolName != "Pgm_status")
                        {
                            float value = (float)Convert.ToDouble(dr["VALUE"]);
                            // we should check if this is alread logged!
                            //if (symbolName != "Rpm" && symbolName != "Bil_hast" && symbolName != "Kyl_temp" && symbolName != "Lufttemp" && symbolName != "AD_EGR" && symbolName != "P_medel" && symbolName != "Medeltrot")
                            {
                                _line += symbolName + "=" + value.ToString() + "|";
                            }
                        }

                    }

                }
                if (type != RealtimeMonitoringType.Userdefined)
                {
                    if (ledToggler3.Checked) _line += "KnockInfo=1|";
                    else _line += "KnockInfo=0|";
                    if (ledToggler1.Checked) _line += "Idle=1|";
                    else _line += "Idle=0|";
                    if (ledToggler2.Checked) _line += "ClosedLoop=1|";
                    else _line += "ClosedLoop=0|";
                    if (ledToggler4.Checked) _line += "Warmup=1|";
                    else _line += "Warmup=0|";
                    if (m_WriteLogMarker)
                    {
                        m_WriteLogMarker = false;
                        _line += "ImportantLine=1|";
                    }
                    else
                    {
                        _line += "ImportantLine=0|";
                    }
                    if (m_appSettings.Usethermo)
                    {
                       
                        // also log thermo value
                       // _line += m_appSettings.Thermochannelname + "=" + _lastThermoValue.ToString("F2") + "|";
                    }
                    if (m_appSettings.Useadc1)
                    {
                        _line += m_appSettings.Adc1channelname + "=" + _lastADC1Value.ToString("F2") + "|";
                    }
                    if (m_appSettings.Useadc2)
                    {
                        _line += m_appSettings.Adc2channelname + "=" + _lastADC2Value.ToString("F2") + "|";
                    }
                    if (m_appSettings.Useadc3)
                    {
                        _line += m_appSettings.Adc3channelname + "=" + _lastADC3Value.ToString("F2") + "|";
                    }
                    if (m_appSettings.Useadc4)
                    {
                        _line += m_appSettings.Adc4channelname + "=" + _lastADC4Value.ToString("F2") + "|";
                    }
                    if (m_appSettings.Useadc5)
                    {
                        _line += m_appSettings.Adc5channelname + "=" + _lastADC5Value.ToString("F2") + "|";
                    }
                }
                /*
                                    // idle = LedToggler1 
                                    // closed loop = LedToggler2 
                                    // Knock = LedToggler3 
                                    // Warmup = LedToggler4 
                 * */
                using (StreamWriter sw = new StreamWriter(m_logfileName, true))
                {
                    sw.WriteLine(_line);
                }
            }
        }

        private void ResizeControls()
        {
            measurementAPCDecreaseDashboard.SizeControl();
            measurementAPCDecreaseKnock.SizeControl();
            measurementBoostBias.SizeControl();
            measurementBoostError.SizeControl();
            measurementBoostReduction.SizeControl();
            measurementBoostRequest.SizeControl();
            measurementBoostTarget.SizeControl();
            measurementConsumption.SizeControl();
            measurementDFactor.SizeControl();
            measurementIFactor.SizeControl();
            measurementIgnitionAdapt.SizeControl();
            measurementIgnitionAdvance.SizeControl();
            measurementIgnitionAdvanceDashboard.SizeControl();
            measurementIgnitionKnock.SizeControl();
            measurementIgnitionRetardKnock.SizeControl();
            measurementIgnitionTrim.SizeControl();
            measurementKnockCountCyl1.SizeControl();
            measurementKnockCountCyl2.SizeControl();
            measurementKnockCountCyl3.SizeControl();
            measurementKnockCountCyl4.SizeControl();
            measurementKnockDiffCyl1.SizeControl();
            measurementKnockDiffCyl2.SizeControl();
            measurementKnockDiffCyl3.SizeControl();
            measurementKnockDiffCyl4.SizeControl();
            measurementKnockOffset1.SizeControl();
            measurementKnockOffset2.SizeControl();
            measurementKnockOffset3.SizeControl();
            measurementKnockOffset4.SizeControl();
            measurementKnockOffsetCyl1.SizeControl();
            measurementKnockOffsetCyl1Max.SizeControl();
            measurementKnockOffsetCyl2.SizeControl();
            measurementKnockOffsetCyl2Max.SizeControl();
            measurementKnockOffsetCyl3.SizeControl();
            measurementKnockOffsetCyl3Max.SizeControl();
            measurementKnockOffsetCyl4.SizeControl();
            measurementKnockOffsetCyl4Max.SizeControl();
            measurementPeakPower.SizeControl();
            measurementPeakPowerRPM.SizeControl();
            measurementPeakTorque.SizeControl();
            measurementPeakTorqueRPM.SizeControl();
            measurementPFactor.SizeControl();
            measurementPower.SizeControl();
            measurementPWMOutput.SizeControl();
            measurementSpeed.SizeControl();
            measurementTorque.SizeControl();
            measurementTPS.SizeControl();



            /*ledAccelEnrichComplete.BackColor = Color.FromArgb(133, 133, 133);
            ledActiveLambdaControl.BackColor = Color.FromArgb(133, 133, 133);
            ledAdaptionAllowed.BackColor = Color.FromArgb(133, 133, 133);
            ledAfterstart2.BackColor = Color.FromArgb(133, 133, 133);
            ledAfterStartComplete.BackColor = Color.FromArgb(133, 133, 133);
            ledAlwaysTempComp.BackColor = Color.FromArgb(133, 133, 133);
            ledAppSyncOk.BackColor = Color.FromArgb(133, 133, 133);
            ledCoolingWaterEnrichFinished.BackColor = Color.FromArgb(133, 133, 133);
            ledDecEnleanmentComplete.BackColor = Color.FromArgb(133, 133, 133);
            ledDecreaseOfEnrichAllowed.BackColor = Color.FromArgb(133, 133, 133);
            ledEngineStarted.BackColor = Color.FromArgb(133, 133, 133);
            ledEngineStopped.BackColor = Color.FromArgb(133, 133, 133);
            ledEngineWarm.BackColor = Color.FromArgb(133, 133, 133);
            ledEnrichmentAfterFuelcut.BackColor = Color.FromArgb(133, 133, 133);
            ledETSError.BackColor = Color.FromArgb(133, 133, 133);
            ledFuelcut.BackColor = Color.FromArgb(133, 133, 133);
            ledFuelcutAllowed.BackColor = Color.FromArgb(133, 133, 133);
            ledFuelcutCyl1.BackColor = Color.FromArgb(133, 133, 133);
            ledFuelcutCyl2.BackColor = Color.FromArgb(133, 133, 133);
            ledFuelcutCyl3.BackColor = Color.FromArgb(133, 133, 133);
            ledFuelcutCyl4.BackColor = Color.FromArgb(133, 133, 133);
            ledFuelKnockMap.BackColor = Color.FromArgb(133, 133, 133);
            ledFuelNotOff.BackColor = Color.FromArgb(133, 133, 133);
            ledFuelSynched.BackColor = Color.FromArgb(133, 133, 133);
            ledFullLoadEnrich.BackColor = Color.FromArgb(133, 133, 133);
            ledIdleControlDisable.BackColor = Color.FromArgb(133, 133, 133);
            ledIdleFuelMap.BackColor = Color.FromArgb(133, 133, 133);
            ledIgnitionKey.BackColor = Color.FromArgb(133, 133, 133);
            ledIgnitionSynced.BackColor = Color.FromArgb(133, 133, 133);
            ledInitDuringStartCompleted.BackColor = Color.FromArgb(133, 133, 133);
            ledKnockMap.BackColor = Color.FromArgb(133, 133, 133);
            ledLimpHome.BackColor = Color.FromArgb(133, 133, 133);
            ledPurgeActive.BackColor = Color.FromArgb(133, 133, 133);
            ledRestart.BackColor = Color.FromArgb(133, 133, 133);
            ledRetardEnrichInProgress.BackColor = Color.FromArgb(133, 133, 133);
            ledRoomTempStart.BackColor = Color.FromArgb(133, 133, 133);
            ledRPMLimiter.BackColor = Color.FromArgb(133, 133, 133);
            ledSondHeatingFirstO2.BackColor = Color.FromArgb(133, 133, 133);
            ledSondHeatingSecondO2.BackColor = Color.FromArgb(133, 133, 133);
            ledTempCompensation.BackColor = Color.FromArgb(133, 133, 133);
            ledThrottleClosed.BackColor = Color.FromArgb(133, 133, 133);
            ledToggler1.BackColor = Color.FromArgb(133, 133, 133);
            ledToggler2.BackColor = Color.FromArgb(133, 133, 133);
            ledToggler3.BackColor = Color.FromArgb(133, 133, 133);
            ledToggler4.BackColor = Color.FromArgb(133, 133, 133);
            measurementAPCDecreaseDashboard.BackColor = Color.FromArgb(133, 133, 133);
            measurementAPCDecreaseKnock.BackColor = Color.FromArgb(133, 133, 133);
            measurementBoostBias.BackColor = Color.FromArgb(133, 133, 133);
            measurementBoostError.BackColor = Color.FromArgb(133, 133, 133);
            measurementBoostReduction.BackColor = Color.FromArgb(133, 133, 133);
            measurementBoostRequest.BackColor = Color.FromArgb(133, 133, 133);
            measurementBoostTarget.BackColor = Color.FromArgb(133, 133, 133);
            measurementConsumption.BackColor = Color.FromArgb(133, 133, 133);
            measurementDFactor.BackColor = Color.FromArgb(133, 133, 133);
            measurementIFactor.BackColor = Color.FromArgb(133, 133, 133);
            measurementIgnitionAdapt.BackColor = Color.FromArgb(133, 133, 133);
            measurementIgnitionAdvance.BackColor = Color.FromArgb(133, 133, 133);
            measurementIgnitionAdvanceDashboard.BackColor = Color.FromArgb(133, 133, 133);
            measurementIgnitionKnock.BackColor = Color.FromArgb(133, 133, 133);
            measurementIgnitionRetardKnock.BackColor = Color.FromArgb(133, 133, 133);
            measurementIgnitionTrim.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockCountCyl1.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockCountCyl2.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockCountCyl3.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockCountCyl4.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockDiffCyl1.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockDiffCyl2.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockDiffCyl3.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockDiffCyl4.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockOffset1.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockOffset2.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockOffset3.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockOffset4.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockOffsetCyl1.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockOffsetCyl1Max.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockOffsetCyl2.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockOffsetCyl2Max.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockOffsetCyl3.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockOffsetCyl3Max.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockOffsetCyl4.BackColor = Color.FromArgb(133, 133, 133);
            measurementKnockOffsetCyl4Max.BackColor = Color.FromArgb(133, 133, 133);
            measurementPeakPower.BackColor = Color.FromArgb(133, 133, 133);
            measurementPeakPowerRPM.BackColor = Color.FromArgb(133, 133, 133);
            measurementPeakTorque.BackColor = Color.FromArgb(133, 133, 133);
            measurementPeakTorqueRPM.BackColor = Color.FromArgb(133, 133, 133);
            measurementPFactor.BackColor = Color.FromArgb(133, 133, 133);
            measurementPower.BackColor = Color.FromArgb(133, 133, 133);
            measurementPWMOutput.BackColor = Color.FromArgb(133, 133, 133);
            measurementSpeed.BackColor = Color.FromArgb(133, 133, 133);
            measurementTorque.BackColor = Color.FromArgb(133, 133, 133);
            measurementTPS.BackColor = Color.FromArgb(133, 133, 133);*/

        }

        private void xtraTabControl1_SelectedPageChanged(object sender, DevExpress.XtraTab.TabPageChangedEventArgs e)
        {
            // Stop monitoring if session is active
            ResizeControls();
            bool _loggingWasActive = false;

            if (m_loggingActive)
            {
                _loggingWasActive = m_loggingActive;
                m_loggingActive = false;
            }
            if (_EnableAdvancedMode) btnMapEdit.Visible = true;
            digitalDisplayControl7.Visible = false;



            type = RealtimeMonitoringType.Fuel;
            SymbolCollection sc = new SymbolCollection();
            // notify parent of changed tabpage, the realtime stuff should be filled differently in that case
            if (e.Page.Text == xtraTabPage1.Text)
            {
                type = RealtimeMonitoringType.Fuel;
                // fill with fuel map
                SymbolHelper sh_fuel = new SymbolHelper();
                sh_fuel.Varname = "Insp_mat!";
                sh_fuel.Helptext = "Main fuel map";
                sc.Add(sh_fuel);
                SymbolHelper sh_korr = new SymbolHelper();
                sh_korr.Varname = "Adapt_korr!";
                sh_korr.Helptext = "Fuel adaption";
                sc.Add(sh_korr);
                SymbolHelper sh_fuel_mut = new SymbolHelper();
                sh_fuel_mut.Varname = "FuelAdjustmentMap";
                sh_fuel_mut.Helptext = "Autotune fuel";
                sc.Add(sh_fuel_mut);
                SymbolHelper sh_knock = new SymbolHelper();
                sh_knock.Varname = "Fuel_knock_mat!";
                sh_knock.Helptext = "Knock fuel map";
                sc.Add(sh_knock);
                SymbolHelper sh_injconst = new SymbolHelper();
                sh_injconst.Varname = "Inj_konst!";
                sh_injconst.Helptext = "Injector constant";
                sc.Add(sh_injconst);
                btnMapEdit.Relevantsymbols = sc;
            }
            else if (e.Page.Text == xtraTabPage2.Text)
            {
                type = RealtimeMonitoringType.Ignition;
                // fill with ignition maps
                SymbolHelper sh_ign = new SymbolHelper();
                sh_ign.Varname = "Ign_map_0!";
                sh_ign.Helptext = "Main ignition map";
                sc.Add(sh_ign);
                SymbolHelper sh_ignwarmup = new SymbolHelper();
                sh_ignwarmup.Varname = "Ign_map_4!";
                sh_ignwarmup.Helptext = "Warmup ignition map";
                sc.Add(sh_ignwarmup);
                SymbolHelper sh_ignknock = new SymbolHelper();
                sh_ignknock.Varname = "Ign_map_2!";
                sh_ignknock.Helptext = "Knock ignition map";
                sc.Add(sh_ignknock);
                btnMapEdit.Relevantsymbols = sc;
            }
            else if (e.Page.Text == xtraTabPage3.Text)
            {
                type = RealtimeMonitoringType.Boost;
                SymbolHelper sh_boostrequest = new SymbolHelper();
                sh_boostrequest.Varname = "Tryck_mat!";
                sh_boostrequest.Helptext = "Boost request map";
                sc.Add(sh_boostrequest);
                SymbolHelper sh_regkonmat = new SymbolHelper();
                sh_regkonmat.Varname = "Reg_kon_mat!";
                sh_regkonmat.Helptext = "Boost bias map";
                sc.Add(sh_regkonmat);
                SymbolHelper sh_pfact = new SymbolHelper();
                sh_pfact.Varname = "P_fors!";
                sh_pfact.Helptext = "P factors";
                sc.Add(sh_pfact);
                SymbolHelper sh_ifact = new SymbolHelper();
                sh_ifact.Varname = "I_fors!";
                sh_ifact.Helptext = "I factors";
                sc.Add(sh_ifact);
                SymbolHelper sh_dfact = new SymbolHelper();
                sh_dfact.Varname = "D_fors!";
                sh_dfact.Helptext = "D factors";
                sc.Add(sh_dfact);
                btnMapEdit.Relevantsymbols = sc;
            }
            else if (e.Page.Text == xtraTabPage4.Text)
            {
                type = RealtimeMonitoringType.Knock;
                SymbolHelper sh_knock_ref = new SymbolHelper();
                sh_knock_ref.Varname = "Knock_ref_matrix!";
                sh_knock_ref.Helptext = "Knock sensitivity";
                sc.Add(sh_knock_ref);
                SymbolHelper sh_knock_count = new SymbolHelper();
                sh_knock_count.Varname = "Knock_count_map";
                sh_knock_count.Helptext = "Knock counter map";
                sc.Add(sh_knock_count);
                SymbolHelper sh_ignknock = new SymbolHelper();
                sh_ignknock.Varname = "Ign_map_2!";
                sh_ignknock.Helptext = "Knock ignition map";
                sc.Add(sh_ignknock);
                SymbolHelper sh_knock = new SymbolHelper();
                sh_knock.Varname = "Fuel_knock_mat!";
                sh_knock.Helptext = "Knock fuel map";
                sc.Add(sh_knock);

                btnMapEdit.Relevantsymbols = sc;
            }
            else if (e.Page.Text == xtraTabPage5.Text)
            {
                type = RealtimeMonitoringType.Dashboard;
                btnMapEdit.Relevantsymbols = sc;

            }
            else if (e.Page.Text == xtraTabPage6.Text)
            {
                type = RealtimeMonitoringType.Settings;

                btnMapEdit.Relevantsymbols = sc;

            }
            else if (e.Page.Text == xtraTabPage7.Text)
            {
                type = RealtimeMonitoringType.Userdefined;
                // only show symbol that are in the watchlist
                // clear the map, it will be filled by itself
                System.Data.DataTable dtnew = new System.Data.DataTable();
                if (gridControl4.DataSource == null)
                {
                    dtnew.TableName = "RTSymbols";
                    dtnew.Columns.Add("SYMBOLNAME");
                    dtnew.Columns.Add("MINVALUE", Type.GetType("System.Double")); // autosense max and min!
                    dtnew.Columns.Add("MAXVALUE", Type.GetType("System.Double"));
                    dtnew.Columns.Add("VALUE", Type.GetType("System.Double"));
                    dtnew.Columns.Add("PEAK", Type.GetType("System.Double"));
                    gridControl4.DataSource = dtnew;
                }
                btnMapEdit.Relevantsymbols = sc;
            }
            else if (e.Page.Text == xtraTabPage8.Text)
            {
                type = RealtimeMonitoringType.EngineStatus;
                btnMapEdit.Relevantsymbols = sc;
            }
            else if (e.Page.Text == xtraTabPage9.Text)
            {
                type = RealtimeMonitoringType.OnlineGraph;
                btnMapEdit.Relevantsymbols = sc;
            }
            else if (e.Page.Text == tabAutoTuneIgnition.Text)
            {
                type = RealtimeMonitoringType.AutotuneIgnition;
            }
            else if (e.Page.Text == tabAutoTune.Text)
            {
                type = RealtimeMonitoringType.AutotuneFuel;
                btnMapEdit.Visible = false;
                digitalDisplayControl7.Visible = true;
            }
            else if (e.Page.Text == tabUserMaps.Text)
            {
                type = RealtimeMonitoringType.UserMaps;
                btnMapEdit.Relevantsymbols = sc;

                // load the grid
                DataTable dt = new DataTable();
                dt.TableName = "UserMaps";
                dt.Columns.Add("Mapname");
                dt.Columns.Add("Description");
                if (File.Exists(Application.StartupPath + "\\UserMaps.xml"))
                {
                    dt.ReadXml(Application.StartupPath + "\\UserMaps.xml");
                }
                /*else
                {
                    dt.Columns.Add("Mapname");
                    dt.Columns.Add("Description");
                }*/
                gridControl5.DataSource = dt;


            }
            if (e.PrevPage.Text == tabUserMaps.Text)
            {
                // save
                if (gridControl5.DataSource != null)
                {
                    DataTable dt = (DataTable)gridControl5.DataSource;
                    dt.WriteXml(Application.StartupPath + "\\UserMaps.xml");
                }
            }
            if (onMonitorTypeChanged != null)
            {
                // disable all controls in the bottom to indicate we don't have data for them at this point.
                // next, when data is received for one of the controls, enable them again
                DisableGenericControls();

                onMonitorTypeChanged(this, new RealtimeMonitoringEventArgs(type));

            }

            if (_loggingWasActive)
            {
                m_loggingActive = true;
                m_logfileName = DetermineNewLogFileName();
            }
        }

        private void DisableGenericControls()
        {
            //digitalDisplayControl1.Enabled = false;
            //digitalDisplayControl1.DigitText = "----"; // rpm = 4 digits
            //labelControl1.Enabled = false;
            digitalDisplayControl2.Enabled = false;
            digitalDisplayControl2.DigitText = "---";
            labelControl2.Enabled = false;
            digitalDisplayControl3.Enabled = false;
            digitalDisplayControl3.DigitText = "---";
            labelControl3.Enabled = false;
            digitalDisplayControl4.Enabled = false;
            digitalDisplayControl4.DigitText = "---";
            labelControl4.Enabled = false;
            //digitalDisplayControl5.Enabled = false;
            //digitalDisplayControl5.DigitText = "---";
            //labelControl5.Enabled = false;
            //digitalDisplayControl6.Enabled = false;
            //digitalDisplayControl6.DigitText = "---";
            //labelControl6.Enabled = false;
        }

        public class AutoTuneEventArgs : System.EventArgs
        {
            private bool _ready;

            public bool Ready
            {
                get { return _ready; }
                set { _ready = value; }
            }

            public AutoTuneEventArgs(bool ready)
            {
                _ready = ready;
            }
        }

        public class ClosedLoopOnOffEventArgs : System.EventArgs
        {
            private bool _SwitchOn;

            public bool SwitchOn
            {
                get { return _SwitchOn; }
                set { _SwitchOn = value; }
            }

            public ClosedLoopOnOffEventArgs(bool on)
            {
                _SwitchOn = on;
            }
        }


        public class OpenLogFileRequestEventArgs : System.EventArgs
        {
            private string _filename;

            public string Filename
            {
                get { return _filename; }
                set { _filename = value; }
            }

            public OpenLogFileRequestEventArgs(string filename)
            {
                this._filename = filename;
            }
        }

        public class MapDisplayRequestEventArgs : System.EventArgs
        {
            private double _correctionFactor = 1;

            public double CorrectionFactor
            {
                get { return _correctionFactor; }
                set { _correctionFactor = value; }
            }
            private double _correctionOffset = 0;

            public double CorrectionOffset
            {
                get { return _correctionOffset; }
                set { _correctionOffset = value; }
            }
            private bool _useUserCorrection = false;

            public bool UseUserCorrection
            {
                get { return _useUserCorrection; }
                set { _useUserCorrection = value; }
            }

            private string _mapname;

            public string MapName
            {
                get { return _mapname; }
                set { _mapname = value; }
            }

            public MapDisplayRequestEventArgs(string mapname, double correctionfactor, double correctionoffset, bool useusercorrection)
            {
                this._mapname = mapname;
                this._correctionFactor = correctionfactor;
                this._correctionOffset = correctionoffset;
                this._useUserCorrection = useusercorrection;
            }
        }

        public class ProgramModeEventArgs : System.EventArgs
        {
            private int _byteNumber = 0;

            public int ByteNumber
            {
                get { return _byteNumber; }
                set { _byteNumber = value; }
            }
            private byte _mask = 0;

            public byte Mask
            {
                get { return _mask; }
                set { _mask = value; }
            }

            private bool _enable;

            public bool Enable
            {
                get { return _enable; }
                set { _enable = value; }
            }

            public ProgramModeEventArgs(int bytenumber, byte mask, bool enable)
            {
                _byteNumber = bytenumber;
                _mask = mask;
                _enable = enable;
            }
        }

        public class LoggingEventArgs : System.EventArgs
        {
            private string _file;

            public string File
            {
                get { return _file; }
                set { _file = value; }
            }

            public LoggingEventArgs(string file)
            {
                this._file = file;
            }
        }

        public class RealtimeMonitoringEventArgs : System.EventArgs
        {
            private RealtimeMonitoringType _type;

            public RealtimeMonitoringType Type
            {
                get { return _type; }
                set { _type = value; }
            }

            public RealtimeMonitoringEventArgs(RealtimeMonitoringType type)
            {
                this._type = type;
            }
        }

        private void gridView1_CustomDrawCell(object sender, DevExpress.XtraGrid.Views.Base.RowCellCustomDrawEventArgs e)
        {
            // give a little color to the enrichment/enleanment cells
            // indicate value in colors just as in realtime panel, max is always 255
            if (e.Column.Name == gcValue.Name)
            {
                // enrichment = green to orangered
                // enleanment = green to Blue
                System.Drawing.Rectangle rect = new System.Drawing.Rectangle(e.Bounds.X - 1, e.Bounds.Y, e.Bounds.Width + 1, e.Bounds.Height);
                Brush brush = new System.Drawing.Drawing2D.LinearGradientBrush(rect, Color.LightGreen, Color.OrangeRed, System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
                if (gridView1.GetRowCellValue(e.RowHandle, gcSymbol).ToString().ToLower().Contains("enlean"))
                {
                    brush = new System.Drawing.Drawing2D.LinearGradientBrush(rect, Color.LightGreen, Color.Blue, System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
                }
                try
                {
                    DataRow dr = gridView1.GetDataRow(e.RowHandle);
                    if (dr != null && e.DisplayText != null)
                    {
                        int actualvalue = Convert.ToInt32(e.CellValue);
                        //double range = 255;

                        double percentage = (double)(actualvalue * 100) / 255;
                        percentage /= 100;
                        if (percentage < 0) percentage = 0;
                        if (percentage > 1) percentage = 1;
                        double xwidth = percentage * (double)(e.Bounds.Width - 2);
                        if (xwidth > 0)
                        {
                            e.Graphics.FillRectangle(brush, e.Bounds.X + 1, e.Bounds.Y + 1, (float)xwidth, e.Bounds.Height - 2);
                        }
                    }
                }
                catch (Exception E)
                {
                    Console.WriteLine(E.Message);
                }

            }
        }



        private void btnMapEdit_onRequestMapDisplay(object sender, MapEditButton.ShowSymbolEventArgs e)
        {
            // re-cast to parent (main application)
            if (onMapDisplayRequested != null)
            {
                onMapDisplayRequested(this, new MapDisplayRequestEventArgs(e.SymbolName, 1, 0, false));
            }

        }

        private bool _autoTuningIgnition = false;

        public bool AutoTuningIgnition
        {
            get { return _autoTuningIgnition; }
            //set { _autoTuningIgnition = value; }
        }

        private bool _autoTuning = false;

        public bool AutoTuning
        {
            get { return _autoTuning; }
            //set { _autoTuning = value; }
        }

        private void btnAutoTune_Click(object sender, EventArgs e)
        {
            btnAutoTune.Enabled = false;
            btnAutoTune.Text = "Wait...";
            Application.DoEvents();

            if (_autoTuning)
            {
                //btnAutoTune.Text = "Autotune";
                StopAutoTune();
            }
            else
            {
                //btnAutoTune.Text = "Tuning...";
                this.xtraTabControl1.SelectedTabPage = tabAutoTune;
                StartAutoTune();

            }
        }

        private void SwitchClosedLoopOperation(bool on)
        {
            // signal application to turn closed loop on or off
            if (onSwitchClosedLoopOnOff != null)
            {
                onSwitchClosedLoopOnOff(this, new ClosedLoopOnOffEventArgs(on));
            }
        }

        public void SetAutoTuneIgnitionButtonText(string text)
        {
            btnAutoTuneIgnition.Text = text;
            btnAutoTuneIgnition.Enabled = true;
            Application.DoEvents();
        }

        public void SetAutoTuneButtonText(string text)
        {
            btnAutoTune.Text = text;
            btnAutoTune.Enabled = true;
            Application.DoEvents();
        }

        private void StartAutoTune()
        {
            //insert code here
            // step1: turn off closed loop operation
            // Pgm_mod! byte 0 bit 4  0x10 Lambda control on/off 
            btnAutoTuneIgnition.Visible = false;
            SwitchClosedLoopOperation(false);
            _autoTuning = true;
            gridView2.PaintStyleName = "UltraFlat";
            gridView2.Appearance.Row.Font = new Font("Tahoma", 6, FontStyle.Bold);
            gridView2.Appearance.Row.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            gridView2.Appearance.Row.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            gridView3.PaintStyleName = "UltraFlat";
            gridView3.Appearance.Row.Font = new Font("Tahoma", 6, FontStyle.Bold);
            gridView3.Appearance.Row.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            gridView3.Appearance.Row.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            ResizeGridControls();
            _initiallyShowFuelMap = true;
            _initiallyShowAFRMap = true;
            // what else?

            // we need to merge the adaption maps or at least clear them

            // init
        }

        private void StopAutoTune()
        {
            _autoTuning = false;
            //insert code here
            SwitchClosedLoopOperation(true);
            btnAutoTuneIgnition.Visible = true;
            // stopwatch off?
        }

        private bool ContainsFloatDifferences(float[] arr1, float[] arr2)
        {
            bool retval = false;
            if (arr1.Length != arr2.Length) retval = true;
            else
            {
                for (int i = 0; i < arr1.Length; i++)
                {
                    if (arr1[i] != arr2[i]) retval = true;
                }
            }

            return retval;
        }

        private bool ContainsIntDifferences(int[] arr1, int[] arr2)
        {
            bool retval = false;
            if (arr1.Length != arr2.Length) retval = true;
            else
            {
                for (int i = 0; i < arr1.Length; i++)
                {
                    if (arr1[i] != arr2[i])
                    {
                        retval = true;
                    }
                }
            }

            return retval;
        }

        private bool ContainsByteDifferences(byte[] arr1, byte[] arr2)
        {
            bool retval = false;
            if (arr1.Length != arr2.Length) retval = true;
            else
            {
                for (int i = 0; i < arr1.Length; i++)
                {
                    if (arr1[i] != arr2[i]) retval = true;
                }
            }

            return retval;
        }

        public void UpdateFeedbackAFR(float[] AFRMapInMemory, float[] targetAFRmap, int[] afrcountermap)
        {
            // set data into gridControl2
            // set data into gridControl3
            int numberrows = 16;
            int tablewidth = 16;
            int map_offset = 0;
            bool changed = false;
            m_AFRMapInMemory = CopyFloatData(AFRMapInMemory, m_AFRMapInMemory, out changed);
            if (changed || _initiallyShowAFRMap)
            {
                _initiallyShowAFRMap = false;
                m_afrmapCounter = CopyIntData(afrcountermap, m_afrmapCounter, out changed);
                m_afrtargetmap = CopyFloatData(targetAFRmap, m_afrtargetmap, out changed);
                if (gridControl2.DataSource != null)
                {
                    // only update
                    DataTable dt = (DataTable)gridControl2.DataSource;
                    for (int i = 0; i < numberrows; i++)
                    {
                        //object[] objarr = new object[tablewidth];
                        double b;
                        for (int j = 0; j < (tablewidth); j++)
                        {
                            b = Convert.ToDouble(m_AFRMapInMemory.GetValue(((15 - i) * tablewidth) + j));
                            // check value
                            //objarr.SetValue(b.ToString(), j);
                            double cellvalue = System.Double.Parse(dt.Rows[i][j].ToString());
                            if (cellvalue != b)
                            {
                                if (Math.Abs(cellvalue - b) > 0.01)
                                {
                                    //Console.WriteLine("Updated value in AFR map: " + cellvalue.ToString() + " : " + b.ToString());
                                    float f = (float)b;
                                    dt.Rows[i][j] = f.ToString("F1");
                                }
                            }
                        }
                        //System.Data.DataRow r = dt.NewRow();
                        //r.ItemArray = objarr;
                        //dt.Rows.InsertAt(r, 0);
                    }
                }
                else
                {
                    DataTable dt = new DataTable();
                    if (m_AFRMapInMemory.Length != 0)
                    {
                        dt.Columns.Clear();
                        for (int c = 0; c < tablewidth; c++)
                        {
                            dt.Columns.Add(c.ToString());
                        }
                        for (int i = numberrows - 1; i >= 0; i--)
                        {
                            object[] objarr = new object[tablewidth];
                            double b;
                            for (int j = 0; j < (tablewidth); j++)
                            {
                                b = Convert.ToDouble(m_AFRMapInMemory.GetValue(/*map_offset++*/ (i * tablewidth) + j));
                                float f = (float)b;
                                objarr.SetValue(f.ToString("F1"), j);
                            }

                            //System.Data.DataRow r = dt.NewRow();
                            //r.ItemArray = objarr;
                            //dt.Rows.InsertAt(r, 0);
                            dt.Rows.Add(objarr);
                        }

                        gridControl2.DataSource = dt;

                        /*if (!gridView2.OptionsView.ColumnAutoWidth)
                        {
                            for (int c = 0; c < gridView3.Columns.Count; c++)
                            {
                                gridView2.Columns[c].Width = 40;
                            }
                        }*/
                    }
                }
                //gridView2.RefreshData();
                //Application.DoEvents();
            }
        }

        private int[] CopyIntData(int[] source, int[] target, out bool changed)
        {
            changed = false;

            if (target == null)
            {
                changed = true;
                target = new int[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    target[i] = source[i];
                }
            }
            else
            {
                for (int i = 0; i < source.Length; i++)
                {
                    if (target[i] != source[i])
                    {
                        target[i] = source[i];
                        changed = true;
                    }
                }

            }
            return target;
        }

        private float[] CopyFloatData(float[] source, float[] target, out bool changed)
        {
            changed = false;

            if (target == null)
            {
                changed = true;
                target = new float[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    target[i] = source[i];
                }
            }
            else
            {
                for (int i = 0; i < source.Length; i++)
                {
                    if (target[i] != source[i])
                    {
                        // er moet een minimale afwijking zijn voordat dit gebeurd anders verversen we te snel
                        if (Math.Abs(target[i] - source[i]) >= 0.01)
                        {
                            target[i] = source[i];
                            changed = true;
                        }
                    }
                }

            }
            return target;
        }

        public void RedoGrids()
        {
            //gridView2.RefreshData();
            //gridView3.RefreshData();
            gridControl2.Invalidate();
            gridControl3.Invalidate();
            gridControl6.Invalidate();
            Application.DoEvents();
        }

        public void UpdateMutatedFuelMap(byte[] fuelmap, int[] fuelmapmutations)
        {
            // set data into gridControl3
            int numberrows = 16;
            int tablewidth = 16;
            int map_offset = 0;
            bool changed = false;
            m_fuelmapMutations = CopyIntData(fuelmapmutations, m_fuelmapMutations, out changed);
            if (changed || _initiallyShowFuelMap)
            {
                _initiallyShowFuelMap = false;
                if (gridControl3.DataSource != null)
                {
                    // only update
                    DataTable dt = (DataTable)gridControl3.DataSource;
                    for (int i = 0; i < numberrows; i++)
                    {
                        //object[] objarr = new object[tablewidth];
                        int b;
                        for (int j = 0; j < (tablewidth); j++)
                        {
                            b = (byte)fuelmap.GetValue(((15 - i) * tablewidth) + j);
                            // check value
                            //objarr.SetValue(b.ToString(), j);
                            if (dt.Rows[i][j].ToString() != b.ToString())
                            {
                                //Console.WriteLine("Updated value!!!");
                                //Console.WriteLine("Updated value in fuel map: " + dt.Rows[i][j].ToString() + " : " + b.ToString());
                                dt.Rows[i][j] = b.ToString();
                            }
                        }
                        //System.Data.DataRow r = dt.NewRow();
                        //r.ItemArray = objarr;
                        //dt.Rows.InsertAt(r, 0);
                    }
                }
                else
                {
                    DataTable dt = new DataTable();
                    if (fuelmap.Length != 0)
                    {
                        dt.Columns.Clear();
                        for (int c = 0; c < tablewidth; c++)
                        {
                            dt.Columns.Add(c.ToString());
                        }
                        for (int i = numberrows - 1; i >= 0; i--)
                        {
                            object[] objarr = new object[tablewidth];
                            int b;
                            for (int j = 0; j < (tablewidth); j++)
                            {
                                b = (byte)fuelmap.GetValue(/*map_offset++*/ (i * tablewidth) + j);
                                objarr.SetValue(b.ToString(), j);
                            }

                            //System.Data.DataRow r = dt.NewRow();
                            //r.ItemArray = objarr;
                            //dt.Rows.InsertAt(r, 0);
                            dt.Rows.Add(objarr);
                        }

                        gridControl3.DataSource = dt;

                        /*if (!gridView3.OptionsView.ColumnAutoWidth)
                        {
                            for (int c = 0; c < gridView3.Columns.Count; c++)
                            {
                                gridView3.Columns[c].Width = 40;
                            }
                        }*/
                    }
                }
                //gridView3.RefreshData();
                //Application.DoEvents();
            }
        }

        private int LookUpIndexAxisRPMMap(double value, int[] axisvalues)
        {
            int return_index = -1;
            double min_difference = 10000000;
            for (int t = 0; t < axisvalues.Length; t++)
            {
                int b = (int)axisvalues.GetValue(t);
                //b *= 10;
                double diff = Math.Abs((double)b - value);
                if (min_difference > diff)
                {
                    min_difference = diff;
                    return_index = t;
                }
            }
            return return_index;
        }

        private int LookUpIndexAxisMAPMap(double value, int[] axisvalues, double multiplywith)
        {
            int return_index = -1;
            double min_difference = 10000000;
            for (int t = 0; t < axisvalues.Length; t++)
            {
                double b = Convert.ToDouble((int)axisvalues.GetValue(t));
                b *= multiplywith;
                b -= 100;
                b /= 100;
                double diff = Math.Abs(b - value);
                if (min_difference > diff)
                {
                    min_difference = diff;
                    return_index = t;
                }
            }
            return return_index;
        }



        private void gridView2_CustomDrawCell(object sender, DevExpress.XtraGrid.Views.Base.RowCellCustomDrawEventArgs e)
        {
            // feedback AFR custom draw
            // calculate difference and base color scheme on that
            try
            {
                int mapidx = LookUpIndexAxisMAPMap(_lastInletPressure.Value, m_fuelxaxis, multiply);

                int rpmidx = LookUpIndexAxisRPMMap(_lastRPM.Value, m_fuelyaxis);
                if (e.Column.AbsoluteIndex == mapidx && (15 - e.RowHandle) == rpmidx)
                {
                    //SolidBrush sb = new SolidBrush(Color.Yellow);
                    //e.Graphics.FillRectangle(sb, e.Bounds);
                    //sb.Dispose();
                    Pen p = new Pen(Brushes.Black, 2);
                    e.Graphics.DrawRectangle(p, e.Bounds.X + 1, e.Bounds.Y + 1, e.Bounds.Width - 2, e.Bounds.Height - 2);
                    p.Dispose();
                    //gridView2.RefreshRowCell(e.RowHandle, e.Column);


                }
                else if (m_afrmapCounter != null)
                {
                    int counter = (int)m_afrmapCounter.GetValue(((15 - e.RowHandle) * 16) + e.Column.AbsoluteIndex);
                    if (counter > 0)
                    {
                        // highlight
                        double realvalue = Double.Parse(e.CellValue.ToString());
                        if (realvalue == 0)
                        {
                            e.DisplayText = "";
                            return;
                        }
                        if (_showAsLambda)
                        {
                            double lambdaValue = realvalue / 14.7;
                            e.DisplayText = lambdaValue.ToString("F2");
                        }
                        double targetvalue = Convert.ToDouble(m_afrtargetmap.GetValue(((15 - e.RowHandle) * 16) + e.Column.AbsoluteIndex));
                        double afrdiff = realvalue - targetvalue;
                        bool isBlue = false;
                        if (afrdiff > 0)
                        {
                            // mixture is lean
                            isBlue = true;
                        }
                        afrdiff *= 40;
                        //b /= m_MaxValueInTable;
                        int green = 255;
                        int blue = 255;
                        Color c = Color.White;
                        //if (afrdiff < 0) afrdiff = 0;
                        if (afrdiff > 255) afrdiff = 255;
                        if (Double.IsNaN(afrdiff)) afrdiff = 0;
                        if (isBlue)
                        {
                            afrdiff *= 2;
                            if (afrdiff > 255) afrdiff = 255;
                            blue = 255 - (int)afrdiff;
                            green = 255;

                            c = Color.FromArgb((int)afrdiff, (int)afrdiff, green, blue);

                        }
                        else
                        {
                            //if (afrdiff < 0) afrdiff = 0; ??
                            //afrdiff *= 4;
                            c = Color.FromArgb((int)Math.Abs(afrdiff), Color.Red);
                        }
                        // If the current cell is the active one (current load/rpm) then highlight in yellow
                        //_lastInletPressure
                        //_lastRPM


                        SolidBrush sb = new SolidBrush(c);
                        e.Graphics.FillRectangle(sb, e.Bounds);
                        sb.Dispose();
                    }
                    else
                    {
                        if (e.DisplayText != "")
                        {
                            e.DisplayText = "";
                        }
                        return;
                    }
                    // If the current cell is the active one (current load/rpm) then highlight in yellow
                    //_lastInletPressure
                    //_lastRPM
                }

            }
            catch (Exception E)
            {
                Console.WriteLine("gridView2_CustomDrawCell: " + E.Message);
            }
        }

        private void gridView3_CustomDrawCell(object sender, DevExpress.XtraGrid.Views.Base.RowCellCustomDrawEventArgs e)
        {
            // mutated fuelmap custom draw
            // highlight mutated cells
            try
            {
                int mapidx = LookUpIndexAxisMAPMap(_lastInletPressure.Value, m_fuelxaxis, multiply);
                int rpmidx = LookUpIndexAxisRPMMap(_lastRPM.Value, m_fuelyaxis);
                if (e.Column.AbsoluteIndex == mapidx && (15 - e.RowHandle) == rpmidx)
                {
                    //SolidBrush sby = new SolidBrush(Color.Yellow);
                    //e.Graphics.FillRectangle(sby, e.Bounds);
                    //sby.Dispose();
                    Pen p = new Pen(Brushes.Black, 2);
                    e.Graphics.DrawRectangle(p, e.Bounds.X + 1, e.Bounds.Y + 1, e.Bounds.Width - 2, e.Bounds.Height - 2);
                    p.Dispose();


                }
                else if (m_fuelmapMutations != null)
                {
                    int counter = (int)m_fuelmapMutations.GetValue(/*map_offset++*/ ((15 - e.RowHandle) * 16) + e.Column.AbsoluteIndex);
                    if (counter > 0)
                    {
                        // highlight
                        SolidBrush sb = new SolidBrush(Color.Orange);
                        e.Graphics.FillRectangle(sb, e.Bounds);
                        sb.Dispose();

                    }
                }
                if (e.CellValue != null)
                {
                    float value = (float)Convert.ToInt32(e.CellValue);
                    value *= 0.00390625F;
                    value += 0.5F;
                    if (e.DisplayText != value.ToString("F2"))
                    {
                        e.DisplayText = value.ToString("F2");
                    }
                    // convert to real correction factor

                }


            }
            catch (Exception E)
            {
                Console.WriteLine("gridView3_CustomDrawCell: " + E.Message);
            }

        }

        private void fadeTimer_Tick(object sender, EventArgs e)
        {
            Color c = Color.Red;
            if (measurementKnockDiffCyl1.DigitColor.R > 0)
            {
                c = measurementKnockDiffCyl1.DigitColor;
                measurementKnockDiffCyl1.DigitColor = Color.FromArgb(c.R - 1, c.G, c.B);
            }
            if (measurementKnockDiffCyl2.DigitColor.R > 0)
            {
                c = measurementKnockDiffCyl2.DigitColor;
                measurementKnockDiffCyl2.DigitColor = Color.FromArgb(c.R - 1, c.G, c.B);
            }
            if (measurementKnockDiffCyl3.DigitColor.R > 0)
            {
                c = measurementKnockDiffCyl3.DigitColor;
                measurementKnockDiffCyl3.DigitColor = Color.FromArgb(c.R - 1, c.G, c.B);
            }
            if (measurementKnockDiffCyl4.DigitColor.R > 0)
            {
                c = measurementKnockDiffCyl4.DigitColor;
                measurementKnockDiffCyl4.DigitColor = Color.FromArgb(c.R - 1, c.G, c.B);
            }
        }

        public bool IsEnrichmentActive()
        {
            bool retval = false;
            // check all enrichment factors, if they are not within the predefined boundaries, autotune is not allowed
            int max_enrichmentValueAllowed = _EnrichmentFilter;
            if (_last_Lacc_mangd_cyl1 > max_enrichmentValueAllowed) retval = true;
            if (_last_Lacc_mangd_cyl2 > max_enrichmentValueAllowed) retval = true;
            if (_last_Lacc_mangd_cyl3 > max_enrichmentValueAllowed) retval = true;
            if (_last_Lacc_mangd_cyl4 > max_enrichmentValueAllowed) retval = true;
            if (_last_Acc_mangd_cyl1 > max_enrichmentValueAllowed) retval = true;
            if (_last_Acc_mangd_cyl2 > max_enrichmentValueAllowed) retval = true;
            if (_last_Acc_mangd_cyl3 > max_enrichmentValueAllowed) retval = true;
            if (_last_Acc_mangd_cyl4 > max_enrichmentValueAllowed) retval = true;
            if (_last_Lret_mangd_cyl1 > max_enrichmentValueAllowed) retval = true;
            if (_last_Lret_mangd_cyl2 > max_enrichmentValueAllowed) retval = true;
            if (_last_Lret_mangd_cyl3 > max_enrichmentValueAllowed) retval = true;
            if (_last_Lret_mangd_cyl4 > max_enrichmentValueAllowed) retval = true;
            if (_last_Ret_mangd_cyl1 > max_enrichmentValueAllowed) retval = true;
            if (_last_Ret_mangd_cyl2 > max_enrichmentValueAllowed) retval = true;
            if (_last_Ret_mangd_cyl3 > max_enrichmentValueAllowed) retval = true;
            if (_last_Ret_mangd_cyl4 > max_enrichmentValueAllowed) retval = true;
            return retval;
        }

        private void gridControl3_Resize(object sender, EventArgs e)
        {
            ResizeGridControls();
        }

        private void ResizeGridControls()
        {
            // check font settings depeding on size of the control (font should increase when size increases for better readability)
            gridView2.RowHeight = gridControl2.Height / 18;
            gridView3.RowHeight = gridControl3.Height / 18;
            gridView6.RowHeight = gridControl3.Height / 18;
            gridView2.Appearance.Row.Font = new Font("Tahoma", gridView2.RowHeight / 3, FontStyle.Bold);
            gridView3.Appearance.Row.Font = new Font("Tahoma", gridView3.RowHeight / 3, FontStyle.Bold);
            gridView6.Appearance.Row.Font = new Font("Tahoma", gridView6.RowHeight / 3, FontStyle.Bold);
            /*
            if (gridControl2.Size.Height < 400)
            {
                gridView2.Appearance.Row.Font = new Font("Tahoma", 6);
                gridView3.Appearance.Row.Font = new Font("Tahoma", 6);
            }
            else if (gridControl2.Size.Height >= 400 && gridControl2.Size.Height < 500)
            {
                gridView2.Appearance.Row.Font = new Font("Tahoma", 7);
                gridView3.Appearance.Row.Font = new Font("Tahoma", 7);
            }
            else if (gridControl2.Size.Height >= 500 && gridControl2.Size.Height < 600)
            {
                gridView2.Appearance.Row.Font = new Font("Tahoma", 8);
                gridView3.Appearance.Row.Font = new Font("Tahoma", 8);
            }
            else if (gridControl2.Size.Height >= 600 && gridControl2.Size.Height < 700)
            {
                gridView2.Appearance.Row.Font = new Font("Tahoma", 9);
                gridView3.Appearance.Row.Font = new Font("Tahoma", 9);
            }
            else if (gridControl2.Size.Height >= 700 && gridControl2.Size.Height < 800)
            {
                gridView2.Appearance.Row.Font = new Font("Tahoma", 9);
                gridView3.Appearance.Row.Font = new Font("Tahoma", 9);
            }
            else if (gridControl2.Size.Height >= 800)
            {
                gridView2.Appearance.Row.Font = new Font("Tahoma", 16);
                gridView3.Appearance.Row.Font = new Font("Tahoma", 16);
            }*/
        }

        private void SetValueInFreeLoggingList(string symbol, float value)
        {
            SetDataTableFreeLogging();
            // update the list
            DataTable dt = (DataTable)gridControl4.DataSource;
            bool _fnd = false;
            foreach (DataRow dr in dt.Rows)
            {
                if (dr["SYMBOLNAME"].ToString() == symbol)
                {
                    _fnd = true;
                    double dval = Convert.ToDouble(value);
                    if (dval < Convert.ToDouble(dr["MINVALUE"])) dr["MINVALUE"] = dval;
                    if (dval > Convert.ToDouble(dr["MAXVALUE"])) dr["MAXVALUE"] = dval;
                    if (dval > Convert.ToDouble(dr["PEAK"])) dr["PEAK"] = dval;
                    dr["VALUE"] = dval;
                }
            }
            if (!_fnd)
            {
                /*if (symbol == m_appSettings.Adc1channelname ||
                    symbol == m_appSettings.Adc2channelname ||
                    symbol == m_appSettings.Adc3channelname ||
                    symbol == m_appSettings.Adc4channelname ||
                    symbol == m_appSettings.Adc5channelname ||
                    symbol == m_appSettings.Thermochannelname)*/
                // <GS-04042011> removed because symbols should always be added if not present
                {
                    dt.Rows.Add(symbol, Convert.ToDouble(value), Convert.ToDouble(value), Convert.ToDouble(value), Convert.ToDouble(value));
                }
                // we have to do something for the AD channel stuff and the thermo input
                // that go with the Multiadapter

                // add it to the list
                //<GS-27072010> dt.Rows.Add(symbol, Convert.ToDouble(value), Convert.ToDouble(value), Convert.ToDouble(value), Convert.ToDouble(value)); 
                // <GS-10062010>
                // SizeColumns();

            }
            /*foreach (Control c in tableLayoutPanel6.Controls)
            {
                if (c is Measurement)
                {
                    Measurement m = (Measurement)c;
                    if (m.SymbolToDisplay == symbol)
                    {
                        m.Value = value;
                        break;
                    }
                }
            }*/
        }

        private void SetDataTableFreeLogging()
        {
            if (gridControl4.DataSource == null)
            {
                System.Data.DataTable dtnew = new System.Data.DataTable();
                dtnew.TableName = "RTSymbols";
                dtnew.Columns.Add("SYMBOLNAME");
                dtnew.Columns.Add("MINVALUE", Type.GetType("System.Double")); // autosense max and min!
                dtnew.Columns.Add("MAXVALUE", Type.GetType("System.Double"));
                dtnew.Columns.Add("VALUE", Type.GetType("System.Double"));
                dtnew.Columns.Add("PEAK", Type.GetType("System.Double"));
                gridControl4.DataSource = dtnew;
            }
            // add all the system variables

        }

        private void gridView4_CustomDrawCell(object sender, DevExpress.XtraGrid.Views.Base.RowCellCustomDrawEventArgs e)
        {
            if (e.Column.Name == gcrtvalue.Name)
            {
                // get maximum and minumum  value
                try
                {

                    DataRow dr = gridView4.GetDataRow(e.RowHandle);
                    if (dr != null && e.CellValue != null)
                    {
                        string symName = string.Empty;
                        double maximum = Convert.ToDouble(dr["MAXVALUE"]);
                        double minimum = Convert.ToDouble(dr["MINVALUE"]);
                        double actualvalue = Convert.ToDouble(dr["VALUE"]);
                        double range = maximum - minimum;
                        if (dr["SYMBOLNAME"] != DBNull.Value) symName = dr["SYMBOLNAME"].ToString();
                        if (symName == "Pgm_status")
                        {
                            UInt64 itemp = Convert.ToUInt64(actualvalue);
                            e.DisplayText = itemp.ToString("X16");
                        }
                        else
                        {
                            float fval = (float)actualvalue;
                            e.DisplayText = fval.ToString("F2");

                            double percentage = (actualvalue - minimum) / range;
                            if (percentage < 0) percentage = 0;
                            if (percentage > 1) percentage = 1;
                            double xwidth = percentage * (double)(e.Bounds.Width - 2);
                            if (xwidth > 0)
                            {
                                System.Drawing.Rectangle rect = new System.Drawing.Rectangle(e.Bounds.X - 1, e.Bounds.Y, e.Bounds.Width + 1, e.Bounds.Height);
                                Brush brush = new System.Drawing.Drawing2D.LinearGradientBrush(rect, Color.LightGreen, Color.OrangeRed, System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
                                e.Graphics.FillRectangle(brush, e.Bounds.X + 1, e.Bounds.Y + 1, (float)xwidth, e.Bounds.Height - 2);
                            }
                        }
                        //percentage *= 100;
                        //e.DisplayText = percentage.ToString("F0") + @" %";
                    }
                }
                catch (Exception E)
                {
                    //Console.WriteLine(E.Message);
                }
            }
            else if (e.Column.Name == gcrtminvalue.Name || e.Column.Name == gcrtmaxvalue.Name || e.Column.Name == gcrtpeak.Name)
            {
                try
                {
                    DataRow dr = gridView4.GetDataRow(e.RowHandle);
                    if (dr != null && e.CellValue != null)
                    {
                        string symName = string.Empty;
                        if (dr["SYMBOLNAME"] != DBNull.Value) symName = dr["SYMBOLNAME"].ToString();
                        if (symName == "Pgm_status")
                        {
                            if (e.Column.Name == gcrtminvalue.Name)
                            {
                                float fval = (float)Convert.ToDouble(dr["MINVALUE"]);
                                UInt64 itemp = Convert.ToUInt64(fval);
                                e.DisplayText = itemp.ToString("X16");
                            }
                            else if (e.Column.Name == gcrtmaxvalue.Name)
                            {
                                float fval = (float)Convert.ToDouble(dr["MAXVALUE"]);
                                UInt64 itemp = Convert.ToUInt64(fval);
                                e.DisplayText = itemp.ToString("X16");
                            }
                            else if (e.Column.Name == gcrtpeak.Name)
                            {
                                float fval = (float)Convert.ToDouble(dr["PEAK"]);
                                UInt64 itemp = Convert.ToUInt64(fval);
                                e.DisplayText = itemp.ToString("X16");
                            }
                        }
                        else
                        {
                            if (e.Column.Name == gcrtminvalue.Name)
                            {
                                float fval = (float)Convert.ToDouble(dr["MINVALUE"]);
                                e.DisplayText = fval.ToString("F2");
                            }
                            else if (e.Column.Name == gcrtmaxvalue.Name)
                            {
                                float fval = (float)Convert.ToDouble(dr["MAXVALUE"]);
                                e.DisplayText = fval.ToString("F2");
                            }
                            else if (e.Column.Name == gcrtpeak.Name)
                            {
                                float fval = (float)Convert.ToDouble(dr["PEAK"]);
                                e.DisplayText = fval.ToString("F2");
                            }
                        }

                    }
                }
                catch (Exception floatE)
                {

                }
            }
        }

        private void CastRemoveFromListEvent(string symbol)
        {
            if (symbol != "")
            {
                if (onRemoveSymbolFromMonitorList != null)
                {
                    onRemoveSymbolFromMonitorList(this, new MapDisplayRequestEventArgs(symbol, 1, 0, false));
                }
            }
        }

        private void CastOpenLogFileEvent(string logfilename)
        {
            if (onOpenLogFileRequest != null)
            {
                onOpenLogFileRequest(this, new OpenLogFileRequestEventArgs(logfilename));
            }
        }

        private void CastAddToListEvent(string symbol, double factor, double offset, bool usecorrection)
        {
            if (symbol != "")
            {
                if (onAddSymbolToMonitorList != null)
                {
                    onAddSymbolToMonitorList(this, new MapDisplayRequestEventArgs(symbol, factor, offset, usecorrection));
                }
            }
        }

        private void addSymbolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // show a list of symbols that the user can add (only 1 and 2 byte values in where SRAM address > 0)
            if (m_RealtimeSymbolCollection != null)
            {
                // show the selection screen with a lookupedit control
                // if ok, cast an event to have it added to the realtime list
                frmRealtimeSymbolSelect symsel = new frmRealtimeSymbolSelect();
                symsel.SetCollection(m_RealtimeSymbolCollection);
                if (symsel.ShowDialog() == DialogResult.OK)
                {
                    //<GS-27072010> todo: add code to save user data here!
                    // cast event
                    CastAddToListEvent(symsel.GetSelectedSymbol(), symsel.GetSelectedCorrectionFactor(), symsel.GetSelectedCorrectionOffset(), true);
                    // and add it to the list directly (don't wait for the symbol to be present in the data stream)
                    AddSymbolToDataTable(symsel.GetSelectedSymbol(), GetSymbolLength(symsel.GetSelectedSymbol()));

                }
            }

        }

        private int GetSymbolLength(string symbolname)
        {
            foreach (SymbolHelper sh in m_RealtimeSymbolCollection)
            {
                if (sh.Varname == symbolname) return sh.Length;
            }
            return 2;
        }

        private void AddSymbolToDataTable(string symbolname, int len)
        {
            SetDataTableFreeLogging();
            System.Data.DataTable dt = (DataTable)gridControl4.DataSource;
            bool symFound = false;
            foreach (DataRow dr in dt.Rows)
            {
                if (dr["SYMBOLNAME"] != DBNull.Value)
                {
                    if (dr["SYMBOLNAME"].ToString() == symbolname)
                    {
                        symFound = true;
                        break;
                    }
                }
                /*
                dtnew.Columns.Add("SYMBOLNAME");
                dtnew.Columns.Add("MINVALUE", Type.GetType("System.Double")); // autosense max and min!
                dtnew.Columns.Add("MAXVALUE", Type.GetType("System.Double"));
                dtnew.Columns.Add("VALUE", Type.GetType("System.Double"));
                dtnew.Columns.Add("PEAK", Type.GetType("System.Double"));                 * */
            }
            if (!symFound)
            {
                int peak = 65535;
                if (len == 1) peak = 255;
                dt.Rows.Add(symbolname, 0, peak, 0, 0);
                gridControl4.DataSource = dt;
            }
        }

        private void removeSymbolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // which symbol is selected?
            object odr = gridView4.GetFocusedRow();
            if (odr is DataRowView)
            {
                DataRowView drv = (DataRowView)odr;
                DataRow dr = drv.Row;
                if (dr != null)
                {
                    CastRemoveFromListEvent(dr["SYMBOLNAME"].ToString());
                    gridView4.DeleteRow(gridView4.FocusedRowHandle);
                }
            }
        }

        private void resetSymbolPeakToolStripMenuItem_Click(object sender, EventArgs e)
        {
            object odr = gridView4.GetFocusedRow();
            if (odr is DataRowView)
            {
                DataRowView drv = (DataRowView)odr;
                DataRow dr = drv.Row;
                if (dr != null)
                {
                    dr["PEAK"] = 0;
                }
            }
        }

        Measurement[] msrArray = new Measurement[1];

        private void SizeColumns()
        {

            /*tableLayoutPanel6.Visible = false;
            try
            {

                while (tableLayoutPanel6.ColumnStyles.Count < tableLayoutPanel6.ColumnCount)
                {
                    tableLayoutPanel6.ColumnStyles.Add(new ColumnStyle(SizeType.Percent));
                }
                while (tableLayoutPanel6.ColumnStyles.Count > tableLayoutPanel6.ColumnCount)
                {
                    tableLayoutPanel6.ColumnStyles.RemoveAt(tableLayoutPanel6.ColumnStyles.Count - 1);
                }
                for (int i = 0; i < tableLayoutPanel6.ColumnCount; i++)
                {
                    tableLayoutPanel6.ColumnStyles[i].SizeType = SizeType.Percent;
                    tableLayoutPanel6.ColumnStyles[i].Width = 100F / (float)tableLayoutPanel6.ColumnCount;
                }
            }
            catch (Exception E)
            {
                Console.WriteLine("Failed to size: " + E.Message);
            }
            LoadExtraMeasurements();
            tableLayoutPanel6.Visible = true;*/

        }

        private void LoadExtraMeasurements()
        {
            // first remove buttons from tableLayout
            /*DataTable dt = (DataTable)gridControl4.DataSource;
            
            SymbolCollection sc = new SymbolCollection();
            if (dt != null)
            {
                foreach (DataRow dr in dt.Rows)
                {
                    SymbolHelper sh = new SymbolHelper();
                    sh.Varname = dr["SYMBOLNAME"].ToString();
                    sc.Add(sh);
                }
            }
            

            try
            {
                if (msrArray != null)
                {
                    for (int i = 0; i < msrArray.Length; i++)
                    {

                        if (msrArray[i] != null)
                        {
                            if (!msrArray[i].IsDisposed) msrArray[i].Dispose();
                        }
                    }
                }
                try
                {
                    msrArray = new Measurement[tableLayoutPanel6.ColumnCount];
                    for (int i = 0; i < msrArray.Length; i++)
                    {
                        msrArray[i] = new Measurement();
                        msrArray[i].MeasurementText = i.ToString();
                        // add control to designated panel
                        msrArray[i].Dock = DockStyle.Fill;
                        // hook it to a symbol, the enumerated next on in free logging mode probably ???
                        msrArray[i].Symbols = sc;
                        Console.WriteLine("seen sc.count: " + sc.Count.ToString());
                        if (sc.Count > i)
                        {
                            msrArray[i].SymbolToDisplay = sc[i].Varname;
                            switch (sc[i].Varname)
                            {
                                case "P_medel":
                                    msrArray[i].NumberOfDecimals = 2;

                                    break;
                                    
                            }
                        }
                        tableLayoutPanel6.Controls.Add(msrArray[i]);
                    }
                }
                catch (Exception E)
                {
                    Console.WriteLine(E.Message);
                }
            }
            catch (Exception loadMeasurementsE)
            {
                Console.WriteLine(loadMeasurementsE.Message);
            }*/
        }

        private bool _EnableAdvancedMode = false;

        public bool EnableAdvancedMode
        {
            get { return _EnableAdvancedMode; }
            set
            {
                _EnableAdvancedMode = value;
                SetStandardAdvancedMode();
            }
        }

        private void SetStandardAdvancedMode()
        {
            if (_EnableAdvancedMode)
            {
                // show tabpages/buttons
                xtraTabPage6.PageVisible = true;
                xtraTabPage6.PageEnabled = true;
                xtraTabPage7.PageVisible = true;
                xtraTabPage7.PageEnabled = true;
                tabAutoTune.PageEnabled = true;
                tabAutoTune.PageVisible = true;
                tabAutoTuneIgnition.PageEnabled = true;
                tabAutoTuneIgnition.PageVisible = true;
                btnAutoTune.Visible = true;
                btnAutoTuneIgnition.Visible = true;
                btnMapEdit.Visible = true;
            }
            else
            {
                // hide tabpages/buttons
                xtraTabPage6.PageVisible = false;
                xtraTabPage6.PageEnabled = false;
                xtraTabPage7.PageVisible = false;
                xtraTabPage7.PageEnabled = false;
                tabAutoTune.PageEnabled = false;
                tabAutoTune.PageVisible = false;
                tabAutoTuneIgnition.PageEnabled = false;
                tabAutoTuneIgnition.PageVisible = false;

                btnAutoTune.Visible = false;
                btnAutoTuneIgnition.Visible = false;
                btnMapEdit.Visible = false;

            }
        }


        private void addGaugeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /*if (tableLayoutPanel6.ColumnCount <= 10)
            {
                tableLayoutPanel6.ColumnCount++;
                SizeColumns();
            }*/
        }

        private void removeGaugeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /*if (tableLayoutPanel6.ColumnCount > 0)
            {
                tableLayoutPanel6.ColumnCount--;
                // resize columns
                SizeColumns();
            }*/

        }


        public void WriteLogMarker()
        {
            // only if currently logging
            if (m_loggingActive)
            {
                m_WriteLogMarker = true;
            }

        }

        public void UpdateProgramModeButtons(byte[] pgm_mode)
        {
            // update all the buttons checked/unchecked

            /*Console.WriteLine("Program mode settings");
            foreach(byte b in pgm_mode)
            {
                Console.Write(b.ToString("X2") + " ");
            }
            Console.WriteLine();*/

            if (pgm_mode.Length > 3)
            {
                if ((pgm_mode[0] & 0x10) > 0)
                {
                    // closed loop is set to ON
                    btnClosedLoop.ImageIndex = 2;
                    btnClosedLoopFuelTab.ImageIndex = 2;
                }
                else
                {
                    btnClosedLoop.ImageIndex = -1;
                    btnClosedLoopFuelTab.ImageIndex = -1;
                }
                //btnFuelcut = Fuelcut in engine braking BYTE 1 MASK 0x04
                if ((pgm_mode[1] & 0x04) > 0)
                {
                    btnFuelcut.ImageIndex = 2;
                }
                else
                {
                    btnFuelcut.ImageIndex = -1;
                }
                //btnPurgeControl = Purge control BYTE 2 MASK 0x20
                if ((pgm_mode[2] & 0x20) > 0)
                {
                    btnPurgeControl.ImageIndex = 2;
                }
                else
                {
                    btnPurgeControl.ImageIndex = -1;
                }
                //SetValueInProgramMode(0, 0x40, btnIdleControl.ImageIndex != 2);
                if ((pgm_mode[0] & 0x40) > 0) btnIdleControl.ImageIndex = 2;
                else btnIdleControl.ImageIndex = -1;
                //SetValueInProgramMode(2, 0x80, btnLambdaControlInIdle.ImageIndex != 2);
                if ((pgm_mode[2] & 0x80) > 0)
                {
                    btnLambdaControlInIdle.ImageIndex = 2;
                    btnIdleClosedLoopFuelTab.ImageIndex = 2;
                }
                else
                {
                    btnLambdaControlInIdle.ImageIndex = -1;
                    btnIdleClosedLoopFuelTab.ImageIndex = -1;
                }
                //SetValueInProgramMode(3, 0x10, btnAPCControl.ImageIndex != 2);
                if ((pgm_mode[3] & 0x10) > 0)
                {
                    btnAPCControl.ImageIndex = 2;
                    btnAPCControlOnBoostTab.ImageIndex = 2;
                }
                else
                {
                    btnAPCControl.ImageIndex = -1;
                    btnAPCControlOnBoostTab.ImageIndex = -1;
                }
                //SetValueInProgramMode(0, 0x01, btnAfterstartEnrichment.ImageIndex != 2);
                if ((pgm_mode[0] & 0x01) > 0) btnAfterstartEnrichment.ImageIndex = 2;
                else btnAfterstartEnrichment.ImageIndex = -1;
                //SetValueInProgramMode(0, 0x80, btnCrankingEnrichment.ImageIndex != 2);
                if ((pgm_mode[0] & 0x80) > 0) btnCrankingEnrichment.ImageIndex = 2;
                else btnCrankingEnrichment.ImageIndex = -1;
                //SetValueInProgramMode(0, 0x02, btnWOTEnrichment.ImageIndex != 2);
                if ((pgm_mode[0] & 0x02) > 0) btnWOTEnrichment.ImageIndex = 2;
                else btnWOTEnrichment.ImageIndex = -1;
                //SetValueInProgramMode(1, 0x10, btnAccelerationEnrichment.ImageIndex != 2);
                if ((pgm_mode[1] & 0x10) > 0) btnAccelerationEnrichment.ImageIndex = 2;
                else btnAccelerationEnrichment.ImageIndex = -1;
                //SetValueInProgramMode(1, 0x20, btnDecelerationEnleanment.ImageIndex != 2);
                if ((pgm_mode[1] & 0x20) > 0) btnDecelerationEnleanment.ImageIndex = 2;
                else btnDecelerationEnleanment.ImageIndex = -1;
                //SetValueInProgramMode(3, 0x40, btnGlobalAdaption.ImageIndex != 2);
                if ((pgm_mode[3] & 0x40) > 0) btnGlobalAdaption.ImageIndex = 2;
                else btnGlobalAdaption.ImageIndex = -1;
                //SetValueInProgramMode(0, 0x20, btnAdaptivity.ImageIndex != 2);
                if ((pgm_mode[0] & 0x20) > 0) btnAdaptivity.ImageIndex = 2;
                else btnAdaptivity.ImageIndex = -1;
                //SetValueInProgramMode(2, 0x40, btnAdaptionOfIdleControl.ImageIndex != 2);
                if ((pgm_mode[2] & 0x40) > 0) btnAdaptionOfIdleControl.ImageIndex = 2;
                else btnAdaptionOfIdleControl.ImageIndex = -1;
                //SetValueInProgramMode(4, 0x20, btnKnockControl.ImageIndex != 2);
                if (pgm_mode.Length > 4)
                {
                    if ((pgm_mode[4] & 0x20) > 0)
                    {
                        btnKnockControl.ImageIndex = -1;
                        btnKnockControlKnockTab.ImageIndex = -1;
                    }
                    else
                    {
                        btnKnockControl.ImageIndex = 2;
                        btnKnockControlKnockTab.ImageIndex = 2;
                        if (!btnAutoTuneIgnition.Enabled)
                        {
                            btnAutoTuneIgnition.Enabled = true; // ONLY if knock detection is active
                        }
                    }
                }
                else
                {
                    btnKnockControl.Enabled = false;
                    btnKnockControl.Visible = false;
                    btnKnockControlKnockTab.Enabled = false;
                    btnKnockControlKnockTab.Visible = false;
                }
            }
        }




        private void SetValueInProgramMode(int byteNumber, byte Mask, bool Enable)
        {
            // <GS-14042010> implement a sturdy function for this, it should write, read and redisplay Pgm_mod! in sram
            //Also, on startup, the initial values should be shown (general refresh of the Pgm_mod! variables)
            // we can only cast an event for request!
            if (onProgramModeChange != null)
            {
                onProgramModeChange(this, new ProgramModeEventArgs(byteNumber, Mask, Enable));
            }
        }

        private void btnClosedLoop_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(0, 0x10, btnClosedLoop.ImageIndex != 2);
        }

        private void btnPurgeControl_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(2, 0x20, btnPurgeControl.ImageIndex != 2);
        }

        private void btnFuelcut_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(1, 0x04, btnFuelcut.ImageIndex != 2);
        }

        private void btnIdleControl_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(0, 0x40, btnIdleControl.ImageIndex != 2);
        }

        private void btnLambdaControlInIdle_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(2, 0x80, btnLambdaControlInIdle.ImageIndex != 2);
        }

        private void btnAPCControl_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(3, 0x10, btnAPCControl.ImageIndex != 2);
        }

        private void btnAfterstartEnrichment_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(0, 0x01, btnAfterstartEnrichment.ImageIndex != 2);
        }

        private void btnCrankingEnrichment_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(0, 0x80, btnCrankingEnrichment.ImageIndex != 2);
        }

        private void btnWOTEnrichment_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(0, 0x02, btnWOTEnrichment.ImageIndex != 2);
        }

        private void btnAccelerationEnrichment_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(1, 0x10, btnAccelerationEnrichment.ImageIndex != 2);
        }

        private void btnDecelerationEnleanment_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(1, 0x20, btnDecelerationEnleanment.ImageIndex != 2);
        }

        private void btnGlobalAdaption_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(3, 0x40, btnGlobalAdaption.ImageIndex != 2);
        }

        private void btnAdaptivity_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(0, 0x20, btnAdaptivity.ImageIndex != 2);
        }

        private void btnAdaptionOfIdleControl_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(2, 0x40, btnAdaptionOfIdleControl.ImageIndex != 2);
        }

        private void btnKnockControl_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(4, 0x20, btnKnockControl.ImageIndex == 2);
        }

        private void btnClosedLoopFuelTab_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(0, 0x10, btnClosedLoopFuelTab.ImageIndex != 2);
        }

        private void btnIdleClosedLoopFuelTab_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(2, 0x80, btnIdleClosedLoopFuelTab.ImageIndex != 2);
        }

        private void btnAPCControlOnBoostTab_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(3, 0x10, btnAPCControlOnBoostTab.ImageIndex != 2);
        }

        private void btnKnockControlKnockTab_Click(object sender, EventArgs e)
        {
            SetValueInProgramMode(4, 0x20, btnKnockControlKnockTab.ImageIndex == 2);
        }

        public void RefreshOnlineGraph()
        {
            if (type == RealtimeMonitoringType.OnlineGraph)
            {
                int m_knock = 0;
                if (ledToggler3.Checked) m_knock = 1;
                onlineGraphControl1.ForceRepaint(m_knock); //  Show indicate knock
            }
        }

        public void SetNumberOfSymbolsToWatch(int numberOfWatchedSymbols)
        {
            /*            tableLayoutPanel6.ColumnCount = numberOfWatchedSymbols;
                        //SizeColumns();
                        Console.WriteLine("Columns sized");*/
        }

        private double GetCorrectionFactor(string symbolname)
        {
            double retval = 1;
            foreach (SymbolHelper sh in m_watchListCollection)
            {
                if (sh.Varname == symbolname)
                {
                    retval = sh.UserCorrectionFactor;
                    break;
                }
            }
            return retval;
        }

        private double GetCorrectionOffset(string symbolname)
        {
            double retval = 0;
            foreach (SymbolHelper sh in m_watchListCollection)
            {
                if (sh.Varname == symbolname)
                {
                    retval = sh.UserCorrectionOffset;
                    break;
                }
            }
            return retval;
        }

        private void editSymbolPropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // get the currently selected symbol
            object o = gridView4.GetFocusedRow();
            if (o != null)
            {
                if (o is DataRowView)
                {
                    DataRowView dr = (DataRowView)o;
                    frmRealtimeSymbolSelect symsel = new frmRealtimeSymbolSelect();
                    symsel.SetCollection(m_RealtimeSymbolCollection);
                    symsel.SetCorrectionFactor(GetCorrectionFactor(dr["SYMBOLNAME"].ToString()));
                    symsel.SetCorrectionOffset(GetCorrectionOffset(dr["SYMBOLNAME"].ToString()));
                    symsel.SetSelectedSymbol(dr["SYMBOLNAME"].ToString());
                    if (symsel.ShowDialog() == DialogResult.OK)
                    {
                        // first remove it from the list
                        CastRemoveFromListEvent(dr.Row["SYMBOLNAME"].ToString());
                        // now alter the symbols properties
                        CastAddToListEvent(symsel.GetSelectedSymbol(), symsel.GetSelectedCorrectionFactor(), symsel.GetSelectedCorrectionOffset(), true);


                    }

                }

            }

        }

        private SymbolCollection m_watchListCollection = new SymbolCollection();

        public void SetRealtimeSymbollist(SymbolCollection m_RealtimeUserSymbols)
        {
            m_watchListCollection = m_RealtimeUserSymbols;
            foreach (SymbolHelper sh in m_watchListCollection)
            {
                AddSymbolToDataTable(sh.Varname, sh.Length);
            }
        }

        private void simpleButton2_Click(object sender, EventArgs e)
        {
            // save layout
            //m_watchListCollection
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Layout files|*.rt2";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                SaveUserDefinedRealtimeSymbols(sfd.FileName);
            }
        }

        private void simpleButton3_Click(object sender, EventArgs e)
        {
            // load layout
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Layout files|*.rt2";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                gridControl4.DataSource = null;
                LoadUserDefinedRealtimeSymbols(ofd.FileName);
            }
        }

        private double ConvertToDouble(string v)
        {
            double d = 0;
            if (v == "") return d;
            string vs = "";
            vs = v.Replace(System.Threading.Thread.CurrentThread.CurrentCulture.NumberFormat.NumberGroupSeparator, System.Threading.Thread.CurrentThread.CurrentCulture.NumberFormat.NumberDecimalSeparator);
            Double.TryParse(vs, out d);
            return d;
        }

        private void LoadUserDefinedRealtimeSymbols(string filename)
        {

            if (File.Exists(filename))
            {
                m_watchListCollection = new Trionic5Tools.SymbolCollection(); // first empty
                char[] sep = new char[1];
                sep.SetValue(';', 0);
                using (StreamReader sr = new StreamReader(filename))
                {
                    string line = string.Empty;
                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] values = line.Split(sep);


                        foreach (Trionic5Tools.SymbolHelper sh in m_RealtimeSymbolCollection)
                        {
                            if (sh.Varname == values.GetValue(0).ToString())
                            {
                                Trionic5Tools.SymbolHelper shnew = new Trionic5Tools.SymbolHelper();
                                shnew.Varname = sh.Varname;
                                shnew.Flash_start_address = sh.Flash_start_address;
                                shnew.Length = sh.Length;
                                shnew.Start_address = sh.Start_address;
                                shnew.Color = sh.Color;
                                shnew.UserCorrectionFactor = ConvertToDouble(values.GetValue(1).ToString());
                                shnew.UserCorrectionOffset = ConvertToDouble(values.GetValue(2).ToString());
                                shnew.UseUserCorrection = true;
                                m_watchListCollection.Add(shnew);
                            }
                        }
                    }
                }
                SetRealtimeSymbollist(m_watchListCollection);
            }
        }

        private void SaveUserDefinedRealtimeSymbols(string filename)
        {
            //Assembly currentAssembly = Assembly.GetExecutingAssembly();


            using (StreamWriter sw = new StreamWriter(filename, false))
            {
                foreach (Trionic5Tools.SymbolHelper sh in m_watchListCollection)
                {
                    sw.WriteLine(sh.Varname + ";" + sh.UserCorrectionFactor.ToString() + ";" + sh.UserCorrectionOffset + ";");
                }
            }
        }

        private void ledToggler19_Load(object sender, EventArgs e)
        {

        }

        private void sndTimer_Tick(object sender, EventArgs e)
        {
            sndTimer.Enabled = false;
            _soundAllowed = true; // re-allow the playback of sounds
        }

        public void AddToRealtimeUserMaps(string symbolname, string descr)
        {
            DataTable dt;
            if (gridControl5.DataSource != null)
            {
                dt = (DataTable)gridControl5.DataSource;
            }
            else
            {
                dt = new DataTable();
                dt.TableName = "UserMaps";
                dt.Columns.Add("Mapname");
                dt.Columns.Add("Description");
            }
            bool found = false;
            foreach (DataRow dr in dt.Rows)
            {
                if (dr["Mapname"].ToString() == symbolname) found = true;
            }
            if (!found)
            {
                dt.Rows.Add(symbolname, descr);
            }
            gridControl5.DataSource = dt;
            // and save
            dt.WriteXml(Application.StartupPath + "\\UserMaps.xml");
        }

        private void gridView5_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete)
            {
                // remove selected entry
                // and save
                int[] rows = gridView5.GetSelectedRows();
                if (rows.Length > 0)
                {
                    gridView5.DeleteRow((int)rows.GetValue(0));
                    DataTable dt = (DataTable)gridControl5.DataSource;
                    dt.WriteXml(Application.StartupPath + "\\UserMaps.xml");
                }

            }
            else if (e.KeyCode == Keys.Enter)
            {
                // start table viewer
                StartMapViewerFromUsermaps();

            }

        }
        private void StartMapViewerFromUsermaps()
        {
            int[] rows = gridView5.GetSelectedRows();
            if (rows.Length > 0)
            {
                DataRow dr = gridView5.GetDataRow((int)rows.GetValue(0));

                if (onMapDisplayRequested != null)
                {
                    onMapDisplayRequested(this, new MapDisplayRequestEventArgs(dr["Mapname"].ToString(), 1, 0, false));
                }
            }
        }

        private void gridView5_DoubleClick(object sender, EventArgs e)
        {
            StartMapViewerFromUsermaps();
        }

        public void ToggleAutoTune()
        {
            // if autotune on -> off
            btnAutoTune_Click(this, EventArgs.Empty);

        }

        private void digitalDisplayControl6_Click(object sender, EventArgs e)
        {
            _peakBoost = -1;
            digitalDisplayControl6.DigitText = _peakBoost.ToString("F2");
        }

        private void btnAutoTuneIgnition_Click(object sender, EventArgs e)
        {
            btnAutoTuneIgnition.Enabled = false;
            btnAutoTuneIgnition.Text = "Wait...";
            Application.DoEvents();

            if (_autoTuningIgnition)
            {
                StopAutoTuneIgnition();
            }
            else
            {
                //btnAutoTune.Text = "Tuning...";
                this.xtraTabControl1.SelectedTabPage = tabAutoTuneIgnition;
                StartAutoTuneIgnition();

            }
        }

        private void ResizeGridControlsIgnition()
        {
            // check font settings depeding on size of the control (font should increase when size increases for better readability)
            gridView6.RowHeight = gridControl6.Height / 18;
            gridView6.Appearance.Row.Font = new Font("Tahoma", gridView6.RowHeight / 3, FontStyle.Bold);
        }

        private void SwitchIgnitionTuning(bool on)
        {
            if (onSwitchIgnitionTuningOnOff != null)
            {
                onSwitchIgnitionTuningOnOff(this, new ClosedLoopOnOffEventArgs(on));
            }
        }


        private void StartAutoTuneIgnition()
        {
            //insert code here
            btnAutoTune.Visible = false;
            SwitchIgnitionTuning(true); // start

            _autoTuningIgnition = true;
            gridView6.PaintStyleName = "UltraFlat";
            gridView6.Appearance.Row.Font = new Font("Tahoma", 6, FontStyle.Bold);
            gridView6.Appearance.Row.TextOptions.VAlignment = DevExpress.Utils.VertAlignment.Center;
            gridView6.Appearance.Row.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            ResizeGridControlsIgnition();
            _initiallyShowIgnitionMap = true;
        }

        private void StopAutoTuneIgnition()
        {
            SwitchIgnitionTuning(false); // stop
            _autoTuningIgnition = false;
            btnAutoTune.Visible = true;
            //insert code here
        }

        public void UpdateMutatedIgnitionMap(int[] ignitionmap, int[] countermap, int[] lockedmap)
        {
            // set data into gridControl6
            int numberrows = 16;
            int tablewidth = 18;
            int map_offset = 0;
            bool changed = false;
            m_ignitionmapMutations = CopyIntData(countermap, m_ignitionmapMutations, out changed);
            if (changed || _initiallyShowIgnitionMap)
            {
                _initiallyShowIgnitionMap = false;
                if (gridControl6.DataSource != null)
                {
                    // only update
                    Console.WriteLine("Detected change in ignition map in realtime control");
                    DataTable dt = (DataTable)gridControl6.DataSource;
                    for (int i = 0; i < numberrows; i++)
                    {
                        //object[] objarr = new object[tablewidth];
                        int b;
                        for (int j = 0; j < (tablewidth); j++)
                        {
                            int v = (int)ignitionmap.GetValue(((15 - i) * tablewidth) + j);
                            if ((int)lockedmap.GetValue(((15 - i) * tablewidth) + j) > 0)
                            {
                                if (dt.Rows[i][j].ToString() != "L" + v.ToString())
                                {
                                    dt.Rows[i][j] = "L" + v.ToString();
                                }
                            }
                            else
                            {
                                if ((int)countermap.GetValue(((15 - i) * tablewidth) + j) > 0)
                                {
                                    if (dt.Rows[i][j].ToString() != "C" + v.ToString())
                                    {
                                        dt.Rows[i][j] = "C" + v.ToString();
                                    }
                                }
                                else
                                {
                                    if (dt.Rows[i][j].ToString() != v.ToString())
                                    {
                                        dt.Rows[i][j] = v.ToString();
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    DataTable dt = new DataTable();
                    if (ignitionmap.Length != 0)
                    {
                        dt.Columns.Clear();
                        for (int c = 0; c < tablewidth; c++)
                        {
                            dt.Columns.Add(c.ToString());
                        }
                        for (int i = numberrows - 1; i >= 0; i--)
                        {
                            object[] objarr = new object[tablewidth];
                            int b;
                            for (int j = 0; j < (tablewidth); j++)
                            {
                                b = (int)ignitionmap.GetValue((i * tablewidth) + j);
                                objarr.SetValue(b.ToString(), j);
                            }
                            dt.Rows.Add(objarr);
                        }

                        gridControl6.DataSource = dt;
                    }
                }
            }
        }

        private void gridControl6_Resize(object sender, EventArgs e)
        {
            ResizeGridControlsIgnition();
        }

        private void gridView6_CustomDrawCell(object sender, DevExpress.XtraGrid.Views.Base.RowCellCustomDrawEventArgs e)
        {
            if (e.CellValue != null)
            {
                if (e.CellValue != DBNull.Value)
                {
                    try
                    {

                        float dv = 0;
                        bool locked = false;
                        bool changed = false;

                        if (e.CellValue.ToString().StartsWith("L"))
                        {
                            // locked cell
                            locked = true;
                            dv = (float)ConvertToDouble(e.CellValue.ToString().Substring(1, e.CellValue.ToString().Length - 1));
                        }
                        else if (e.CellValue.ToString().StartsWith("C"))
                        {
                            changed = true;
                            dv = (float)ConvertToDouble(e.CellValue.ToString().Substring(1, e.CellValue.ToString().Length - 1));
                        }
                        else
                        {
                            dv = (float)ConvertToDouble(e.CellValue.ToString());
                        }
                        if (dv > 32000)
                        {
                            dv = 65536 - dv;
                            dv = -dv;
                        }

                        dv /= 10;
                        e.DisplayText = dv.ToString("F1") + "\u00b0";
                        if (locked)
                        {
                            SolidBrush sbsb = new SolidBrush(Color.OrangeRed); // indicate the cell is locked, needs user release action
                            e.Graphics.FillRectangle(sbsb, e.Bounds);
                            int pos = (e.Bounds.Width) - 12;
                            int ypos = (e.Bounds.Height / 2) - 5;
                            if (pos >= 0)
                            {
                                System.Drawing.Image cflag = (Image)global::Trionic5Controls.Properties.Resources.db_lock16_h;
                                e.Graphics.DrawImage(cflag, e.Bounds.X + pos, e.Bounds.Y + ypos, 10, 10);
                            }

                        }
                        else if (changed)
                        {
                            SolidBrush sbsb = new SolidBrush(Color.YellowGreen); // indicate the cell has been changed this session
                            e.Graphics.FillRectangle(sbsb, e.Bounds);
                        }

                        int mapidx = LookUpIndexAxisMAPMap(_lastInletPressure.Value, m_ignitionxaxis, multiply);
                        int rpmidx = LookUpIndexAxisRPMMap(_lastRPM.Value, m_ignitionyaxis);

                        if (e.Column.AbsoluteIndex == mapidx && (15 - e.RowHandle) == rpmidx)
                        {
                            Pen p = new Pen(Brushes.Black, 2);
                            e.Graphics.DrawRectangle(p, e.Bounds.X + 1, e.Bounds.Y + 1, e.Bounds.Width - 2, e.Bounds.Height - 2);
                            p.Dispose();
                        }

                    }
                    catch (Exception E)
                    {
                        Console.WriteLine(E.Message);
                    }
                }
            }
        }

        private void digitalDisplayControl4_Click(object sender, EventArgs e)
        {
            // switch from AFR to lambda and back
            ToggleAFRLambda();
        }

        private bool _showAsLambda = false;

        private void ToggleAFRLambda()
        {
            _showAsLambda = !_showAsLambda;
            if (_showAsLambda)
            {
                labelControl4.Text = "λ";
            }
            else
            {
                labelControl4.Text = "AFR";
            }
        }

        private void digitalDisplayControl7_Click(object sender, EventArgs e)
        {
            ToggleAFRLambda();
        }

        private void digitalDisplayControl8_Click(object sender, EventArgs e)
        {
            ToggleAFRLambda();
        }



        private void simpleButton4_Click_1(object sender, EventArgs e)
        {
            if (simpleButton4.Text == "Night Panel")
            {
                simpleButton4.Text = "Day Panel";
                this.defaultLookAndFeel1.LookAndFeel.SkinName = "DevExpress Dark Style";

            }
            else if (simpleButton4.Text == "Day Panel")
            {
                this.defaultLookAndFeel1.LookAndFeel.SkinName = "DevExpress Style";
                simpleButton4.Text = "Night Panel";
            }
        }
    }
}

        
        
    


  