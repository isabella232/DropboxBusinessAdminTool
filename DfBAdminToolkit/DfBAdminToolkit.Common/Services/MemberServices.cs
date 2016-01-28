﻿namespace DfBAdminToolkit.Common.Services
{
    using Newtonsoft.Json.Linq;
    using RestSharp;
    using System;
    using System.IO;
    using System.Net;

    public class MemberServices
        : IMemberServices
    {
        private readonly string _baseUrl;
        private readonly string _apiVersion;

        public string AddMemberUrl { get; set; }

        public string ListMembersUrl { get; set; }

        public string ListMembersContinuationUrl { get; set; }

        public string SearchFilesUrl { get; set; }

        public string ListFolderUrl { get; set; }

        public string FileDumpUrl { get; set; }

        public MemberServices(
            string baseUrl,
            string apiVersion)
        {
            _baseUrl = baseUrl;
            _apiVersion = apiVersion;
        }

        public IServiceResponse AddMember(IMemberData data, string authToken)
        {
            IServiceResponse serviceResponse = null;
            try
            {
                if (!string.IsNullOrEmpty(AddMemberUrl))
                {
                    RestClient client = new RestClient(
                        string.Format("{0}/{1}/", _baseUrl, _apiVersion)
                    );
                    RestRequest request = new RestRequest(AddMemberUrl, Method.POST);
                    request.AddHeader("Authorization", "Bearer " + authToken);
                    request.AddHeader("Content-Type", "application/json");

                    //currently creates all as member only and sends email (could be altered)
                    JObject jsonProv = new JObject(
                        new JProperty("new_members",
                            new JArray(
                                new JObject(
                                    new JProperty("member_email", data.Email),
                                    new JProperty("member_given_name", data.FirstName),
                                    new JProperty("member_surname", data.LastName),
                                    new JProperty("send_welcome_email", data.SendWelcomeEmail),
                                    new JProperty("role",
                                        new JObject(
                                            new JProperty(".tag", data.RoleName)
                                        )
                                    )
                                )
                            )
                        ),
                        new JProperty("force_async", false)
                    );
                    request.AddParameter("application/json", jsonProv, ParameterType.RequestBody);
                    request.RequestFormat = DataFormat.Json;
                    IRestResponse response = client.Execute(request);
                    serviceResponse = new ServiceResponse(response.StatusCode, response.ErrorMessage);
                }
                else
                {
                    throw new ArgumentNullException("Missing service url");
                }
            }
            catch (Exception e)
            {
                serviceResponse = new ServiceResponse(HttpStatusCode.InternalServerError, e.Message);
            }

            return serviceResponse;
        }

        public IDataResponse ListMembers(IMemberData data, string authToken)
        {
            IDataResponse dataResponse = null;
            try
            {
                if (!string.IsNullOrEmpty(ListMembersUrl))
                {
                    RestClient client = new RestClient(
                        string.Format("{0}/{1}/", _baseUrl, _apiVersion)
                    );
                    RestRequest request = new RestRequest(ListMembersUrl, Method.POST);

                    //add headers
                    request.AddHeader("Authorization", "Bearer " + authToken);
                    JObject json = new JObject(
                        new JProperty("limit", data.SearchLimit)
                    );
                    request.AddParameter("application/json", json, ParameterType.RequestBody);
                    request.RequestFormat = DataFormat.Json;
                    IRestResponse response = client.Execute(request);
                    dataResponse = new DataResponse(response.StatusCode, response.ErrorMessage, response.Content);
                }
                else
                {
                    throw new ArgumentNullException("Missing service url");
                }
            }
            catch (Exception e)
            {
                dataResponse = new DataResponse(HttpStatusCode.InternalServerError, e.Message, null);
            }
            return dataResponse;
        }

        public IDataResponse ListMembersContinuation(IMemberData data, string authToken)
        {
            IDataResponse dataResponse = null;
            try
            {
                if (!string.IsNullOrEmpty(ListMembersContinuationUrl))
                {
                    RestClient client = new RestClient(
                        string.Format("{0}/{1}/", _baseUrl, _apiVersion)
                    );
                    RestRequest request = new RestRequest(ListMembersContinuationUrl, Method.POST);

                    //add headers
                    request.AddHeader("Authorization", "Bearer " + authToken);
                    JObject json = new JObject(
                        new JProperty("cursor", data.Cursor)
                    );
                    request.AddParameter("application/json", json, ParameterType.RequestBody);
                    request.RequestFormat = DataFormat.Json;
                    IRestResponse response = client.Execute(request);
                    dataResponse = new DataResponse(response.StatusCode, response.ErrorMessage, response.Content);
                }
                else
                {
                    throw new ArgumentNullException("Missing service url");
                }
            }
            catch (Exception e)
            {
                dataResponse = new DataResponse(HttpStatusCode.InternalServerError, e.Message, null);
            }
            return dataResponse;
        }

        public IDataResponse ListFolders(IMemberData data, string authToken)
        {
            IDataResponse dataResponse = null;
            try
            {
                if (!string.IsNullOrEmpty(ListFolderUrl))
                {
                    RestClient client = new RestClient(
                           string.Format("{0}/{1}/", _baseUrl, _apiVersion)
                       );
                    RestRequest request = new RestRequest(ListFolderUrl, Method.POST);

                    request.AddHeader("Authorization", "Bearer " + authToken);
                    request.AddHeader("Content-Type", "application/json");
                    request.AddHeader("Dropbox-API-Select-User", data.MemberId);

                    //set up properties for JSON to the API
                    JObject jsonSearch = new JObject(
                        new JProperty("path", ""),
                        new JProperty("recursive", true),
                        new JProperty("include_media_info", false)
                    );

                    request.AddParameter("application/json", jsonSearch, ParameterType.RequestBody);
                    IRestResponse response = client.Execute(request);
                    dataResponse = new DataResponse(response.StatusCode, response.ErrorMessage, response.Content);
                }
            }
            catch (Exception e)
            {
                dataResponse = new DataResponse(HttpStatusCode.InternalServerError, e.Message, null);
            }
            return dataResponse;
        }

        public IDataResponse DumpFile(IMemberData data, string outputFolder, string authToken)
        {
            IDataResponse dataResponse = null;
            try
            {
                if (!string.IsNullOrEmpty(FileDumpUrl))
                {
                    string pathString = string.Concat(@"{""path"":""", data.Path, @"""}");
                    string url = string.Format("{0}/{1}/", _baseUrl, _apiVersion);
                    RestClient client = new RestClient(url);
                    RestRequest request = new RestRequest(FileDumpUrl, Method.GET);

                    //add headers, include user authentication we pass in with admin privileges
                    request.AddHeader("Authorization", "Bearer " + authToken);
                    request.AddHeader("Dropbox-API-Select-User", data.MemberId);
                    request.AddHeader("Dropbox-API-Arg", pathString);

                    //download file by using raw bytes returned
                    byte[] jsonResponseDump = client.DownloadData(request);

                    //get the Dropbox folder structure so we can recreate correct folder structure locally under user folder below
                    String dbdirName =  Path.GetDirectoryName(data.Path);
                    dbdirName = dbdirName.Remove(0, 1);

                    // create folder if doesn't exists.
                    // strip out email prefix as folder name.
                    int index = data.Email.IndexOf("@");
                    string folderName = data.Email.Substring(0, index);

                    //combine Dropbox subdirectory to email username
                    if (!string.IsNullOrEmpty(dbdirName))
                    {
                        folderName = folderName + "\\" + dbdirName;
                    }
                    string fullOutputFolder = Path.Combine(outputFolder, folderName);

                    if (!Directory.Exists(fullOutputFolder))
                    {
                        Directory.CreateDirectory(fullOutputFolder);
                    }

                    string outputPath = Path.Combine(fullOutputFolder, data.FileName);
                    File.WriteAllBytes(outputPath, jsonResponseDump);
                }
            }
            catch (Exception e)
            {
                dataResponse = new DataResponse(HttpStatusCode.InternalServerError, e.Message, null);
            }
            return dataResponse;
        }

        public IDataResponse SearchFiles(IMemberData data, string authToken)
        {
            IDataResponse dataResponse = null;
            try
            {
                if (!string.IsNullOrEmpty(SearchFilesUrl))
                {
                    RestClient client = new RestClient(
                           string.Format("{0}/{1}/", _baseUrl, _apiVersion)
                       );
                    RestRequest request = new RestRequest(SearchFilesUrl, Method.POST);
                    //add headers
                    request.AddHeader("Authorization", "Bearer " + authToken);
                    request.AddHeader("Content-Type", "application/json");
                    request.AddHeader("Dropbox-API-Select-User", data.MemberId);

                    //set up properties for JSON to the API
                    JObject jsonSearch = new JObject(
                        new JProperty("path", ""),
                        new JProperty("query", data.SearchText),
                        new JProperty("max_results", data.SearchLimit),
                        new JProperty("mode", data.SearchMode)
                    );
                    request.AddParameter("application/json", jsonSearch, ParameterType.RequestBody);
                    IRestResponse response = client.Execute(request);
                    dataResponse = new DataResponse(response.StatusCode, response.ErrorMessage, response.Content);
                }
                else
                {
                    throw new ArgumentNullException("Missing service url");
                }
            }
            catch (Exception e)
            {
                dataResponse = new DataResponse(HttpStatusCode.InternalServerError, e.Message, null);
            }
            return dataResponse;
        }
    }
}