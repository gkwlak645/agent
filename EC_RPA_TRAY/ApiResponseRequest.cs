using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EC_RPA_TRAY.Core
{
    public class ApiRequest
    {
        public string tanantNm { get; set; }     
        public string robotNm { get; set; }
        public string sttus { get; set; }
        public int sn { get; set; }
        public string exceptionMssage { get; set; }
        public string startDt { get; set; }
        public string endDt { get; set; }
    }

    public class ApiReponse : ApiResponseBase
    {
        public string responseCode { get; set; }
        public string responseMessage { get; set; }
        public string transactionId { get; set; }
        public RespData result { get; set; }

        public class RespData
        {
            public string inputArguments { get; set; }
            public string processName { get; set; }
            public string processKey { get; set; }
            public string sttus { get; set; }
            public int sn { get; set; }

        }
    }

    public class ApiResult
    {
        public int result { get; set; }
        public ApiReponse resultData { get; set; }
    }

}
