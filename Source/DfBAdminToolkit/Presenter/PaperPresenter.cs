﻿namespace DfBAdminToolkit.Presenter {

    using Common.Services;
    using Common.Utils;
    using CsvHelper;
    using CsvHelper.Configuration;
    using Model;
    using View;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;

    public class PaperPresenter
        : PresenterBase, IPaperPresenter
    {

        IList<PaperListViewItemModel> docIds;

        public PaperPresenter(IPaperModel model, IPaperView view)
            : base(model, view)
        {
        }

        protected override void Initialize()
        {
            IPaperView view = base._view as IPaperView;
            IPaperModel model = base._model as IPaperModel;
            PresenterBase.SetViewPropertiesFromModel<IPaperView, IPaperModel>(
                ref view, model
            );

            SyncContext.Post(delegate {
                view.RefreshAccessToken();
            }, null);
        }

        protected override void WireViewEvents()
        {
            if (!IsViewEventsWired) {
                IPaperView view = base._view as IPaperView;
                view.DataChanged += OnDataChanged;
                view.CommandGetPaper += OnCommandGetPaper;
                view.CommandExportPaper += OnCommandExportPaper;
                IsViewEventsWired = true;
            }
        }

        protected override void UnWireViewEvents()
        {
            if (IsViewEventsWired) {
                IPaperView view = base._view as IPaperView;
                view.DataChanged -= OnDataChanged;
                view.CommandGetPaper -= OnCommandGetPaper;
                view.CommandExportPaper -= OnCommandExportPaper;
                IsViewEventsWired = false;
            }
        }

        protected override void CleanUp()
        {
        }

        public void UpdateSettings() {
            OnDataChanged(this, new EventArgs());
        }

        #region REST Service

        private void GetPaperDocs(IPaperModel model, IMainPresenter presenter)
        {
            IMemberServices service = new MemberServices(ApplicationResource.BaseUrl, ApplicationResource.ApiVersion);
            service.ListPaperDocsUrl = ApplicationResource.ActionListPaperDocs;
            service.UserAgentVersion = ApplicationResource.UserAgent;
            string paperAccessToken = ApplicationResource.DefaultAccessToken;
            docIds = new List<PaperListViewItemModel>();

            IList<TeamListViewItemModel> members = SearchOwners(model, presenter);
            string memberId = string.Empty;
            
            foreach (var memberitem in members)
            {
                if (!string.IsNullOrEmpty(memberitem.TeamId))
                {
                    memberId = memberitem.TeamId;
                }
                IDataResponse response = service.ListPaperDocs(new MemberData()
                {
                    MemberId = memberId
                }, paperAccessToken);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (response.Data != null)
                    {
                        string data = response.Data.ToString();
                        dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(data);

                        int resultCount = jsonData["doc_ids"].Count;
                        for (int i = 0; i < resultCount; i++)
                        {
                            dynamic paperDocId = jsonData["doc_ids"][i];

                            // update model
                            PaperListViewItemModel lvItem = new PaperListViewItemModel()
                            {
                                PaperId = paperDocId,
                                MemberId = memberId,
                                IsChecked = true
                            };
                            docIds.Add(lvItem);
                        }
                        //if the group count is above limit - default 1000 we need to grab the cursor and call continue
                        string cursor = jsonData["cursor"]["value"];
                        bool hasMore = jsonData["has_more"];
                        while (hasMore)
                        {
                            service.ListPaperDocsUrl = ApplicationResource.ActionListContinuationPaperDocs;
                            IDataResponse responseCont = service.ListPaperDocs(new MemberData()
                            {
                                Cursor = cursor
                            }, paperAccessToken);

                            string dataCont = responseCont.Data.ToString();
                            dynamic jsonDataCont = JsonConvert.DeserializeObject<dynamic>(dataCont);

                            int resultContCount = jsonDataCont["doc_ids"].Count;
                            for (int i = 0; i < resultContCount; i++)
                            {
                                dynamic paperDocId = jsonDataCont["doc_ids"][i];
                                // update model
                                PaperListViewItemModel lvItem = new PaperListViewItemModel()
                                {
                                    PaperId = paperDocId,
                                    MemberId = memberId,
                                    IsChecked = true
                                };
                                docIds.Add(lvItem);
                            }
                            hasMore = jsonDataCont["has_more"];
                            cursor = jsonDataCont["cursor"]["value"];
                        }
                    }
                }
            }   
        }

        private void GetPaperMetadata(IPaperModel model, IMainPresenter presenter, string docId, string memberId)
        {
            IMemberServices service = new MemberServices(ApplicationResource.BaseUrl, ApplicationResource.ApiVersion);
            service.GetPaperMetadataUrl = ApplicationResource.ActionGetPaperDocMetadata;
            service.UserAgentVersion = ApplicationResource.UserAgent;
            string paperAccessToken = ApplicationResource.DefaultAccessToken;

            IDataResponse response = service.GetPaperMetadata(docId, paperAccessToken, memberId);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                if (response.Data != null)
                {
                    string data = response.Data.ToString();
                    dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(data);

                    dynamic paperDocIdobj = jsonData["doc_id"];
                    dynamic ownerobj = jsonData["owner"];
                    dynamic paperNameobj = jsonData["title"];
                    dynamic createdDateobj = jsonData["created_date"];
                    dynamic statusobj = jsonData["status"][".tag"];
                    dynamic revisionobj = jsonData["revision"];
                    dynamic lastUpdatedDateobj = jsonData["last_updated_date"];
                    dynamic lastEditorobj = jsonData["last_editor"];
                    
                    string paperDocId = paperDocIdobj.Value as string;
                    string owner = ownerobj.Value as string;
                    string paperName = paperNameobj.Value as string;
                    DateTime createdDate = DateTime.MinValue;
                    if (createdDateobj != null)
                    {
                        createdDate = createdDateobj;
                    }
                    string status = statusobj.Value as string;
                    UInt64 revision = 0;
                    if (revisionobj != null)
                    {
                        revision = revisionobj;
                    }
                    DateTime lastUpdatedDate = DateTime.MinValue;
                    if (lastUpdatedDateobj != null)
                    {
                        lastUpdatedDate = lastUpdatedDateobj;
                    }
                    string lastEditor = lastEditorobj.Value as string;

                    // update model
                    PaperListViewItemModel lvItem = new PaperListViewItemModel()
                    {
                        PaperId = paperDocId,
                        Owner = owner,
                        PaperName = paperName,
                        CreatedDate = createdDate,
                        Status = status,
                        Revision = revision,
                        LastUpdatedDate = lastUpdatedDate,
                        LastEditor = lastEditor,
                        MemberId = memberId,
                        IsChecked = true
                    };
                    model.Paper.Add(lvItem);
                }
            }
        }

        private string CreateTeamFolder(IPaperModel model, string teamFolderName, IMainPresenter presenter)
        {
            string errorMessage = string.Empty;
            string fileAccessToken = ApplicationResource.DefaultAccessToken;
            IMemberServices service = new MemberServices(ApplicationResource.BaseUrl, ApplicationResource.ApiVersion);
            service.CreateTeamFolderUrl = ApplicationResource.ActionCreateTeamFolder;
            service.UserAgentVersion = ApplicationResource.UserAgent;
            IServiceResponse response = service.CreateTeamFolder(teamFolderName, fileAccessToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                if (SyncContext != null)
                {
                    SyncContext.Post(delegate
                    {
                        presenter.UpdateProgressInfo(string.Format("Created team folder [" + teamFolderName + "]"));
                    }, null);
                }
            }
            else
            {
                errorMessage = ErrorMessages.FAILED_TO_CREATE_TEAMFOLDER;
            }
            return errorMessage;
        }

        private string SetFolderStatus(IPaperModel model, string teamFolderId, bool activeSetting, IMainPresenter presenter)
        {
            string errorMessage = string.Empty;
            string fileAccessToken = ApplicationResource.DefaultAccessToken;
            IMemberServices service = new MemberServices(ApplicationResource.BaseUrl, ApplicationResource.ApiVersion);
            if (activeSetting)
            {
                service.ActivateTeamFolderUrl = ApplicationResource.ActionActivateTeamFolder;
            }
            if (!activeSetting)
            {
                service.ArchiveTeamFolderUrl = ApplicationResource.ActionArchiveTeamFolder;
            }
            service.UserAgentVersion = ApplicationResource.UserAgent;
            IServiceResponse response = service.SetTeamFolderStatus(teamFolderId, activeSetting, fileAccessToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                if (SyncContext != null)
                {
                    SyncContext.Post(delegate
                    {
                        presenter.UpdateProgressInfo(string.Format("Updated team folder status for [" + teamFolderId + "]"));
                    }, null);
                }
            }
            else
            {
                errorMessage = ErrorMessages.FAILED_TO_UPDATE_TEAM_FOLDER_STATUS;
            }
            return errorMessage;
        }

        private string SetDefaultArchiveSetting(IPaperModel model, string teamFolderId, bool syncSetting, IMainPresenter presenter)
        {
            string errorMessage = string.Empty;
            string fileAccessToken = ApplicationResource.DefaultAccessToken;
            string syncStringSetting = "sync";
            IMemberServices service = new MemberServices(ApplicationResource.BaseUrl, ApplicationResource.ApiVersion);
            service.SyncSettingTeamFolderUrl = ApplicationResource.ActionUpdateDefaultSyncSettingTeamFolder;
            if (!syncSetting)
            {
                syncStringSetting = "no_sync";
            }
            service.UserAgentVersion = ApplicationResource.UserAgent;
            IServiceResponse response = service.SetFolderSyncSetting(teamFolderId, syncStringSetting, fileAccessToken);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                if (SyncContext != null)
                {
                    SyncContext.Post(delegate
                    {
                        presenter.UpdateProgressInfo(string.Format("Updated default sync setting for [" + teamFolderId + "]"));
                    }, null);
                }
            }
            if (response.StatusCode.ToString() == "BadRequest")
            {
                if (SyncContext != null)
                {
                    SyncContext.Post(delegate {
                        presenter.UpdateProgressInfo("Error: This endpoint is only available for teams with access to managed sync");
                    }, null);
                }
            }
            else
            {
                errorMessage = ErrorMessages.FAILED_TO_UPDATE_TEAM_FOLDER_STATUS;
            }
            return errorMessage;
        }

        public IList<TeamListViewItemModel> SearchOwners(IPaperModel model, IMainPresenter presenter)
        {
            IList<TeamListViewItemModel> members = new List<TeamListViewItemModel>();
            if (!string.IsNullOrEmpty(model.AccessToken))
            {
                MemberServices service = new MemberServices(ApplicationResource.BaseUrl, ApplicationResource.ApiVersion);
                service.ListMembersUrl = ApplicationResource.ActionListMembers;
                service.UserAgentVersion = ApplicationResource.UserAgent;
                IDataResponse response = service.ListMembers(new MemberData()
                {
                    SearchLimit = ApplicationResource.SearchDefaultLimit
                }, model.AccessToken);

                if (SyncContext != null)
                {
                    SyncContext.Post(delegate {
                        presenter.UpdateProgressInfo("Searching users...");
                    }, null);
                }
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    if (response.Data != null)
                    {
                        string data = response.Data.ToString();
                        dynamic jsonData = JsonConvert.DeserializeObject<dynamic>(data);
                        int resultCount = jsonData["members"].Count;
                        int total = 0;
                        for (int i = 0; i < resultCount; i++)
                        {
                            dynamic profile = jsonData["members"][i]["profile"];
                            dynamic idObj = profile["team_member_id"];
                            dynamic emailObj = profile["email"];
                            dynamic status = profile["status"];
                            if (status != null && (status[".tag"].ToString().Equals("active") || status[".tag"].ToString().Equals("suspended")))
                            {
                                string teamId = idObj.Value as string;
                                string email = emailObj.Value as string;
                                // update model
                                TeamListViewItemModel lvItem = new TeamListViewItemModel()
                                {
                                    Email = email,
                                    TeamId = teamId
                                };
                                members.Add(lvItem);
                            }
                            if (SyncContext != null)
                            {
                                SyncContext.Post(delegate {
                                    presenter.UpdateProgressInfo("Scanning Account(s): " + (++total));
                                }, null);
                            }
                            Thread.Sleep(1);
                        }
                        // collect more.
                        bool hasMore = jsonData["has_more"];
                        string cursor = jsonData["cursor"];

                        while (hasMore)
                        {
                            service.ListMembersContinuationUrl = ApplicationResource.ActionListMembersContinuation;
                            service.UserAgentVersion = ApplicationResource.UserAgent;
                            IDataResponse responseCont = service.ListMembersContinuation(new MemberData()
                            {
                                Cursor = cursor
                            }, model.AccessToken);

                            string dataCont = responseCont.Data.ToString();
                            dynamic jsonDataCont = JsonConvert.DeserializeObject<dynamic>(dataCont);

                            int resultContCount = jsonDataCont["members"].Count;
                            for (int i = 0; i < resultContCount; i++)
                            {
                                dynamic profile = jsonDataCont["members"][i]["profile"];
                                dynamic idObj = profile["team_member_id"];
                                dynamic emailObj = profile["email"];
                                dynamic status = profile["status"];

                                string teamId = idObj.Value as string;
                                string email = emailObj.Value as string;

                                if (status != null && (status[".tag"].ToString().Equals("active") || status[".tag"].ToString().Equals("suspended")))
                                {
                                    // update model
                                    TeamListViewItemModel lvItem = new TeamListViewItemModel()
                                    {
                                        Email = email,
                                        TeamId = teamId
                                    };
                                    members.Add(lvItem);
                                }
                                if (SyncContext != null)
                                {
                                    SyncContext.Post(delegate {
                                        presenter.UpdateProgressInfo("Scanning Account(s): " + (++total));
                                    }, null);
                                }
                                Thread.Sleep(1);
                            }
                            hasMore = jsonDataCont["has_more"];
                            cursor = jsonDataCont["cursor"];
                        }
                    }
                }
            }
            return members;
        }

        #endregion REST Service

        #region Events

        private void OnCommandGetPaper(object sender, System.EventArgs e)
        {
            IPaperView view = base._view as IPaperView;
            IPaperModel model = base._model as IPaperModel;
            IMainPresenter presenter = SimpleResolver.Instance.Get<IMainPresenter>();
            // clear existing data first
            model.Paper.Clear();

            if (SyncContext != null) {
                SyncContext.Post(delegate {
                    presenter.EnableControl(false);
                    presenter.ActivateSpinner(true);
                    presenter.UpdateProgressInfo("Processing...");
                }, null);
            }
            Thread getpaperdocs = new Thread(() => {
                if (string.IsNullOrEmpty(model.AccessToken))
                {
                    SyncContext.Post(delegate {
                        presenter.EnableControl(true);
                        presenter.ActivateSpinner(false);
                        presenter.UpdateProgressInfo("");
                    }, null);
                }
                else
                {   
                    //get Paper Ids
                    this.GetPaperDocs(model, presenter);
                    
                    //get metadata from ids
                    foreach (PaperListViewItemModel item in docIds)
                    {
                        this.GetPaperMetadata(model, presenter, item.PaperId, item.MemberId);
                    }
                    if (SyncContext != null)
                    {
                        SyncContext.Post(delegate
                        {
                            // update result and update view.
                            PresenterBase.SetViewPropertiesFromModel<IPaperView, IPaperModel>(
                                ref view, model
                            );
                            // update result and update view.
                            view.RenderPaperList();
                            presenter.ActivateSpinner(false);
                            presenter.EnableControl(true);
                            presenter.UpdateProgressInfo("Paper doc list completed. Total Docs [" + model.Paper.Count.ToString() + "]");
                        }, null);
                    }
                }
            });
            getpaperdocs.Start();
        }

        private void OnCommandSetPaperArchiveSetting(object sender, System.EventArgs e)
        {
            IPaperView view = base._view as IPaperView;
            IPaperModel model = base._model as IPaperModel;
            IMainPresenter presenter = SimpleResolver.Instance.Get<IMainPresenter>();
            PaperModel teamModel = view.GetPaperIds();
            bool archiveSetting = view.ArchiveSetting;

            if (SyncContext != null)
            {
                SyncContext.Post(delegate
                {
                    presenter.EnableControl(false);
                    presenter.ActivateSpinner(true);
                    presenter.UpdateProgressInfo("Processing...");

                }, null);
            }
            Thread setfolderarchivesetting = new Thread(() =>
            {
                if (string.IsNullOrEmpty(model.AccessToken))
                {
                    SyncContext.Post(delegate
                    {
                        presenter.EnableControl(true);
                        presenter.ActivateSpinner(false);
                        presenter.UpdateProgressInfo("");
                    }, null);
                }
                else
                {
                    foreach (PaperListViewItemModel lvItem in teamModel.Paper)
                    {
                        this.SetDefaultArchiveSetting(model, lvItem.PaperId, archiveSetting, presenter);
                    }
                    if (SyncContext != null)
                    {
                        SyncContext.Post(delegate
                        {
                            // update result and update view.
                            PresenterBase.SetViewPropertiesFromModel<IPaperView, IPaperModel>(
                                ref view, model
                            );
                            // update result and update view.
                            view.RenderPaperList();
                            presenter.ActivateSpinner(false);
                            presenter.EnableControl(true);
                        }, null);
                    }
                }
            });
            setfolderarchivesetting.Start();
        }

        private void OnCommandExportPaper(object sender, System.EventArgs e)
        {
            IPaperView view = base._view as IPaperView;
            IPaperModel model = base._model as IPaperModel;
            IMainPresenter presenter = SimpleResolver.Instance.Get<IMainPresenter>();

            if (SyncContext != null)
            {
                SyncContext.Post(delegate {
                    presenter.EnableControl(false);
                    presenter.ActivateSpinner(true);
                    presenter.UpdateProgressInfo("Processing...");
                }, null);
            }
            Thread exportteamfolders = new Thread(() => {
                if (string.IsNullOrEmpty(model.AccessToken))
                {
                    SyncContext.Post(delegate {
                        presenter.EnableControl(true);
                        presenter.ActivateSpinner(false);
                        presenter.UpdateProgressInfo("");
                    }, null);
                }
                else
                {
                    this.GetPaperDocs(model, presenter);
                    if (SyncContext != null)
                    {
                        SyncContext.Post(delegate
                        {
                            PresenterBase.SetViewPropertiesFromModel<IPaperView, IPaperModel>(
                                ref view, model
                            );
                            string sPath = string.Empty;

                            if (model.Paper.Count > 0)
                            {
                                //create CSV file in My Documents folder
                                sPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\PaperExport.csv";
                                CsvConfiguration config = new CsvConfiguration()
                                {
                                    HasHeaderRecord = true,
                                    Delimiter = ",",
                                    Encoding = System.Text.Encoding.UTF8
                                };
                                config.RegisterClassMap(new TeamFoldersHeaderMap());
                                int total = model.Paper.Count;
                                using (CsvWriter writer = new CsvWriter(new StreamWriter(sPath), config))
                                {
                                    writer.WriteHeader<TeamFoldersHeaderRecord>();
                                    int count = 0;
                                    foreach (var item in model.Paper)
                                    {
                                        writer.WriteField<string>(item.PaperName);
                                        writer.WriteField<string>(item.PaperId);
                                        writer.WriteField<string>(item.Status);
                                        count++;
                                        if (SyncContext != null)
                                        {
                                            SyncContext.Post(delegate
                                            {
                                                presenter.UpdateProgressInfo(string.Format("Writing Record: {0}/{1}", (count), total));
                                            }, null);
                                        }
                                        writer.NextRecord();
                                    }
                                }
                                if (SyncContext != null)
                                {
                                    SyncContext.Post(delegate
                                    {
                                        presenter.UpdateProgressInfo("Completed. Exported file located at " + sPath);
                                    }, null);
                                }
                            }
                            if (model.Paper.Count == 0)
                            {
                                presenter.UpdateProgressInfo("No team folders were chosen to export.");
                            }
                            presenter.ActivateSpinner(false);
                            presenter.EnableControl(true);
                        }, null);
                    }
                }
            });
            exportteamfolders.Start();
        }

        private void OnDataChanged(object sender, System.EventArgs e) {
            IPaperView view = base._view as IPaperView;
            IPaperModel model = base._model as IPaperModel;
            PresenterBase.SetModelPropertiesFromView<IPaperModel, IPaperView>(
                ref model, view
            );
        }

        #endregion Events
    }
}