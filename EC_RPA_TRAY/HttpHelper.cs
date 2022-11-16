/**********************************************************************  
  * 설명____________: web 통신 helper
  **********************************************************************/
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Security.Cryptography;
using log4net;

namespace EC_RPA_TRAY.Core
{
    public static class HttpHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(HttpHelper));

        public static long DownloadFile(String remoteFilename, String localFilename, ProgressBar pb = null)
        {
            if (string.IsNullOrEmpty(remoteFilename) || string.IsNullOrEmpty(localFilename))
            {
                return -1;
            }

            // Function will return the number of bytes processed
            // to the caller. Initialize to 0 here.
            long bytesProcessed = 0;
            long bytesToRead = 0;

            // Assign values to these objects here so that they can
            // be referenced in the finally block
            Stream remoteStream = null;
            Stream localStream = null;
            WebResponse response = null;

            if (File.Exists(localFilename))
            {
                File.Delete(localFilename);
            }

            // Use a try/catch/finally block as both the WebRequest and Stream
            // classes throw exceptions upon error
            try
            {
                // Create a request for the specified remote file name
                WebRequest request = WebRequest.Create(remoteFilename);
                if (request != null)
                {
                    // Send the request to the server and retrieve the
                    // WebResponse object 
                    response = request.GetResponse();
                    if (response != null)
                    {

                        if (!Int64.TryParse(response.Headers.Get("Content-Length"), out bytesToRead))
                        {
                            return -2;
                        }

                        if (pb != null)
                        {
                            pb.Value = 0;
                            pb.Minimum = 0;
                            pb.Maximum = (int)(bytesToRead / 1024);
                        }

                        // Once the WebResponse object has been retrieved,
                        // get the stream object associated with the response's data
                        remoteStream = response.GetResponseStream();

                        // Create the local file
                        localStream = File.Create(localFilename);

                        // Allocate a 1k buffer
                        byte[] buffer = new byte[1024];
                        int bytesRead;

                        // Simple do/while loop to read from stream until
                        // no bytes are returned
                        do
                        {
                            // Read data (up to 1k) from the stream
                            bytesRead = remoteStream.Read(buffer, 0, buffer.Length);

                            // Write the data to the local file
                            localStream.Write(buffer, 0, bytesRead);

                            // Increment total bytes processed
                            bytesProcessed += bytesRead;

                            if (pb != null)
                            {
                                pb.Value = (int)(bytesProcessed / 1024);
                                pb.Update();
                            }
                            //Console.WriteLine(bytesProcessed.ToString());
                        } while (bytesRead > 0);

                        if (pb != null)
                        {
                            if (bytesProcessed >= bytesToRead)
                            {
                                pb.Value = pb.Maximum;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //log.Error(e.Message);
                //log.Error(e.StackTrace);
                bytesProcessed = -100;
                log.Debug(ex.Message);
            }
            finally
            {
                // Close the response and streams objects here 
                // to make sure they're closed even if an exception
                // is thrown at some point
                if (response != null) response.Close();
                if (remoteStream != null) remoteStream.Close();
                if (localStream != null) localStream.Close();
            }

            // Return total bytes processed to caller.
            return bytesProcessed;
        }

        /// <summary>
        /// 파일이 있는지 검사하고 있으면 파일의 크기를 읽어옴. 0보다 작은경우 : 에러 및 파일이 없음. 
        /// </summary>
        /// <param name="remoteFilename"></param>
        /// <returns></returns>
        public static long ExistFile(String remoteFilename)
        {
            long return_value = 0;
            //int result = 0;
            if (string.IsNullOrEmpty(remoteFilename))
            {
                return -1;
            }

            HttpWebResponse response = null;
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(remoteFilename);
                request.Method = "HEAD";

                response = (HttpWebResponse)request.GetResponse();

                if (!Int64.TryParse(response.Headers.Get("Content-Length"), out return_value))
                {
                    return_value = -2;
                }
            }
            catch (WebException ex)
            {
                return_value = -100;
                log.Debug(ex.Message);
            }
            finally
            {
                if (response != null)
                {
                    response.Close();
                }
            }

            return return_value;
        }

       
        public static int GetJsonResponse<T>(string send_url, string send_method, string send_data, ref T recv_list, bool logging_data)
        {
            log.Debug("IN");
            int return_value = 0;

            HttpWebRequest httpWebRequest = null;
            HttpWebResponse httpResponse = null;


            //! input check.
            if (string.IsNullOrEmpty(send_url) || string.IsNullOrEmpty(send_method))
            {
                //log.Error("Invalid input.");
                return -1;
            }


            try
            {
                if (send_method == "GET")
                {
                    send_url += "?" + send_data;
                }


                httpWebRequest = (HttpWebRequest)WebRequest.Create(send_url);
                //add htech
                string compareText = string.Empty;
                compareText = send_url.ToUpper();
                if (compareText.IndexOf("HTTPS") != -1)
                {
                    httpWebRequest.Credentials = CredentialCache.DefaultCredentials;
                    System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Ssl3;
                    ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => { return true; };
                }

                httpWebRequest.Proxy = null;
                httpWebRequest.Method = send_method;
                httpWebRequest.Timeout = 3 * 60 * 1000; //! 5 분 최대 대기.

                if (send_method == "POST")
                {
#if true   //! 데이터 저장을 위해서, 2초쯤 더걸림.
                    if (logging_data)
                    {
                        log.Debug(typeof(T).Name + "_Request");
                        log.Debug(send_data);
                    }
#endif

                    byte[] bytes = Encoding.UTF8.GetBytes(send_data);

                    httpWebRequest.ContentType = "application/json;charset=UTF-8";
                    httpWebRequest.ContentLength = bytes.Length;
                    Stream requestStream = httpWebRequest.GetRequestStream();
                    requestStream.Write(bytes, 0, bytes.Length);
                    requestStream.Close();
                }

                //! 응답을 받고, 
                httpResponse = httpWebRequest.GetResponse() as HttpWebResponse;

                log.Debug("Get response, status [{0}]" + httpResponse.StatusCode);
                //! 응답코드를 체크하고, 


               
                //웹서버에서 202 에러 응답이 왔다.exception 발생.조치사항.
                //웹서버 헤더 토큰 이상 - POS_CFG 테이블에 API_TOKEN_DATA,API_TOKEN_DATE 데이터를 삭제 후 조치.새로 받는다.

                if (httpResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception(string.Format("Server error (HTTP {0}: {1})."
                                                     , httpResponse.StatusCode
                                                     , httpResponse.StatusDescription));
                }

                //! 정상이면 데이터를 뽑아낸다.
                using (Stream responseStream = httpResponse.GetResponseStream())
                {

                    log.Debug("Get response stream success.");


                    //! 메모리 스트림에 받아서, 
                    //! 압축된것이면 압축을 풀고,
                    //log.Info("GetResponse::Json Parsing started.");

                    using (StreamReader sr = new StreamReader(responseStream))
                    {
#if true   //! 데이터 저장을 위해서, 2초쯤 더걸림.
                        string response_string = sr.ReadToEnd();
                        log.Debug(">>Json parse start.");
                        if (logging_data)
                        {
                            //File.WriteAllText(tran_data_dir + DateTime.Now.ToString("yyyyMMddHHmmss_") + typeof(T).Name + ".json",  response_string);
                            log.Debug(typeof(T).Name);
                            log.Debug(response_string);
                        }

                        JsonHelper.Deserialize(response_string, ref recv_list);
                        log.Debug("<<json parse finish.");
#else
                        CommonVariables.logger.Debug("    json parse start.");
                        JsonHelper.Deserialize(sr, ref recv_list);
                        CommonVariables.logger.Debug("    json parse finish.");
#endif
                    }

                    responseStream.Close();
                }

                return_value = 0;
            }
            catch (WebException ex)
            {
                log.Debug("Error on url:[{0}" + send_url);
                log.Debug(ex.Message);
                return_value = -2;

                if (httpWebRequest != null)
                {
                    httpWebRequest.Abort();
                }

                //htech : throw 하지 말고 에러코드만 리턴하자.
                //throw;
            }
            catch (Exception ex)
            {
                log.Debug(ex.Message);
                return_value = -3;

                if (httpWebRequest != null)
                {
                    httpWebRequest.Abort();
                }

                //htech : throw 하지 말고 에러코드만 리턴하자.
                //throw;
            }
            finally
            {
                if (httpResponse != null)
                {
                    httpResponse.Close();
                }
            }

            log.Debug("OUT");
            return return_value;
        }

        //! @brief : Server의 online 여부를 묻기위함. 
        //! @note   - url 만 입력으로 받음.
        public static int GetServerOnlineCheck(string send_url)
        {
            int return_value = 0;
            string send_method = "GET";
            string send_data = string.Empty;
            string token_data = string.Empty;

            HttpWebRequest httpWebRequest = null;
            HttpWebResponse httpResponse = null;


            //! input check.
            if (string.IsNullOrEmpty(send_url))
            {
                return -1;
            }


            try
            {
                httpWebRequest = (HttpWebRequest)WebRequest.Create(send_url);

                //add htech
                string compareText = string.Empty;
                compareText = send_url.ToUpper();
                if (compareText.IndexOf("HTTPS") != -1)
                {
                    httpWebRequest.Credentials = CredentialCache.DefaultCredentials;
                    System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Ssl3;
                    ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => { return true; };
                }

                if (!string.IsNullOrWhiteSpace(token_data))
                {
                    httpWebRequest.Headers["__TOKEN_DATA__"] = token_data;
                }

                httpWebRequest.Proxy = null;
                httpWebRequest.Method = send_method;
                httpWebRequest.Timeout = 3 * 1000; //! 5 분 최대 대기.

                if (send_method == "POST")
                {

                    byte[] bytes = Encoding.UTF8.GetBytes(send_data);

                    httpWebRequest.ContentType = "application/json;charset=UTF-8";
                    httpWebRequest.ContentLength = bytes.Length;
                    Stream requestStream = httpWebRequest.GetRequestStream();
                    requestStream.Write(bytes, 0, bytes.Length);
                    requestStream.Close();
                }

                //! 응답을 받고, 
                httpResponse = httpWebRequest.GetResponse() as HttpWebResponse;


                //! 응답코드를 체크하고, 
                if (httpResponse.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception(string.Format("    Server error (HTTP {0}: {1})."
                                                     , httpResponse.StatusCode
                                                     , httpResponse.StatusDescription));
                }

                //! 정상이면 데이터를 뽑아낸다.
                using (Stream responseStream = httpResponse.GetResponseStream())
                {
                    responseStream.Close();
                }

                return_value = 0;
            }
            catch (WebException ex)
            {
                return_value = -2;

                if (httpWebRequest != null)
                {
                    httpWebRequest.Abort();
                }
            }
            catch (Exception ex)
            {
                return_value = -3;

                if (httpWebRequest != null)
                {
                    httpWebRequest.Abort();
                }
            }
            finally
            {
                if (httpResponse != null)
                {
                    httpResponse.Close();
                }
            }

            return return_value;
        }


        public static bool SendRecv(string send_url, string send_method, string send_data, ref string recv_data, string compress = "N")
        {
            bool return_value = true;
            HttpWebResponse httpWebResponse = null;
            HttpWebRequest httpWebRequest = null;

            //! input check.
            if (string.IsNullOrWhiteSpace(send_url) || string.IsNullOrWhiteSpace(send_method))
            {
                return false;
            }

            try
            {

                if (send_method == "GET")
                {
                    send_url += "?" + send_data;
                }

                httpWebRequest = (HttpWebRequest)WebRequest.Create(send_url);

                //Add htech 
                string compareText = string.Empty;
                compareText = send_url.ToUpper();
                if (compareText.IndexOf("HTTPS") != -1)
                {
                    httpWebRequest.Credentials = CredentialCache.DefaultCredentials;
                    System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Ssl3;
                    ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => { return true; };
                }

                httpWebRequest.Method = send_method;
                httpWebRequest.Timeout = 5 * 60 * 1000; //! 5 분 최대 대기.
                //httpWebRequest.Timeout = System.Threading.Timeout.Infinite;

                if (send_method == "POST")
                {
                    //Write 
                    byte[] bytes = UTF8Encoding.UTF8.GetBytes(send_data);
                    httpWebRequest.ContentType = "application/x-www-form-urlencoded";// "text/xml; encoding='utf-8'";
                    httpWebRequest.ContentLength = bytes.Length;
                    Stream requestStream = httpWebRequest.GetRequestStream();
                    requestStream.Write(bytes, 0, bytes.Length);
                    requestStream.Close();
                }

                //response 
                httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                Stream stream = httpWebResponse.GetResponseStream();

#if (STREAM_TO_FILE)
                FileStream gFS = new FileStream("test file", FileMode.Create);
                long CurFileSize = 0;
#endif

                int recv_bytes = 0;
                const int LENGTH = 1024 * 10;
                const int OFFSET = 0;
                byte[] buffer = new byte[LENGTH];

                MemoryStream store = new MemoryStream();
                while (true)
                {
                    Array.Clear(buffer, 0, buffer.Length);
                    recv_bytes = stream.Read(buffer, OFFSET, LENGTH);
                    if (recv_bytes > 0)
                    {
#if (STREAM_TO_FILE)
                        CurFileSize = CurFileSize + recv_bytes;
                        gFS.Write(buffer, 0, recv_bytes);
#endif
                        store.Write(buffer, 0, recv_bytes);
                    }
                    else
                    {
#if (STREAM_TO_FILE)
                         gFS.Close();
#endif
                        break;
                    }
                }

                stream.Close();

                //압축풀기
                if (compress.Equals("Y"))
                {
                    using (MemoryStream inStream = new MemoryStream(store.GetBuffer()))
                    using (GZipStream gzipStream = new GZipStream(inStream, CompressionMode.Decompress))
                    using (MemoryStream outStream = new MemoryStream())
                    {
                        gzipStream.CopyTo(outStream);
                        recv_data = Encoding.Default.GetString(outStream.ToArray());
                    }
                }
                else
                {
                    //recv_data = Encoding.UTF8.GetString(data);
                    recv_data = Encoding.UTF8.GetString(store.GetBuffer());
                }

                return_value = true;
            }
            catch (WebException ex)
            {
                return_value = false;
                log.Debug(ex.Message);

                using (WebResponse response = ex.Response)
                {
                    HttpWebResponse httpResponse2 = (HttpWebResponse)response;

                    if (httpResponse2 != null)
                    {
                        Console.WriteLine("Error : {0}", httpResponse2.StatusCode);
                        using (Stream data = response.GetResponseStream())
                        using (var reader = new StreamReader(data))
                        {
                            //the html content is here
                            recv_data = reader.ReadToEnd();
                            Console.WriteLine(recv_data);
                        }
                    }
                }
                recv_data = ex.Message;

                if (httpWebRequest != null)
                {
                    httpWebRequest.Abort();
                }
            }
            catch (Exception ex)
            {
                return_value = false;
                log.Debug(ex.Message);

                recv_data = ex.Message;

                if (httpWebRequest != null)
                {
                    httpWebRequest.Abort();
                }
            }
            finally
            {
                if (httpWebResponse != null)
                {
                    httpWebResponse.Close();
                }
            }
            return return_value;
        }



    }
}
