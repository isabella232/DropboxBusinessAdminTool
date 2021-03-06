﻿namespace DfBAdminToolkit.View
{
    using Model;
    using System;
    using System.Collections.Generic;

    public interface ITeamAuditingView
        : IView
    {
        event EventHandler CommandLoadTeamEvents;
        event EventHandler CommandLoadCSV;
        event EventHandler CommandExportToCSV;
        event EventHandler CommandFilterMembers;

        string AccessToken { get; set; }

        DateTime StartTime { get; set; }

        DateTime EndTime { get; set; }

        void RefreshAccessToken();

        string TeamAuditingInputFilePath { get; set; }

        string EventCategory { get; set; }

        void RenderTeamAuditingList(List<TeamAuditingListViewItemModel> TeamAuditing);

        List<TeamAuditingListViewItemModel> RenderTeamAuditingFilteredMemberList(List<MemberListViewItemModel> members, List<TeamAuditingListViewItemModel> TeamAuditing, List<TeamAuditingListViewItemModel> newAudit);
    }
}