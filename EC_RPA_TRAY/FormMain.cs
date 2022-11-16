using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Web;
using System.Data.SqlClient;
using System.Diagnostics;

using MetroFramework.Forms;
using System.Threading;
using EC_RPA_TRAY.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using log4net;
using System.Threading.Tasks;

namespace EC_RPA_TRAY
{
    public partial class FormMain : MetroForm
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FormMain));

        public const string NETWORK_ONLINE_EVENT_NAME = "ServerNetworkOnlineEvent";
        public const string NETWORK_OFFLINE_EVENT_NAME = "ServerNetworkOfflineEvent";
        public int realtime = 60;
        private Boolean isStoped = false;

        BackgroundWorker realtime_walker = null;
        ManualResetEvent realtime_event_start = new ManualResetEvent(false);
        ManualResetEvent realtime_event_stop = new ManualResetEvent(false);
        ManualResetEvent realtime_event_exit = new ManualResetEvent(false);
        EventWaitHandle network_event_online = new EventWaitHandle(false, EventResetMode.ManualReset, NETWORK_ONLINE_EVENT_NAME);
        EventWaitHandle network_event_offline = new EventWaitHandle(false, EventResetMode.ManualReset, NETWORK_OFFLINE_EVENT_NAME);

        private string uipathDir = "";
        
        public FormMain()
        {
            InitializeComponent();

            //! ------------------------------------------------------------------
            //! Realtime.
            RealtimeInit();
            realtime_walker = new BackgroundWorker();
            realtime_walker.DoWork += new DoWorkEventHandler(RealtimeWork);
            realtime_walker.RunWorkerAsync();
            //! ------------------------------------------------------------------

            this.comboBox1.Items.Add("1분");   //60
            this.comboBox1.Items.Add("3분");   //180
            this.comboBox1.Items.Add("5분");   //300
            this.comboBox1.Items.Add("10분");  //600
            this.comboBox1.Items.Add("30분");  //1800
            this.comboBox1.SelectedIndex = 0;   // 처음 시간 설정 0 - 1분, 1 - 3분
            

            //! ------------------------------------------------------------------
            //! Init 설정파일 관련 (없으면 새로 만듬)
            string DirPath = Environment.CurrentDirectory + @"\setting";
            string FilePath = DirPath + "\\Setting.ini" ;
        
            DirectoryInfo di = new DirectoryInfo(DirPath);
            FileInfo fi = new FileInfo(FilePath);

            
            IniFile ini = new IniFile();
            try
            {
                if (!di.Exists) Directory.CreateDirectory(DirPath);
                if (!fi.Exists)
                {
                    ini["RPA_SETTING"]["tanantNm"] = "Default";
                    ini["RPA_SETTING"]["robotNm"] = "Tbot07";
                    ini["RPA_SETTING"]["serverUrl"] = "http://1.221.147.165:9696";
                    ini["RPA_SETTING"]["checkApiUrl"] = "/task/botSchedule/getBotSchedule";
                    ini["RPA_SETTING"]["returnApiUrl"] = "/task/botSchedule/setBotScheduleSttus";
                    ini["RPA_SETTING"]["uipathDir"] = @"C:\Program Files (x86)\UiPath\Studio\UiRobot.exe";
                    ini.Save(FilePath);
                }                
            }
            catch (Exception e)
            {
                log.Debug(e.Message + "[Init File Create Failed]");
            }
            //! ------------------------------------------------------------------

            // ini 읽기
            ini.Load(FilePath);
            string tanantNm = ini["RPA_SETTING"]["tanantNm"].ToString();
            string robotNm = ini["RPA_SETTING"]["robotNm"].ToString();
            string serverUrl = ini["RPA_SETTING"]["serverUrl"].ToString();
            string checkApiUrl = ini["RPA_SETTING"]["checkApiUrl"].ToString();
            string returnApiUrl = ini["RPA_SETTING"]["returnApiUrl"].ToString();
            uipathDir = ini["RPA_SETTING"]["uipathDir"].ToString();
            //! ------------------------------------------------------------------

            this.tb_tanantNm.Text    = tanantNm;
            this.tb_robotNm.Text     = robotNm;
            this.tb_serverUrl.Text   = serverUrl;
            this.tb_checkApiUrl.Text = checkApiUrl;
            this.tb_returnApiUrl.Text = returnApiUrl;

            this.comboBox1.Enabled = false; // 강제 1분으로 고정
            this.tb_tanantNm.ReadOnly = true;
            this.tb_robotNm.ReadOnly = true;
            this.tb_serverUrl.ReadOnly = true;
            this.tb_checkApiUrl.ReadOnly = true;
            this.tb_returnApiUrl.ReadOnly = true;
        }

        private void FormMain_Resize(object sender, EventArgs e)
        {
            //log.Debug("[FormMain_Resize]");
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            //log.Debug("[FormMain_Load]");
            //Visible = false;
            //ShowInTaskbar = false;
            //WindowState = FormWindowState.Minimized;
            radioButton1.Checked = true;            
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //log.Debug("[Form1_FormClosing]");
            if (e.CloseReason == CloseReason.UserClosing)
            {
                RPA_Tray.Visible = true;
                this.Hide();
                e.Cancel = true;
            }
            else{
                RealtimeExit();
                //지금 프로그램 종료하기
                Application.ExitThread();
                Environment.Exit(0);
            }
        }

        //트레이 콘텍스트메뉴 클릭 이벤트
        private void contextMenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            //log.Debug("[contextMenuStrip1_ItemClicked]");
            if (e.ClickedItem.Tag != null && e.ClickedItem.Tag.ToString().Equals("EXIT"))
            {
                RealtimeExit();
                this.Close();
                this.Dispose();
                Application.ExitThread();
                Application.Exit();
            }
            else if (e.ClickedItem.Tag != null && e.ClickedItem.Tag.ToString().Equals("SHOW"))
            {
                WindowState = FormWindowState.Normal;
                this.Visible = true;
                this.Focus();
            }
        }

        //ON
        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (this.radioButton1.Checked == true)
            {
                log.Debug("[radioButton1_CheckedChanged]");                
                RealtimeStart();                
            }                      
        }

        //OFF
        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (this.radioButton2.Checked == true)
            {
                log.Debug("[radioButton2_CheckedChanged]");
                RealtimeStop();               
            }
        }


        //////////////////////////////////////////////////////////////////////////////////////////////////////////
        void RealtimeInit()
        {
            try
            {
                realtime_event_start.Reset();
                realtime_event_stop.Reset();
                realtime_event_exit.Reset();
            }
            catch (Exception ex)
            {
                log.Debug("[**** EXCEPTIN!! ****] " + ex.ToString() +  Environment.NewLine);
            }
            finally
            {
            }
        }

        void RealtimeStart()
        {
            try
            {                
                realtime_event_start.Set();
            }
            catch (Exception ex)
            {
                log.Debug("[**** EXCEPTIN!! ****] " + ex.ToString() + Environment.NewLine);
            }
            finally
            {
            }
        }

        void RealtimeStop()
        {
            try
            {             
                realtime_event_stop.Set();
            }
            catch (Exception ex)
            {
                log.Debug("[**** EXCEPTIN!! ****] " + ex.ToString() + Environment.NewLine);
            }
            finally
            {
            }
        }

        void RealtimePause()
        {
            try
            {
                realtime_event_stop.Set();
            }
            catch (Exception ex)
            {
                log.Debug("[**** EXCEPTIN!! ****] " + ex.ToString() + Environment.NewLine);
            }
            finally
            {
            }
        }


        void RealtimeExit()
        {
            try
            {
                realtime_event_exit.Set();
            }
            catch (Exception ex)
            {
                log.Debug("[**** EXCEPTIN!! ****] " + ex.ToString() + Environment.NewLine);
            }
            finally
            {
            }
        }

        void RealtimeWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                WaitHandle[] waitWork = new WaitHandle[5];
                int index = 0;
                waitWork[index++] = realtime_event_exit;
                waitWork[index++] = realtime_event_start;
                waitWork[index++] = realtime_event_stop;
                waitWork[index++] = network_event_online;
                waitWork[index++] = network_event_offline;

                int wait_result = -1;

                bool work_enabled = false;

                do
                {
                    
                    wait_result = WaitHandle.WaitAny(waitWork, realtime * 1000);
                    switch (wait_result)
                    {
                        case 0:
                            break;

                        case 2:
                            work_enabled = false;
                            log.Debug("MODE_SALE_IDLE - STOP ");
                            realtime_event_stop.Reset();
                            break;

                        //! network online
                        case 3:
                            log.Debug("--- get network online");
                            break;

                        //! network offline
                        case 4:
                            log.Debug("--- get network offline");
                            break;

                        case 1:
                            work_enabled = true;
                            log.Debug("MODE_SALE_IDLE - START realtime " + realtime.ToString());
                            realtime_event_start.Reset();
                            goto case WaitHandle.WaitTimeout;

                        case WaitHandle.WaitTimeout:
                            log.Debug("MODE_SALE_IDLE - WORK :" + work_enabled.ToString() + " " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            if (work_enabled)
                            {
                                isStoped = false;
                                GetBotScheduleAsync();                             
                            }
                            break;
                    }
                } while (wait_result != 0);
            }
            catch (Exception ex)
            {
                log.Debug("[**** EXCEPTIN!! ****] " + ex.ToString() + Environment.NewLine);
            }
            finally
            {
            }
        }
        //////////////////////////////////////////////////////////////////////////////////////////////////////////


        //////////////////////////////////////////////////////////////////////////////////////////////////////////

        // 포털 API 호출하는 공통 모듈
        ApiResult CommonCallApi(string type, ApiRequest req)
        {
            ApiResult apiResult = new ApiResult(); ;
            int result = 0;
            ApiReponse apiResp = null;

            try
            {
                log.Debug("CommonCallApi() tb_tanantNm - " + tb_tanantNm.Text);
                log.Debug("CommonCallApi() tb_robotNm - " + tb_robotNm.Text);
                log.Debug("CommonCallApi() tb_serverUrl - " + tb_serverUrl.Text);
                log.Debug("CommonCallApi() tb_checkApiUrl - " + tb_checkApiUrl.Text);
                log.Debug("CommonCallApi() tb_returnApiUrl - " + tb_returnApiUrl.Text);
                
                log.Debug("CommonCallApi() sttus - " + req.sttus);

                req.tanantNm = tb_tanantNm.Text;
                req.robotNm = tb_robotNm.Text;
                string request_url = "";

                // type = 0 : 스케줄조회
                if ("0".Equals(type))
                {
                    request_url = tb_serverUrl.Text + tb_checkApiUrl.Text;
                }

                // type = 1 : 작업시작
                if ("1".Equals(type))
                {
                    request_url = tb_serverUrl.Text + tb_returnApiUrl.Text;                    
                }

                // type = 2 : 작업결과
                if ("2".Equals(type))
                {
                    request_url = tb_serverUrl.Text + tb_returnApiUrl.Text;                    
                }

                log.Debug("GetBotSchedule() request_url - " + request_url);

                string request_string = JsonConvert.SerializeObject(req);

                result = HttpHelper.GetJsonResponse(request_url, "POST", request_string, ref apiResp, true);

                if (result != 0 || apiResp == null)
                {
                    //실패
                    log.Debug("FAIL HttpHelper.GetJsonResponse : " + result.ToString());
                }
                else
                {
                    //성공
                    log.Debug("SUCCESS  : resp - " + apiResp);
                    if (apiResp != null)
                    {
                        log.Debug("SUCCESS  : resp.responseCode - " + apiResp.responseCode);
                        log.Debug("SUCCESS  : resp.responseMessage - " + apiResp.responseMessage);

                        if (apiResp.responseCode.Equals("I101"))
                        {
                            //log.Debug("SUCCESS  : resp.result.processName - " + apiResp.result.processName);
                            //log.Debug("SUCCESS  : resp.result.processKey - " + apiResp.result.processKey);
                            //log.Debug("SUCCESS  : resp.result.sttus - " + apiResp.result.sttus);
                            //log.Debug("SUCCESS  : resp.result.sn - " + apiResp.result.sn);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Debug("[**** EXCEPTIN!! ****] " + ex.ToString() + Environment.NewLine);
            }
            finally
            {
                apiResult.result = result;
                apiResult.resultData = apiResp;
            }
            return apiResult;
        }


        // CMD를 이용하여 로봇 프로세스를 실행한다
        string ActionUipath(ApiReponse schResp)
        {
            string resultStr = "";
            ProcessStartInfo cmd = new ProcessStartInfo();
            Process process = new Process();

            try
            {
                cmd.FileName = @"cmd";

                //cmd.WindowStyle = ProcessWindowStyle.Hidden;             // cmd창이 숨겨지도록 하기
                cmd.CreateNoWindow = true;                               // cmd창을 띄우지 안도록 하기
                cmd.UseShellExecute = false;

                cmd.RedirectStandardOutput = true;        // cmd창에서 데이터를 가져오기
                cmd.RedirectStandardInput = true;          // cmd창으로 데이터 보내기
                cmd.RedirectStandardError = true;          // cmd창에서 오류 내용 가져오기

                //string fullPath = @"C:\Program Files (x86)\UiPath\Studio\UiRobot.exe";
                string fullPath = uipathDir;

                
                cmd.WorkingDirectory = Path.GetDirectoryName(fullPath);

                //process.EnableRaisingEvents = false;
                process.StartInfo = cmd;
                process.Start();
                
                string processName = schResp.result.processKey;

                process.StandardInput.Write(@"UiRobot.exe execute --process " + processName + Environment.NewLine);
                process.StandardInput.Close();

                Task k = startStopCheck();

                string uipathResult = process.StandardOutput.ReadToEnd();

                if (uipathResult.IndexOf("Exception") > 0)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("[Result Info]" + DateTime.Now + "\r\n");
                    sb.Append(uipathResult);
                    sb.Append("\r\n");

                    resultStr = sb.ToString();
                }

                if (uipathResult.IndexOf("{}") > 0)
                {
                    resultStr = "0";
                }

            }
            catch (Exception ex)
            {
                log.Debug("[**** EXCEPTIN!! ****] " + ex.ToString() + Environment.NewLine);
            }
            finally
            {
                log.Debug("[33333");
                process.WaitForExit();
                process.Close();

            }

            return resultStr;
        }

        

        private async Task startStopCheck()
        {
            await Task.Run(() =>
            {
                log.Debug("[startStopCheck Start]");

                while (true)
                {

                    Thread.Sleep(realtime * 1000);
                    //log.Debug("[startStopCheck ing");

                    if (isStoped)
                    {
                        break;
                    }


                    if (IsStopCommand() == 1000)
                    {
                        string sProcessName = "UiPath.Executor"; //UiPath.Executor
                        Process[] arProcess = Process.GetProcessesByName(sProcessName);
                                                
                        if (arProcess.Length > 0)
                        {                        
                            isStoped = true;
                            arProcess[0].Kill();
                            break;
                        }
                    }                    
                }                
            });
        }

        private int GetBotScheduleAsync()
        {
            int result = 0;
            try
            {
                log.Debug("[**** Start GetBotScheduleAsync ****] " + Environment.NewLine);

                // 스케줄 조회 API콜
                ApiRequest getSchApiReq = new ApiRequest();
                ApiResult getBotScheduleResult = CommonCallApi("0", getSchApiReq);

                if (getBotScheduleResult.result != 0)
                {
                    log.Debug("FAIL HttpHelper.GetJsonResponse : " + result.ToString());
                    throw new Exception("RPAE:9001 - 스케줄 통신중 오류가 발생하였습니다.");
                }

                ApiReponse getSchResp = getBotScheduleResult.resultData;
                if (getSchResp == null)
                {
                    log.Debug("Response Data is null");
                    throw new Exception("RPAE:9002 - 스케줄 통신 성공하였으나 데이터에 문제가 발생하였습니다.");
                }

                if ("I100".Equals(getSchResp.responseCode)) //스케줄이 없는경우
                {
                    log.Debug("Not Schedule!");
                }
                else if ("E200".Equals(getSchResp.responseCode)) //등록되지 않은 로봇일경우
                {
                    log.Debug("Not Register Robot");
                    throw new Exception("RPAE:9003 - 스케줄 통신 성공하였으나 등록되지 않은 ROBOT입니다.");
                }
                
                if ("I101".Equals(getSchResp.responseCode)) //스케줄이 있는경우
                {
                    if("WAIT".Equals(getSchResp.result.sttus))
                    {
                        string startDt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        // 시작시그널 API콜
                        ApiRequest startApiReq = new ApiRequest();
                        startApiReq.sn = getSchResp.result.sn;
                        startApiReq.sttus = "START";
                        startApiReq.startDt = startDt;

                        ApiResult startApiResult = CommonCallApi("1", startApiReq);

                        // cmd 콜
                        string cmdResp = ActionUipath(getSchResp);

                        log.Debug("**" + cmdResp);

                        // 결과 리턴 API콜
                        ApiRequest endApiReq = new ApiRequest();

                        endApiReq.sn = getSchResp.result.sn;
                        if ("0".Equals(cmdResp))
                        {
                            endApiReq.sttus = "SUCCESS";
                        }
                        else
                        {

                            if (isStoped)
                            {
                                endApiReq.sttus = "STOPPED";
                            }
                            else
                            {
                                endApiReq.sttus = "EXCEPTION";
                            }

                            
                            endApiReq.exceptionMssage = cmdResp;
                        }
                        string endDt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        endApiReq.startDt = startDt;
                        endApiReq.endDt = endDt;

                        ApiResult endApiResult = CommonCallApi("2", endApiReq);
                    } 
                    else if ("STOP".Equals(getSchResp.result.sttus)) // stop
                    {
                        log.Debug("[**** 결과이상 ****] " + Environment.NewLine);
                    } else
                    {
                        //started
                    }
                 

                }
                else  // 존재하지 않는 리스폰스 코드
                {

                }
                

            }
            catch (Exception ex)
            {
                log.Debug("[**** EXCEPTION!! **** MAIN ****]" + Environment.NewLine);
                log.Debug(System.Reflection.Assembly.GetExecutingAssembly().ManifestModule.Name + ":" + Environment.NewLine + // Project Name (Project Name (프로젝트명))                    
                                 System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name + "." + Environment.NewLine + // Class Name (Class Name (클래스명))                          
                                 System.Reflection.MethodBase.GetCurrentMethod().Name + "():" + Environment.NewLine + // Function Name (Function Name (함수명))                      
                                 ex.StackTrace + Environment.NewLine + ex.Message);
            }
            finally
            {
                isStoped = true;
            }

            return result;
        }



        private int IsStopCommand()
        {
            int result = 0;
            try
            {
                log.Debug("[**** Start IsStopCommand ****] " + Environment.NewLine);

                // 스케줄 조회 API콜
                ApiRequest getSchApiReq = new ApiRequest();
                ApiResult getBotScheduleResult = CommonCallApi("0", getSchApiReq);

                if (getBotScheduleResult.result != 0)
                {
                    log.Debug("FAIL HttpHelper.GetJsonResponse : " + result.ToString());
                    throw new Exception("RPAE:9001 - 스케줄 통신중 오류가 발생하였습니다.");
                }

                ApiReponse getSchResp = getBotScheduleResult.resultData;
                if (getSchResp == null)
                {
                    log.Debug("Response Data is null");
                    throw new Exception("RPAE:9002 - 스케줄 통신 성공하였으나 데이터에 문제가 발생하였습니다.");
                }

                if ("I100".Equals(getSchResp.responseCode)) //스케줄이 없는경우
                {
                    log.Debug("Not Schedule!");
                }
                else if ("E200".Equals(getSchResp.responseCode)) //등록되지 않은 로봇일경우
                {
                    log.Debug("Not Register Robot");
                    throw new Exception("RPAE:9003 - 스케줄 통신 성공하였으나 등록되지 않은 ROBOT입니다.");
                }

                if ("I101".Equals(getSchResp.responseCode)) //스케줄이 있는경우
                {
                    if ("WAIT".Equals(getSchResp.result.sttus))
                    {
                      // 존재할수 없는경우
                    }
                    else if ("STOP".Equals(getSchResp.result.sttus)) // stop
                    {
                        result = 1000;
                    }
                    else
                    {
                        //started
                    }
                }
                else  // 존재하지 앟는 리스폰스 코드
                {

                }
            }
            catch (Exception ex)
            {
                log.Debug("[**** EXCEPTION!! **** MAIN ****]" + Environment.NewLine);
                log.Debug(System.Reflection.Assembly.GetExecutingAssembly().ManifestModule.Name + ":" + Environment.NewLine + // Project Name (Project Name (프로젝트명))                    
                                 System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name + "." + Environment.NewLine + // Class Name (Class Name (클래스명))                          
                                 System.Reflection.MethodBase.GetCurrentMethod().Name + "():" + Environment.NewLine + // Function Name (Function Name (함수명))                      
                                 ex.StackTrace + Environment.NewLine + ex.Message);
            }
            finally
            {                

            }
            return result;
        }



    }

}
