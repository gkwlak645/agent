using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace EC_RPA_TRAY.Core
{
    //! @brief  API 서버와 통신중, Response 에서 공통으로 사용되는 형식. 
    public class ApiResponseBase
    {
        public bool success { get; set; }  //!< 성공여부. 성공:true, 실패:false
        public int errCode { get; set; }  //!< 에러코드, 0:성공, 이외의값:실패.
        public string errMessage { get; set; }  //!< 에러메세지.

        public ApiResponseBase()
        {
            success = false;
            errCode = 0;
            errMessage = string.Empty;
        }
    }
}
