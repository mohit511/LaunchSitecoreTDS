﻿using System;
using Sitecore.Data.Items;
using System.Text;
using System.Collections.Specialized;
using System.Globalization;
using Sitecore.Globalization;
using System.Web.UI.HtmlControls;
using Sitecore.Analytics;
using Sitecore.Web.UI.WebControls;
using System.Linq;
using Sitecore.Analytics.Data.Items;
using Sitecore.Data;
using LaunchSitecore.Configuration.SiteUI.Base;
using System.Collections.Generic;
using System.Web.UI.WebControls;
using Sitecore.Analytics.Lookups;
using System.Net;
using Sitecore.Data.Fields;
using LaunchSitecore.Configuration;
using Sitecore.Analytics.Model;

namespace LaunchSitecore.layouts.LaunchSitecore.Controls.Analytics
{
  public partial class Current_Visit_Info : SitecoreUserControlBase
  {
    private void Page_Load(object sender, EventArgs e)
    {
      if (!Sitecore.Context.PageMode.IsNormal)
      {
        WriteAlert("visit details are not available in page editor");
        pnlDetails.Visible = false;
      }
      else
      {
        // throw an exception if DMS is not installed.
        if (!Sitecore.Analytics.Configuration.AnalyticsSettings.Enabled) throw new ApplicationException("Launch Sitecore requires DMS to be installed.");

        InitializeLabels();
        FakeIPForLocalhost();
                
        PageCount.Text = Convert.ToString(Tracker.Current.Interaction.PageCount);
        EngagementValue.Text = Convert.ToString(Tracker.Current.Interaction.Value);

        // Populate the Patterns
        if (SiteConfiguration.GetSiteSettingsItem() != null)
        {
          MultilistField profiles = SiteConfiguration.GetSiteSettingsItem().Fields["Visible Profiles"];
          rptPatternList.DataSource = profiles.GetItems();
          rptPatternList.DataBind();
        }

        // the call to Tracker.CurrentVisit.CampaignId will either work or throw
        try
        {
          Item campaign = Sitecore.Context.Database.GetItem(new ID(Tracker.Current.Interaction.CampaignId.Value));
          litCurrentCampaign.Text = campaign.Name;
        }
        catch
        { 
            litCurrentCampaign.Text = GetDictionaryText("Current Campaign Empty");
        }

        if (Tracker.Current.Interaction.HasGeoIpData)
        {
          litCity.Text = Tracker.Current.Interaction.GeoData.City;
          litZip.Text = Tracker.Current.Interaction.GeoData.PostalCode;
        }
        else
        {
          litCity.Text = GetDictionaryText("Pending Lookup");
          litZip.Text = GetDictionaryText("Pending Lookup");
        }

        PagesVisited.DataSource = Tracker.Current.Interaction.Pages.Reverse();
        PagesVisited.DataBind();

        List<PageEventData> Conversions = new List<PageEventData>();
        foreach (Sitecore.Analytics.Core.Page p in Tracker.Current.Interaction.GetPages())
        {
          foreach (PageEventData a in p.PageEvents)
          {
            if (a.IsGoal) { Conversions.Add(a); }
          }
        }

        if (Conversions.Count > 0)
        {
          Conversions.Reverse();
          GoalsAcheived.DataSource = Conversions;
          GoalsAcheived.DataBind();
          GoalsEmpty.Visible = false;
        }
      }
    }

    protected void InitializeLabels()
    { 
      litCampaign.Text = GetDictionaryText("Campaign");
      litCityLabel.Text = GetDictionaryText("GeoIp City");
      litGoals.Text = GetDictionaryText("Goals");
      litLocation.Text = GetDictionaryText("GeoIp Location");
      litPages.Text = GetDictionaryText("Pages");
      litPostalCodeLabel.Text = GetDictionaryText("GeoIp Postal Code");      
      litVisitDetails.Text = GetDictionaryText("Visit Details");
      litPattern.Text = GetDictionaryText("Pattern");
    }

    protected void FakeIPForLocalhost()
    {
      Sitecore.Analytics.Tracking.CurrentInteraction currentVisit = Tracker.Current.Interaction;
      if (currentVisit != null)
      {        
        // if we are local host. our IP is 127 which will not resolve so I am using a 'fake' ip address
        if (currentVisit.Ip[0] == 127)
        {          
          currentVisit.Ip[0] = Convert.ToByte(SiteConfiguration.GetSiteSettingsItem()["IP1"]);
          currentVisit.Ip[1] = Convert.ToByte(SiteConfiguration.GetSiteSettingsItem()["IP2"]);
          currentVisit.Ip[2] = Convert.ToByte(SiteConfiguration.GetSiteSettingsItem()["IP3"]);
          currentVisit.Ip[3] = Convert.ToByte(SiteConfiguration.GetSiteSettingsItem()["IP4"]);

          // Sitecore may have already tried to resolve the 127 and failed, so this will initiate a retry
          //currentVisit.HasGeoIpData = false;

          // Save our changes and let DMS request the GeoIP data again.  
          currentVisit.UpdateGeoIpData(new TimeSpan(0, 0, 0, 0, 100));         
          //currentVisit.AcceptChanges();          
        }       
      }
    }

    protected void PagesVisited_ItemDataBound(object sender, RepeaterItemEventArgs e)
    {
      if (e.Item.ItemType == ListItemType.Item || e.Item.ItemType == ListItemType.AlternatingItem)
      {
        Sitecore.Analytics.Core.Page p = (Sitecore.Analytics.Core.Page)e.Item.DataItem;
        {
          Literal litPage = (Literal)e.Item.FindControl("litPage");

          if (litPage != null)
          {
            string pageName = p.Url.ToString().Replace("/en", "/").Replace("//", "/").Remove(0, 1).Replace(".aspx", "");
            if (pageName == String.Empty || pageName == "en") pageName = "home";
            if (pageName.IndexOf("/") != pageName.LastIndexOf("/"))
            {              
              pageName = pageName.Substring(0, pageName.IndexOf("/") + 1) + "..." + pageName.Substring(pageName.LastIndexOf("/"));
            }
            if (pageName.Length < 27) litPage.Text = String.Format("<a href=\"{0}\">{1}</a> ({2}s)", p.Url, pageName, (p.Duration / 1000.0).ToString("f2"));
            else litPage.Text = String.Format("<a href=\"{0}\">{1}...</a> ({2}s)", p.Url, pageName.Substring(0,26), (p.Duration / 1000.0).ToString("f2"));
          }
        }
      }
    }  

    protected void GoalsAchieved_ItemDataBound(object sender, RepeaterItemEventArgs e)
    {
      if (e.Item.ItemType == ListItemType.Item || e.Item.ItemType == ListItemType.AlternatingItem)
      {
        PageEventData c = (PageEventData)e.Item.DataItem;
        {
          Literal litConversion = (Literal)e.Item.FindControl("litConversion");

          if (litConversion != null)
          {
            litConversion.Text = String.Format("{0} ({1})", c.Name, c.Value);
          }
        }
      }
    }

    protected void rptPatternList_ItemDataBound(object sender, RepeaterItemEventArgs e)
    {      
      if (e.Item.ItemType == ListItemType.Item || e.Item.ItemType == ListItemType.AlternatingItem)
      {
        Item node = (Item)e.Item.DataItem;

        Sublayout currentPattern = (Sublayout)e.Item.FindControl("currentPattern");

        if (currentPattern != null)
        {
          currentPattern.DataSource = node.Paths.FullPath; 
        }
      }
    }
  }
}