using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serilog.Logging
{
    public class ApiAuditLog
    {
        public int ApiAuditLogId { get; set; }
        public Guid RequestId { get; set; }
        public DateTime? RequestDateTime { get; set; }
        public string RequestHttpMethod { get; set; }
        public string RequestOrigin { get; set; }
        public string RequestHeader { get; set; }
        public string RequestQueryString { get; set; }
        public string RequestContent { get; set; }
        public DateTime? ResponseDateTime { get; set; }
        public string ResponseContent { get; set; }
        public int ResponseStatusCode { get; set; }
        public string AddedBy { get; set; }
        public DateTime? DateAdded { get; set; }
        public string ChangedBy { get; set; }
        public DateTime? DateChanged { get; set; }
    }
}
