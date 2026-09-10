using Medidata.RWS.NET.Standard.Core;
using Medidata.RWS.NET.Standard.Core.Objects;
using Medidata.RWS.NET.Standard.Core.Requests;
using Medidata.RWS.NET.Standard.Core.Requests.Datasets;
using Medidata.RWS.NET.Standard.Core.Responses;
using Medidata.RWS.NET.Standard.Exceptions;
using RaveStudioAI.Rws.Models;
using System.Diagnostics;

namespace RaveStudioAI.Rws.Services
{
    public sealed class RaveRwsService
    {
        private const int MaxAttempts = 2;
        private static readonly TimeSpan[] RetryDelays =
        [
            TimeSpan.FromSeconds(2)
        ];

        public async Task<IReadOnlyList<Study>> GetStudiesAsync(string subdomain, string username, string password)
        {
            var connection = CreateConnection(subdomain, username, password);
            var studies = await SendWithRetryAsync(() => connection.SendRequestAsync(new ClinicalStudiesRequest()));
            var response = studies as RwsStudies ?? throw new InvalidOperationException("Unexpected studies response.");

            return response
                .Select(x => new Study
                {
                    Oid = x.Oid,
                    StudyName = x.StudyName,
                    ProtocolName = x.ProtocolName,
                    Environment = x.Environment
                })
                .ToList();
        }

        public async Task<IReadOnlyList<Subject>> GetSubjectsAsync(
            string subdomain,
            string username,
            string password,
            string project,
            string environment)
        {
            var connection = CreateConnection(subdomain, username, password);
            var subjects = await SendWithRetryAsync(() => connection.SendRequestAsync(new StudySubjectsRequest(project, environment)));
            var response = subjects as RwsSubjects ?? throw new InvalidOperationException("Unexpected subjects response.");

            return response
                .Select(x => new Subject { SubjectKey = x.SubjectKey })
                .ToList();
        }

        // 旧版批量下载入口：一次方法内拉完所有 subject + form。
        // 当前 WPF 主流程已改用 DownloadSubjectFormRowsAsync，由界面层记录已完成组合，从而支持失败后续拉。
        [Obsolete("Use DownloadSubjectFormRowsAsync with DatasetDownloadJob for resumable dataset downloads.")]
        public async Task<List<RaveDatasetRow>> DownloadRowsAsync(
            string subdomain,
            string username,
            string password,
            string project,
            string environment,
            IReadOnlyList<string> subjectKeys,
            IReadOnlyList<string> formOids,
            string datasetType,
            IProgress<(int Completed, int Total)>? progress = null)
        {
            var rows = new List<RaveDatasetRow>();
            var subjects = subjectKeys
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var forms = formOids
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var total = subjects.Count * forms.Count;
            var completed = 0;

            progress?.Report((completed, total));

            foreach (var formOid in forms)
            {
                foreach (var subjectKey in subjects)
                {
                    var batchRows = await DownloadSubjectFormRowsAsync(
                        subdomain,
                        username,
                        password,
                        project,
                        environment,
                        subjectKey,
                        formOid,
                        datasetType);

                    rows.AddRange(batchRows);
                    completed += 1;
                    progress?.Report((completed, total));
                }
            }

            return rows;
        }

        public async Task<List<RaveDatasetRow>> DownloadSubjectFormRowsAsync(
            string subdomain,
            string username,
            string password,
            string project,
            string environment,
            string subjectKey,
            string formOid,
            string datasetType)
        {
            var connection = CreateConnection(subdomain, username, password);
            var request = new SubjectDatasetRequest(
                project,
                environment,
                subjectKey,
                dataset_type: datasetType,
                formOid: formOid,
                decodesuffix: "_std");
            var result = await SendWithRetryAsync(() => connection.SendRequestAsync(request));
            var response = result as RwsResponse ?? throw new InvalidOperationException("Unexpected dataset response.");
            var xml = await response.ResponseObject.Content.ReadAsStringAsync();

            return OdmDatasetParser.ParseRows(xml);
        }

        private static RwsConnection CreateConnection(string subdomain, string username, string password)
        {
            return new RwsConnection(subdomain, username, password);
        }

        public async Task SendQueryAsync(
            string subdomain,
            string username,
            string password,
            string studyoid,
            string recipient,
            QueryRow row)
        {
            var connection = CreateConnection(subdomain, username, password);
            var queryXml = RaveQueryBuilder.BuildQuery(studyoid, recipient, row);
            Debug.WriteLine("Generated Query XML:");
            Debug.WriteLine(queryXml);
            await SendWithRetryAsync(() => connection.SendRequestAsync(new PostDataRequest(queryXml)));
        }

        private static async Task<IRwsResponse> SendWithRetryAsync(Func<Task<IRwsResponse>> action)
        {
            Exception? lastException = null;

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    return await action();
                }
                catch (RwsException ex) when (IsNonRetryableRwsError(ex.Message))
                {
                    throw;
                }
                catch (Exception ex) when (attempt < MaxAttempts)
                {
                    lastException = ex;
                    await Task.Delay(RetryDelays[attempt - 1]);
                }
            }

            throw lastException ?? new InvalidOperationException("RWS request failed.");
        }

        private static bool IsNonRetryableRwsError(string message)
        {
            return message.Contains("Incorrect login and password combination", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("RWS00008", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("User is locked out", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("RWS00005", StringComparison.OrdinalIgnoreCase);
        }
    }
}

