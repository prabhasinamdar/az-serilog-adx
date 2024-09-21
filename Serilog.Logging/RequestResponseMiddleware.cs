using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Serilog.Logging
{
    public class RequestResponseMiddleware : IMiddleware
    {
        private readonly ILogger<RequestResponseMiddleware> _logger;
        public RequestResponseMiddleware(ILogger<RequestResponseMiddleware> logger)
        {
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var reqHeaderBuilder = new StringBuilder(Environment.NewLine);
            Guid correlationid = Guid.NewGuid();
            foreach (var header in context.Request.Headers)
            {
                if (!header.Key.ToLower().Contains("authorization"))
                    reqHeaderBuilder.AppendLine($"{header.Key}:{header.Value}");

                if (header.Key.ToLower().Contains("correlationid"))
                        Guid.TryParse(header.Value, out correlationid);
            }
            var userid = context.User.FindFirst("email")?.Value?.ToString();
            //LogContext.PushProperty("userid", userid);
            string queryStringValue = "";
            if(context.Request.QueryString.HasValue)
            {
                queryStringValue = context.Request.QueryString.Value;
            }
            //First, get the incoming request
            var requestBodyAsText = await FormatRequest(context);
            var _apiAuditLogEntityObject = new ApiAuditLog()
            {
                RequestId = correlationid,
                RequestHttpMethod = context.Request.Method,
                RequestDateTime = DateTime.Now,
                RequestOrigin = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}",
                RequestQueryString = queryStringValue,
                RequestContent = requestBodyAsText,
                RequestHeader = reqHeaderBuilder.ToString(),
                AddedBy = userid ?? "",
                DateAdded = DateTime.Now
            };

            //Copy a pointer to the original response body stream
            var originalBodyStream = context.Response.Body;
            string logText = "";
            //Create a new memory stream...
            using (var responseBody = new MemoryStream())
            {
                //...and use that for the temporary response body
                context.Response.Body = responseBody;
                var response = string.Empty;
                //Continue down the Middleware pipeline, eventually returning to this class
                try
                {
                    await next(context);
                    response = await FormatResponse(context.Response);
                }
                catch (Exception ex)
                {
                    logText = $"exception: api trace record {correlationid} | {ex.Message} | {ex.StackTrace}";
                    _logger.LogError(ex, "{@Exception}", ex);
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response = ex.Message == null ? "exception." : ex.Message;
                }
                //Format the response from the server
                _apiAuditLogEntityObject.ResponseContent = response;
                _apiAuditLogEntityObject.ResponseDateTime = DateTime.Now;
                _apiAuditLogEntityObject.ResponseStatusCode = context.Response.StatusCode;
                _apiAuditLogEntityObject.ChangedBy = "Core Services API";
                _apiAuditLogEntityObject.DateChanged = DateTime.Now;
                //Save log to chosen datastore
                logText = $"api trace record {correlationid} {JsonSerializer.Serialize(_apiAuditLogEntityObject)}";

                _logger.LogInformation("{@CorrelationId}{@Message}", correlationid, logText);

                //Copy the contents of the new memory stream (which contains the response) to the original stream, which is then returned to the client.
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }

        private async Task<string> FormatRequest(HttpContext context)
        {
            var request = context.Request;
            string bodyAsText = "";
            request.EnableBuffering();
            if (context.Request.Method == "POST")
            {
                bodyAsText = await new StreamReader(context.Request.Body)
                                              .ReadToEndAsync();
                context.Request.Body.Position = 0;
            }
            else
            {
                var buffer = new byte[Convert.ToInt32(request.ContentLength)];
                await request.Body.ReadAsync(buffer, 0, buffer.Length);
                bodyAsText = Encoding.UTF8.GetString(buffer);
                //Reset the position to 0
                request.Body.Seek(0, SeekOrigin.Begin);
            }
            //Used in TenantProvider
            context.Items["RequestContent"] = bodyAsText;
            return bodyAsText;
        }
        private async Task<string> FormatResponse(HttpResponse response)
        {
            //We need to read the response stream from the beginning...
            response.Body.Seek(0, SeekOrigin.Begin);

            //...and copy it into a string
            string text = await new StreamReader(response.Body).ReadToEndAsync();

            //We need to reset the reader for the response so that the client can read it.
            response.Body.Seek(0, SeekOrigin.Begin);

            //Return the string for the response, including the status code (e.g. 200, 404, 401, etc.)
            return $"{response.StatusCode}: {text}";
        }
    }
}
