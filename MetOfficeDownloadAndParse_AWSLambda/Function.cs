using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Transfer;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace MetOfficeDownloadAndParse_AWSLambda
{
    public class Function
    {
        /// <summary>
        /// Downloads Met Office Station metadata and hourly observations, writes to S3 Bucket
        /// </summary>
        /// <param name="context">AWS Lambda Context</param>
        /// <returns></returns>
        /// 

        private static string _metOfficeKey;
        private static string _metOfficeBaseURL = "http://datapoint.metoffice.gov.uk/public/data/val/wxobs/all/json";
        private static string _stationData = $"/all?res=hourly";
        private static string _stationList = "/sitelist";
        private static string _bucketName;
        private static string _awsKey;
        private static string _awsSecretKey;

        public async Task FunctionHandler(ILambdaContext context)
        {
            SetVars(context);
            await GetStationListing();
            await GetStationData();
        }

        public static void SetVars(ILambdaContext context) {
            _metOfficeKey = GetEnvironmentVariable("metOfficeKey",context);
            _bucketName = GetEnvironmentVariable("bucketName", context);
            _awsKey = GetEnvironmentVariable("awsKey", context);
            _awsSecretKey = GetEnvironmentVariable("awsSecretKey", context);
        }
        private static string GetEnvironmentVariable(string key, ILambdaContext context) {
            if (context.ClientContext == null)
            {
                return Environment.GetEnvironmentVariable(key);
            }
            else {
                return context.ClientContext.Environment[key];
            }
        }
        private static async Task GetDataAndWriteToS3(string url, string keyStub)
        {
            var nowString = DateTime.UtcNow.ToString("o");
            using (var client = new HttpClient())
            {
                using (var response = client.GetAsync(url).Result)
                {
                    //Console.WriteLine($"GET {url} RESPONSE: {response.StatusCode}");
                    LambdaLogger.Log($"GET {url} RESPONSE: {response.StatusCode}");
                    if (response.IsSuccessStatusCode)
                    {
                        using (var stream = ZipStream(await response.Content.ReadAsStreamAsync()))
                        using (var transferUtility = new TransferUtility(_awsKey, _awsSecretKey, Amazon.RegionEndpoint.EUWest2))
                        {
                            var uploadRequest = new TransferUtilityUploadRequest
                            {
                                InputStream = stream,
                                BucketName = _bucketName,
                                CannedACL = S3CannedACL.PublicRead,
                                Key = $"{keyStub}_{nowString}.zip"
                            };
                            uploadRequest.UploadProgressEvent += new EventHandler<UploadProgressArgs>(LogS3Progress);
                            await transferUtility.UploadAsync(uploadRequest);
                        }
                    }
                }
            }
        }
        private static Stream ZipStream(Stream str) {
            var ms = new MemoryStream();
            using (var za = new ZipArchive(ms, ZipArchiveMode.Create,true))
            {
                var entry = za.CreateEntry("data.json");
                str.CopyTo(entry.Open());
            }
            return ms;
        }
        private static async Task GetStationData()
        {
             await GetDataAndWriteToS3($"{_metOfficeBaseURL}{_stationData}&key={_metOfficeKey}", "stationdata");
        }
        private static async Task GetStationListing()
        {
            await GetDataAndWriteToS3($"{_metOfficeBaseURL}{_stationList}?key={_metOfficeKey}", "stationlist");
        }
        private static void LogS3Progress(object sender, UploadProgressArgs args)
        {
            LambdaLogger.Log($"File: {args.FilePath} {args.PercentDone.ToString()}% Done");
        }

    }
}
